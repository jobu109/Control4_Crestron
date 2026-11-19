$ErrorActionPreference = 'Stop'
$release = Join-Path $PSScriptRoot '../Control4Gateway/bin/Release'
Get-ChildItem $release -Filter '*.dll' | ForEach-Object { [void][Reflection.Assembly]::LoadFrom($_.FullName) }
$library = [Reflection.Assembly]::LoadFrom((Join-Path $release 'Control4Gateway.dll'))
$coreType = $library.GetType('Control4Crestron.GatewayCore', $true)
$adapterType = $library.GetType('Control4Crestron.Control4_Audio', $true)
$registry = $library.GetType('Control4Crestron.GatewayRegistry', $true)
$flags = [Reflection.BindingFlags]'NonPublic,Instance'
$staticFlags = [Reflection.BindingFlags]'NonPublic,Static'
$parser = $coreType.GetMethod('TryAudioVariable',$staticFlags)
foreach ($case in @(@('[{"value":0}]','CURRENT_VOLUME',$true,0),@('[{"value":"100"}]','CURRENT_VOLUME',$true,100),@('[{"value":101}]','CURRENT_VOLUME',$false,0),@('[{"value":50.5}]','CURRENT_VOLUME',$false,0),@('[{"value":true}]','POWER_STATE',$true,1),@('[{"value":"0"}]','IS_MUTED',$true,0),@('[{"value":2}]','IS_MUTED',$false,0),@('[{"value":"Undefined"}]','POWER_STATE',$false,0),@('[]','POWER_STATE',$false,0),@('[{}]','IS_MUTED',$false,0),@('bad','POWER_STATE',$false,0),@('[{"value":1}]','UNKNOWN',$false,0))) {
    $arguments = [object[]]@($case[0],$case[1],[ushort]0)
    $actual = $parser.Invoke($null,$arguments)
    if ($actual -ne $case[2] -or ($actual -and $arguments[2] -ne $case[3])) { throw ('Audio variable parse failed: ' + $case[0]) }
}
$adapter = [Activator]::CreateInstance($adapterType)
if ($adapter.Initialize([ushort]62010,[ushort]13,[ushort]682,[ushort]340,[ushort]4003) -ne 1) { throw 'Valid audio registration rejected' }
$core = $registry.GetMethod('Get').Invoke($null,[object[]]@([ushort]62010))
$rooms = $coreType.GetField('_audioRooms',$flags).GetValue($core)
$room = $rooms[[ushort]13]
$roomType = $room.GetType()
$coreType.GetField('_generation',$flags).SetValue($core,4)
$coreType.GetField('_feedbackEpoch',$flags).SetValue($core,3)
$coreType.GetField('_online',$flags).SetValue($core,$true)
$event = $adapterType.GetEvent('FeedbackChanged')
$script:audioResults = [Collections.Generic.List[object]]::new()
$handler = [Management.Automation.LanguagePrimitives]::ConvertTo([scriptblock]{param($sender,$feedback) $script:audioResults.Add($feedback)},$event.EventHandlerType)
$event.AddEventHandler($adapter,$handler)
$jsonAssembly = [Reflection.Assembly]::LoadFrom((Join-Path $release 'SimplSharpNewtonsoft.dll'))
$parse = $jsonAssembly.GetType('Newtonsoft.Json.Linq.JToken').GetMethod('Parse',[type[]]@([string]))
$route = $coreType.GetMethod('RouteRealtimeAudio',$flags)
function Send-AudioFixture([string]$Json,[int]$Generation=4) {
    $message = $parse.Invoke($null,[object[]]@($Json))
    [void]$route.Invoke($core,[object[]]@($message,$Generation))
}
$workType = $coreType.GetNestedType('AudioQueryWork',[Reflection.BindingFlags]'NonPublic')
$work = [Activator]::CreateInstance($workType,$true)
$workType.GetField('Generation',$flags).SetValue($work,4)
$workType.GetField('Epoch',$flags).SetValue($work,3)
$workType.GetField('Configuration',$flags).SetValue($work,$room)
# Coalesce event-triggered queries without scheduling real network traffic.
$queries = $coreType.GetField('_audioQueries',$flags).GetValue($core)
$queries.Add([ushort]13,$work)
Send-AudioFixture '{"evtName":"OnDataToUI","iddevice":13,"data":{"volume_level":44,"mute":false}}'
if ($workType.GetField('Dirty',$flags).GetValue($work)) { throw 'Room ID treated as endpoint' }
Send-AudioFixture '{"evtName":"OnDataToUI","iddevice":682,"data":{"volume_level":22,"mute":true}}'
if (!$workType.GetField('Dirty',$flags).GetValue($work) -or $script:audioResults.Count -ne 0) { throw 'Endpoint state assigned directly instead of room readback' }
$apply = $coreType.GetMethod('ApplyAudioReadback',$flags)
[void]$apply.Invoke($core,[object[]]@([ushort]13,$work,'POWER_STATE',[ushort]1,[ushort]1,0))
[void]$apply.Invoke($core,[object[]]@([ushort]13,$work,'CURRENT_VOLUME',[ushort]22,[ushort]1,$roomType.GetField('VolumeVersion',$flags).GetValue($room)))
if ($script:audioResults[$script:audioResults.Count-1].Valid -ne 0) { throw 'Incomplete state marked valid' }
[void]$apply.Invoke($core,[object[]]@([ushort]13,$work,'IS_MUTED',[ushort]1,[ushort]1,$roomType.GetField('MuteVersion',$flags).GetValue($room)))
if ($script:audioResults[$script:audioResults.Count-1].Valid -ne 1) { throw 'Complete measured state not marked valid' }
$oldVersion = $roomType.GetField('VolumeVersion',$flags).GetValue($room)
Send-AudioFixture '{"evtName":"OnDataToUI","iddevice":682,"data":{"volume_level":55}}'
[void]$apply.Invoke($core,[object[]]@([ushort]13,$work,'CURRENT_VOLUME',[ushort]22,[ushort]1,$oldVersion))
if ($roomType.GetField('Volume',$flags).GetValue($room) -ne 22) { throw 'Stale read or direct endpoint assignment accepted' }
[void]$apply.Invoke($core,[object[]]@([ushort]13,$work,'CURRENT_VOLUME',[ushort]18,[ushort]1,$roomType.GetField('VolumeVersion',$flags).GetValue($room)))
if ($roomType.GetField('Volume',$flags).GetValue($room) -ne 18) { throw 'Fresh room read not applied' }
$version = $roomType.GetField('VolumeVersion',$flags).GetValue($room)
foreach ($fixture in @(
'{"evtName":"OnDataToUI","iddevice":340,"data":{"OutputStatus":{"OutputID":4004,"IsVideoBinding":false}}}',
'{"evtName":"OnDataToUI","iddevice":340,"data":{"OutputStatus":{"OutputID":4003,"IsVideoBinding":true}}}',
'{"evtName":"OnDataToUI","iddevice":339,"data":{"OutputStatus":{"OutputID":4003,"IsVideoBinding":false}}}')) { Send-AudioFixture $fixture }
if ($roomType.GetField('VolumeVersion',$flags).GetValue($room) -ne $version) { throw 'Wrong output, video binding or physical driver accepted' }
$workType.GetField('Dirty',$flags).SetValue($work,$false)
Send-AudioFixture '{"evtName":"OnDataToUI","iddevice":340,"data":{"OutputStatus":{"OutputID":4003,"IsVideoBinding":false,"InputID":-1,"VolumeLevel":99,"MuteFlag":true}}}'
if (!$workType.GetField('Dirty',$flags).GetValue($work) -or $roomType.GetField('VolumeVersion',$flags).GetValue($room) -ne ($version+1) -or $roomType.GetField('Volume',$flags).GetValue($room) -ne 18 -or $roomType.GetField('Power',$flags).GetValue($room) -ne 1) { throw 'Matrix output reconciliation failed' }
$muteVersion = $roomType.GetField('MuteVersion',$flags).GetValue($room)
[void]$apply.Invoke($core,[object[]]@([ushort]13,$work,'IS_MUTED',[ushort]0,[ushort]0,$muteVersion))
$last = $script:audioResults[$script:audioResults.Count-1]
if ($last.Valid -ne 0 -or $last.MuteValid -ne 0 -or $last.VolumeValid -ne 1 -or $last.PowerValid -ne 1) { throw 'One failed field invalidated unrelated fields' }
$version = $roomType.GetField('VolumeVersion',$flags).GetValue($room)
$count = $script:audioResults.Count
Send-AudioFixture '{"evtName":"OnDataToUI","iddevice":682,"data":{"volume_level":66}}' 3
Send-AudioFixture '{"evtName":"OnDataToUI","iddevice":682,"data":{"volume_level":101,"mute":"yes"}}'
Send-AudioFixture '{"evtName":"devicecommand","iddevice":682,"data":{"volume_level":66}}'
if ($script:audioResults.Count -ne $count -or $roomType.GetField('VolumeVersion',$flags).GetValue($room) -ne $version) { throw 'Stale, malformed or non-state event accepted' }
# Equipment power remains only a hint; existing in-flight work absorbs it.
Send-AudioFixture '{"evtName":"OnDataToUI","iddevice":682,"data":{"powerstate":false}}'
if (!$workType.GetField('Dirty',$flags).GetValue($work) -or $roomType.GetField('Power',$flags).GetValue($room) -ne 1) { throw 'Device power was incorrectly assigned to room power' }
$queries.Clear()
$workType.GetField('Epoch',$flags).SetValue($work,2)
[void]$apply.Invoke($core,[object[]]@([ushort]13,$work,'POWER_STATE',[ushort]0,[ushort]1,0))
if ($roomType.GetField('Power',$flags).GetValue($room) -ne 1) { throw 'Old connection REST result accepted' }
$coreType.GetField('_online',$flags).SetValue($core,$false)
$second = [Activator]::CreateInstance($adapterType)
if ($second.Initialize([ushort]62010,[ushort]13,[ushort]682,[ushort]340,[ushort]4003) -ne 1) { throw 'Shared room registration rejected' }
$conflict = [Activator]::CreateInstance($adapterType)
if ($conflict.Initialize([ushort]62010,[ushort]13,[ushort]683,[ushort]340,[ushort]4003) -ne 0) { throw 'Conflicting feedback mapping accepted' }
if ($conflict.Initialize([ushort]62010,[ushort]13,[ushort]682,[ushort]340,[ushort]4004) -ne 0) { throw 'Conflicting matrix output accepted' }
if ($conflict.Initialize([ushort]62010,[ushort]212,[ushort]0,[ushort]340,[ushort]0) -ne 0) { throw 'Partial matrix mapping accepted' }
if ($roomType.GetField('References',$flags).GetValue($room) -ne 2) { throw 'Rejected registration removed another module' }
[void]$adapter.Initialize([ushort]62010,[ushort]13,[ushort]682,[ushort]340,[ushort]4003)
if ($roomType.GetField('References',$flags).GetValue($room) -ne 2) { throw 'Reinitialization leaked registration' }
[void]$coreType.GetMethod('StopRealtime',$flags).Invoke($core,@())
$last = $script:audioResults[$script:audioResults.Count-1]
if ($last.Valid -ne 0 -or $last.PowerValid -ne 0 -or $last.VolumeValid -ne 0 -or $last.MuteValid -ne 0) { throw 'Disconnect retained valid audio state' }
$event.RemoveEventHandler($adapter,$handler)
Write-Output 'Audio checks passed: variable validation, endpoint/room distinction, independent validity, stale read rejection, power hint semantics, shared/conflicting registration and disconnect invalidation.'


$build = $coreType.GetMethod('BuildAudioCommand',$staticFlags)
foreach ($case in @(
 @('SetVolume',0,'SET_VOLUME_LEVEL','LEVEL',0),
 @('SetVolume',100,'SET_VOLUME_LEVEL','LEVEL',100),
 @('MuteOn',0,'MUTE_ON','',0),
 @('MuteOff',0,'MUTE_OFF','',0),
 @('RoomOff',0,'ROOM_OFF','',0),
 @('SelectAudioSource',100002,'SELECT_AUDIO_DEVICE','deviceid',100002),
 @('SelectVideoSource',542,'SELECT_VIDEO_DEVICE','deviceid',542))) {
 $payload = $build.Invoke($null,[object[]]@($case[0],[int]$case[1])) | ConvertFrom-Json
 if ($payload.async -ne $true -or $payload.command -ne $case[2]) { throw 'Wrong room command envelope' }
 if ($case[3]) {
   if ($payload.tParams.($case[3]) -ne $case[4]) { throw 'Wrong command parameter name/value' }
 } elseif (@($payload.tParams.psobject.Properties).Count -ne 0) { throw 'Unexpected command parameters' }
}
foreach ($case in @(@('SetVolume',-1),@('SetVolume',101),@('SelectAudioSource',0),@('SelectVideoSource',-1),@('Unknown',0))) {
 $rejected=$false
 try { [void]$build.Invoke($null,[object[]]@($case[0],[int]$case[1])) } catch { $cause=$_.Exception; while($cause.InnerException){$cause=$cause.InnerException}; $rejected=$cause -is [ArgumentException] }
 if (!$rejected) { throw 'Invalid room command accepted' }
}
$coreType.GetField('_online',$flags).SetValue($core,$false)
$beforeBusy=$coreType.GetField('_busy',$flags).GetValue($core)
$adapter.SetVolume(25)
$adapter.MuteOn()
$adapter.MuteOff()
$adapter.RoomOff()
$adapter.SelectAudioSource('100002')
$adapter.SelectVideoSource('542')
$adapter.SelectAudioSource('not-an-id')
if ($coreType.GetField('_busy',$flags).GetValue($core) -ne $beforeBusy) { throw 'Offline command was queued for later replay' }
Write-Output 'Audio control checks passed: verified command payloads, source IDs above 65535, invalid argument rejection and offline no-queue behavior.'

