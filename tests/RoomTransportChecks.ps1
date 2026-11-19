$ErrorActionPreference='Stop'
$release=Join-Path $PSScriptRoot '../Control4Gateway/bin/Release'
Get-ChildItem $release -Filter '*.dll' | ForEach-Object {[void][Reflection.Assembly]::LoadFrom($_.FullName)}
$library=[Reflection.Assembly]::LoadFrom((Join-Path $release 'Control4Gateway.dll'))
$coreType=$library.GetType('Control4Crestron.GatewayCore',$true)
$type=$library.GetType('Control4Crestron.Control4_Room_Transport',$true)
$registry=$library.GetType('Control4Crestron.GatewayRegistry',$true)
$flags=[Reflection.BindingFlags]'NonPublic,Instance'
$static=[Reflection.BindingFlags]'NonPublic,Static'
$build=$coreType.GetMethod('BuildCommandMetadataReport',$static)
$report=$build.Invoke($null,[object[]]@('[{"command":"PLAY","parameters":[]},{"command":"PAUSE","parameters":{}},{"command":"STOP","parameters":[{"name":"ITEM_ID","required":true}]},{"command":"ROOM_OFF","parameters":[]},{"command":"GUIDE","parameters":[]},{"command":"MUTE_TOGGLE"}]',200,13))
$gate=$coreType.GetMethod('AdvertisedTransportCommand',$static)
foreach($case in @(@('Play',$true,'PLAY'),@('Pause',$true,'PAUSE'),@('Stop',$false,''),@('Room_Off',$true,'ROOM_OFF'),@('Guide',$true,'GUIDE'),@('Mute',$true,'MUTE_TOGGLE'),@('Invalid',$false,''))){
 $args=[object[]]@($report,$case[0],$null)
 if($gate.Invoke($null,$args) -ne $case[1] -or ($case[1] -and $args[2] -ne $case[2])){throw 'Transport metadata/parameter gate failed'}
}
$empty=$build.Invoke($null,[object[]]@('[]',200,13))
$args=[object[]]@($empty,'Play',$null)
if($gate.Invoke($null,$args)){throw 'Empty command metadata granted capability'}
$module=[Activator]::CreateInstance($type)
$script:capabilities=[Collections.Generic.List[object]]::new()
$event=$type.GetEvent('CapabilitiesChanged')
$handler=[Management.Automation.LanguagePrimitives]::ConvertTo([scriptblock]{param($sender,$feedback) $script:capabilities.Add($feedback)},$event.EventHandlerType)
$event.AddEventHandler($module,$handler)
if($module.Initialize(62013,13) -ne 1){throw 'Transport init failed'}
$core=$registry.GetMethod('Get').Invoke($null,[object[]]@([ushort]62013))
if($coreType.GetField('_busy',$flags).GetValue($core) -ne 0 -or $coreType.GetField('_roomTimer',$flags)){throw 'Transport startup created I/O or polling'}
$rooms=$coreType.GetField('_transportRooms',$flags).GetValue($core)
$room=$rooms[[ushort]13]
$room.GetType().GetField('Report',$flags).SetValue($room,$report)
$publish=$coreType.GetMethod('PublishTransport',$flags)
[void]$publish.Invoke($core,[object[]]@([ushort]13,$room))
$cap=$script:capabilities[-1]
$packet=$cap.Json | ConvertFrom-Json
if($packet.wire -ne 'keys1' -or $packet.states.Length -ne 50 -or $cap.MappedCount -ne 5 -or $cap.PlayAvailable -ne 1 -or $cap.StopAvailable -ne 0){throw 'SR260 catalog/capability output incorrect'}
if([Text.Encoding]::UTF8.GetByteCount($cap.Json) -gt 255){throw 'Capability packet too large'}
$script:locals=[Collections.Generic.List[object]]::new()
$localEvent=$type.GetEvent('LocalActionRequested')
$localHandler=[Management.Automation.LanguagePrimitives]::ConvertTo([scriptblock]{param($sender,$feedback) $script:locals.Add($feedback.Message)},$localEvent.EventHandlerType)
$localEvent.AddEventHandler($module,$localHandler)
$module.Press('Watch'); $module.Press('Listen'); $module.Press('List'); $module.Press('Play'); $module.Press('Guide')
if($script:locals.Count -ne 3 -or $coreType.GetField('_busy',$flags).GetValue($core) -ne 0){throw 'Local routing or offline no-queue failed'}
$snap=$coreType.GetMethod('TransportCommandReports',$flags).Invoke($core,@())
$snap[0]['commands'].RemoveAll()
if(($room.GetType().GetField('Report',$flags).GetValue($room)['commands']).Count -ne 6){throw 'Inventory report exposed mutable live metadata'}
[void]$coreType.GetMethod('InvalidateTransportRooms',$flags).Invoke($core,@())
if($script:capabilities[-1].DiscoveryValid -ne 0 -or $script:capabilities[-1].PlayAvailable -ne 0){throw 'Disconnect retained command capabilities'}
[void]$module.Initialize(62013,13)
if($rooms[[ushort]13].GetType().GetField('References',$flags).GetValue($rooms[[ushort]13]) -ne 1){throw 'Transport reinit leaked registration'}
$event.RemoveEventHandler($module,$handler)
$localEvent.RemoveEventHandler($module,$localHandler)
Write-Output 'Transport checks passed: REST metadata/argument gate, 50 SR260 keys, packet bound, no startup I/O, local UI requests, offline rejection, snapshot isolation, invalidation and refcounts.'

$controllerFixture=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'fixtures/Room13Commands_2026-11-15.json'))
$jsonAssembly=[Reflection.Assembly]::LoadFrom((Join-Path $release 'SimplSharpNewtonsoft.dll'))
$parse=$jsonAssembly.GetType('Newtonsoft.Json.Linq.JToken').GetMethod('Parse',[type[]]@([string]))
$actual=$parse.Invoke($null,[object[]]@($controllerFixture))
$keys=$coreType.GetField('Sr260Keys',$static).GetValue($null)
$mapped=0
foreach($key in $keys){$args=[object[]]@($actual,$key,$null); if($gate.Invoke($null,$args)){$mapped++}}
if($mapped -ne 43){throw ('Actual controller command mapping count incorrect: '+$mapped)}
foreach($case in @(@('DVR','PVR'),@('Select','ENTER'),@('Digit_9','NUMBER_9'),@('Previous','RECALL'),@('Rewind','SCAN_REV'))){
 $args=[object[]]@($actual,$case[0],$null)
 if(!$gate.Invoke($null,$args) -or $args[2] -ne $case[1]){throw 'Actual REST key mapping incorrect'}
}
Write-Output 'Actual RMC4-exported metadata checks passed: 95 definitions, 43 advertised key mappings; colors remain unresolved and Watch/Listen/List remain local UI.'
