. (Join-Path $PSScriptRoot 'ReadRoomUiFrames.ps1')
$ErrorActionPreference='Stop'
$release=Join-Path $PSScriptRoot '../Control4Gateway/bin/Release'
Get-ChildItem $release -Filter '*.dll' | ForEach-Object {[void][Reflection.Assembly]::LoadFrom($_.FullName)}
$library=[Reflection.Assembly]::LoadFrom((Join-Path $release 'Control4Gateway.dll'))
$type=$library.GetType('Control4Crestron.Control4_Room',$true)
$coreType=$library.GetType('Control4Crestron.GatewayCore',$true)
$registry=$library.GetType('Control4Crestron.GatewayRegistry',$true)
$flags=[Reflection.BindingFlags]'NonPublic,Instance'
$module=[Activator]::CreateInstance($type)
if($module.Initialize(62012,13) -ne 1){throw 'Two-ID room initialization failed'}
$core=$registry.GetMethod('Get').Invoke($null,[object[]]@([ushort]62012))
if($coreType.GetField('_roomTimer',$flags) -or $coreType.GetField('_busy',$flags).GetValue($core) -ne 0){throw 'Room started network/timer offline'}
$rooms=$coreType.GetField('_audioRooms',$flags).GetValue($core)
$room=$rooms[[ushort]13]; $roomType=$room.GetType()
if(!$roomType.GetField('AutomaticMapping',$flags).GetValue($room)){throw 'Room-only registration requires mapping'}
$script:browser=[Collections.Generic.List[object]]::new()
$event=$type.GetEvent('BrowserChanged')
$handler=[Management.Automation.LanguagePrimitives]::ConvertTo([scriptblock]{param($sender,$feedback) $script:browser.Add($feedback)},$event.EventHandlerType)
$event.AddEventHandler($module,$handler)
$jsonAssembly=[Reflection.Assembly]::LoadFrom((Join-Path $release 'SimplSharpNewtonsoft.dll'))
$parse=$jsonAssembly.GetType('Newtonsoft.Json.Linq.JToken').GetMethod('Parse',[type[]]@([string]))
$discovery=$parse.Invoke($null,[object[]]@('{"source_discovery_status":"discovered","rooms":[{"room_id":13,"name":"Living Room","watch_sources":[{"source_id":2061,"name":"DIRECTV Stream","Source_ID$":"2061"}],"listen_sources":[{"source_id":65,"name":"Spotify","Source_ID$":"65"},{"source_id":100002,"name":"Digital Media","Source_ID$":"100002"}]},{"room_id":14,"name":"Other","watch_sources":[],"listen_sources":[]}]}'))
$coreType.GetField('_generation',$flags).SetValue($core,4)
$coreType.GetField('_feedbackEpoch',$flags).SetValue($core,3)
$coreType.GetField('_online',$flags).SetValue($core,$true)
$coreType.GetField('_roomDiscovery',$flags).SetValue($core,$discovery)
$coreType.GetField('_roomDiscoveryGeneration',$flags).SetValue($core,4)
$coreType.GetField('_roomDiscoveryEpoch',$flags).SetValue($core,3)
$publish=$coreType.GetMethod('PublishRoomDiscovery',$flags)
[void]$publish.Invoke($core,[object[]]@([ushort]13))
if($script:browser[-1].ListenCount -ne 2 -or $script:browser[-1].WatchCount -ne 1 -or $script:browser[-1].HasWatch -ne 1){throw 'Room source summary failed'}
$listEvent=$type.GetEvent('SourceListsChanged')
$script:sourceLists=[Collections.Generic.List[object]]::new()
$listHandler=[Management.Automation.LanguagePrimitives]::ConvertTo([scriptblock]{param($sender,$feedback) $script:sourceLists.Add($feedback)},$listEvent.EventHandlerType)
$listEvent.AddEventHandler($module,$listHandler)
$module.SetAudioListPage(1)
$packet=Read-RoomUiDocument $module 'a' {$script:sourceLists[-1].AudioJson}
if(!$packet.valid -or $packet.total -ne 2 -or $packet.items[1].id -cne '100002' -or $packet.items[0].name -ne 'Spotify'){throw 'Widget list contract failed'}
$resolve=$type.GetMethod('TryListedSourceId',$flags)
foreach($case in @(@('100002',$false,$true,100002),@('2061',$true,$true,2061),@('2061',$false,$false,0),@('65',$true,$false,0),@('bad',$false,$false,0),@('0',$false,$false,0))){
 $arguments=[object[]]@($case[0],$case[1],0)
 $accepted=$resolve.Invoke($module,$arguments)
 if($accepted -ne $case[2] -or $arguments[2] -ne $case[3]){throw 'Direct source ID membership validation failed'}
}
$pageBuilder=$type.GetMethod('SourcePages',[Reflection.BindingFlags]'NonPublic,Static')
$longSources=@(1..30 | ForEach-Object {[ordered]@{source_id=$_;name=('A"quoted\name'+('x'*480));'Source_ID$'=[string]$_}}) | ConvertTo-Json -Depth 5
$sourceTokens=$parse.Invoke($null,[object[]]@([string]$longSources))
$pageArguments=[object[]]::new(1)
$pageArguments[0]=$sourceTokens
$pages=$pageBuilder.Invoke($null,$pageArguments)
$seen=0
foreach($page in $pages){
 $json=$page.ToString([Enum]::Parse($jsonAssembly.GetType('Newtonsoft.Json.Formatting'),'None'))
 if([Text.Encoding]::UTF8.GetByteCount($json) -gt 3500){throw 'Source list page exceeds payload bound'}
 $seen+=$page.Count
}
if($pages.Count -le 1 -or $seen -ne 30){throw 'Paging lost sources'}
$module.SetAudioListPage(65535)
if($script:sourceLists[-1].AudioPage -ne $script:sourceLists[-1].AudioPages){throw 'Out of range page not clamped'}
$slotResolver=$type.GetMethod('TrySlotSource',$flags)
$nameResolver=$type.GetMethod('TryNamedSource',$flags)
foreach($case in @(@([ushort]1,$true,$true,2061),@([ushort]2,$false,$true,100002),@([ushort]0,$false,$false,0),@([ushort]3,$false,$false,0))){
 $arguments=[object[]]@($case[0],$case[1],0)
 if($slotResolver.Invoke($module,$arguments) -ne $case[2] -or $arguments[2] -ne $case[3]){throw 'Numbered source slot resolution failed'}
}
foreach($case in @(@(' spotify ',$false,$true,65),@('DIRECTV STREAM',$true,$true,2061),@('Spotify',$true,$false,0),@('',$false,$false,0))){
 $arguments=[object[]]@($case[0],$case[1],0)
 if($nameResolver.Invoke($module,$arguments) -ne $case[2] -or $arguments[2] -ne $case[3]){throw 'Source-name selection matching failed'}
}
$listenField=$type.GetField('_listen',$flags)
$listen=$listenField.GetValue($module)
$duplicate=$parse.Invoke($null,[object[]]@('{"source_id":609,"name":"SPOTIFY","Source_ID$":"609"}'))
$listen.Add($duplicate)
$arguments=[object[]]@('Spotify',$false,0)
if($nameResolver.Invoke($module,$arguments)){throw 'Ambiguous source name was accepted'}
$listen.RemoveAt(2)
$script:sourceSlots=[Collections.Generic.List[object]]::new()
$slotEvent=$type.GetEvent('SourceSlotChanged')
$slotHandler=[Management.Automation.LanguagePrimitives]::ConvertTo([scriptblock]{param($sender,$feedback) $script:sourceSlots.Add($feedback)},$slotEvent.EventHandlerType)
$slotEvent.AddEventHandler($module,$slotHandler)
$script:lastSlotStatus=$null
$statusEvent=$type.GetEvent('SourceSlotsStatusChanged')
$statusHandler=[Management.Automation.LanguagePrimitives]::ConvertTo([scriptblock]{param($sender,$feedback) $script:lastSlotStatus=$feedback},$statusEvent.EventHandlerType)
$statusEvent.AddEventHandler($module,$statusHandler)
if($module.ConfigureSourceSlots(1,1) -ne 1 -or $script:lastSlotStatus.AudioOverflow -ne 1 -or $script:lastSlotStatus.AudioOverflowCount -ne 1){throw 'Configured slot overflow not reported'}
$arguments=[object[]]@([ushort]2,$false,0)
if($slotResolver.Invoke($module,$arguments)){throw 'Selection beyond configured slots accepted'}
$arguments=[object[]]@('Digital Media',$false,0)
if($nameResolver.Invoke($module,$arguments)){throw 'Name beyond configured slots accepted'}
if($module.ConfigureSourceSlots(0,10) -ne 0){throw 'Invalid slot count accepted'}
[void]$module.ConfigureSourceSlots(10,10)
$statusEvent.RemoveEventHandler($module,$statusHandler)
$count=$script:browser.Count
[void]$publish.Invoke($core,[object[]]@([ushort]14))
if($script:browser.Count -ne $count){throw 'Other room changed browser'}
# No live timer or network is needed to verify state invalidation and offline controls.
$coreType.GetField('_online',$flags).SetValue($core,$false)
$module.SelectAudioSourceId('65')
$module.RoomOff()
$module.SetVolume(25)
if($coreType.GetField('_busy',$flags).GetValue($core) -ne 0){throw 'Room queued offline commands'}
[void]$coreType.GetMethod('StopRealtime',$flags).Invoke($core,@())
if($script:browser[-1].SourcesValid -ne 0 -or $script:browser[-1].HasWatch -ne 0){throw 'Disconnect retained usable source list'}
$audio=[Activator]::CreateInstance($library.GetType('Control4Crestron.Control4_Audio',$true))
if($audio.Initialize(62012,13,682,340,4003) -ne 1){throw 'Explicit Audio mapping could not upgrade Room-only registration'}
if($roomType.GetField('WatchId',$flags).GetValue($room) -ne 682 -or $roomType.GetField('References',$flags).GetValue($room) -ne 2){throw 'Shared Room/Audio state damaged'}
[void]$module.Initialize(62012,13)
if($roomType.GetField('References',$flags).GetValue($room) -ne 2){throw 'Room reinitialization leaked references'}
$event.RemoveEventHandler($module,$handler)
$listEvent.RemoveEventHandler($module,$listHandler)
Write-Output 'Room checks passed: two-ID startup without I/O, room/source discovery, named paging, large IDs, room isolation, offline no-queue, disconnect invalidation and shared mapping upgrade.'










