$ErrorActionPreference='Stop'
$release=Join-Path $PSScriptRoot '../Control4Gateway/bin/Release'
Get-ChildItem $release -Filter '*.dll' | ForEach-Object {[void][Reflection.Assembly]::LoadFrom($_.FullName)}
$library=[Reflection.Assembly]::LoadFrom((Join-Path $release 'Control4Gateway.dll'))
$coreType=$library.GetType('Control4Crestron.GatewayCore',$true)
$registry=$library.GetType('Control4Crestron.GatewayRegistry',$true)
$roomType=$library.GetType('Control4Crestron.Control4_Room',$true)
$flags=[Reflection.BindingFlags]'NonPublic,Instance'
foreach($id in 13,14,15){
 $module=[Activator]::CreateInstance($roomType)
 if($module.Initialize(62014,$id) -ne 1){throw 'Room initialization failed'}
}
$mapped=[Activator]::CreateInstance($library.GetType('Control4Crestron.Control4_Audio',$true))
[void]$mapped.Initialize(62014,15,682,340,4003)
$core=$registry.GetMethod('Get').Invoke($null,[object[]]@([ushort]62014))
if($coreType.GetField('_roomTimer',$flags) -or $coreType.GetMethod('PollRoomModules',$flags)){throw 'Periodic room polling still exists'}
$coreType.GetField('_generation',$flags).SetValue($core,4)
$coreType.GetField('_feedbackEpoch',$flags).SetValue($core,3)
$coreType.GetField('_online',$flags).SetValue($core,$true)
$rooms=$coreType.GetField('_audioRooms',$flags).GetValue($core)
$queries=$coreType.GetField('_audioQueries',$flags).GetValue($core)
$workType=$coreType.GetNestedType('AudioQueryWork',[Reflection.BindingFlags]'NonPublic')
foreach($id in 13,14,15){
 $work=[Activator]::CreateInstance($workType,$true)
 $workType.GetField('Generation',$flags).SetValue($work,4)
 $workType.GetField('Epoch',$flags).SetValue($work,3)
 $workType.GetField('Configuration',$flags).SetValue($work,$rooms[[ushort]$id])
 $queries.Add([ushort]$id,$work)
 $rooms[[ushort]$id].GetType().GetField('Volume',$flags).SetValue($rooms[[ushort]$id],[ushort]20)
}
$assembly=[Reflection.Assembly]::LoadFrom((Join-Path $release 'SimplSharpNewtonsoft.dll'))
$parse=$assembly.GetType('Newtonsoft.Json.Linq.JToken').GetMethod('Parse',[type[]]@([string]))
$route=$coreType.GetMethod('RouteRealtimeAudio',$flags)
function Send-Hint([string]$Json,[int]$Generation=4){
 $packet=$parse.Invoke($null,[object[]]@($Json))
 [void]$route.Invoke($core,[object[]]@($packet,$Generation))
}
function Reset-Dirty {foreach($id in 13,14,15){$workType.GetField('Dirty',$flags).SetValue($queries[[ushort]$id],$false)}}
function Is-Dirty([int]$Id){$workType.GetField('Dirty',$flags).GetValue($queries[[ushort]$Id])}
$volume='{"evtName":"OnDataToUI","iddevice":682,"data":{"volume_level":35,"title":"DO_NOT_CACHE"}}'
Send-Hint $volume
if(!(Is-Dirty 13) -or !(Is-Dirty 14) -or !(Is-Dirty 15)){throw 'Mapped/unmapped event refresh was not queued'}
foreach($id in 13,14,15){if($rooms[[ushort]$id].GetType().GetField('Volume',$flags).GetValue($rooms[[ushort]$id]) -ne 20){throw 'Endpoint value assigned to a room'}}
Reset-Dirty
Send-Hint $volume
if((Is-Dirty 13) -or (Is-Dirty 14)){throw 'Repeated identical AV state caused unmapped room reads'}
Reset-Dirty
Send-Hint '{"evtName":"OnDataToUI","iddevice":7,"data":{"active_state_changed":{"room_id":14,"type":"listen","active":true}}}'
if((Is-Dirty 13) -or !(Is-Dirty 14) -or (Is-Dirty 15)){throw 'Agent activity was not room-scoped'}
Reset-Dirty
Send-Hint '{"evtName":"OnDataToUI","iddevice":7,"data":{"active_state_changed":{"room_id":99,"type":"watch","active":false}}}'
Send-Hint '{"evtName":"devicecommand","iddevice":682,"data":{"volume_level":66}}'
Send-Hint '{"evtName":"OnDataToUI","iddevice":340,"data":{"OutputStatus":{"OutputID":4003,"IsVideoBinding":true,"VolumeLevel":66}}}'
Send-Hint '{"evtName":"OnDataToUI","iddevice":682,"data":{"volume_level":90}}' 3
if((Is-Dirty 13) -or (Is-Dirty 14) -or (Is-Dirty 15)){throw 'Unknown room, non-state, video binding or old session accepted'}
Send-Hint '{"evtName":"OnDataToUI","iddevice":340,"data":{"OutputStatus":{"OutputID":4003,"IsVideoBinding":false,"VolumeLevel":42,"MuteFlag":true,"InputID":3001}}}'
if(!(Is-Dirty 13) -or !(Is-Dirty 14)){throw 'Matrix state did not refresh unmapped rooms'}
Reset-Dirty
Send-Hint '{"evtName":"OnDataToUI","iddevice":340,"data":{"OutputStatus":{"OutputID":4003,"IsVideoBinding":false,"VolumeLevel":42,"MuteFlag":true,"InputID":3001}}}'
if((Is-Dirty 13) -or (Is-Dirty 14)){throw 'Repeated identical matrix state caused reads'}
$hints=$coreType.GetField('_roomAvHints',$flags).GetValue($core)
if(($hints.Values -join '') -match 'DO_NOT_CACHE|title'){throw 'Raw media content entered hint cache'}
$coreType.GetMethod('StopRealtime',$flags).Invoke($core,@())
if($hints.Count -ne 0){throw 'Disconnect retained hint cache'}
Write-Output 'Event-driven Room checks passed: no timer, event-only readback, identical-state suppression, scoped room activity, mapped/unmapped behavior, no cross-room value assignment, filtering and disconnect cleanup.'
