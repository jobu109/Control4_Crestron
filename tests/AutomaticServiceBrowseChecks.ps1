$ErrorActionPreference='Stop'
$release=Join-Path $PSScriptRoot '../Control4Gateway/bin/Release'
Get-ChildItem $release -Filter '*.dll' | ForEach-Object {[void][Reflection.Assembly]::LoadFrom($_.FullName)}
$library=[Reflection.Assembly]::LoadFrom((Join-Path $release 'Control4Gateway.dll'))
$roomType=$library.GetType('Control4Crestron.Control4_Room',$true)
$coreType=$library.GetType('Control4Crestron.GatewayCore',$true)
$stateType=$library.GetType('Control4Crestron.RoomActiveSourceEventArgs',$true)
$room=[Activator]::CreateInstance($roomType)
[void]$room.Initialize(62033,13)
$core=$library.GetType('Control4Crestron.GatewayRegistry').GetMethod('Get').Invoke($null,[object[]]@([ushort]62033))
$instance=[Reflection.BindingFlags]'Instance,NonPublic'
$onSource=$roomType.GetMethod('OnActiveSource',$instance)
$script:diagnostics=[Collections.Generic.List[string]]::new()
$event=$roomType.GetEvent('DiagnosticReceived')
$handler=[Management.Automation.LanguagePrimitives]::ConvertTo([scriptblock]{param($sender,$feedback) $script:diagnostics.Add($feedback.Message)},$event.EventHandlerType)
$event.AddEventHandler($room,$handler)
function Send-State($roomId,$sourceId,$valid){
 $state=[Activator]::CreateInstance($stateType); $state.RoomId=[ushort]$roomId; $state.AudioId=$sourceId; $state.AudioValid=[ushort]$valid
 [void]$onSource.Invoke($room,[object[]]@($core,$state))
}
Send-State 13 65 1
$first=@($script:diagnostics | Where-Object {$_ -like 'Connect and initialize Sources before service browsing*'}).Count
if($first -ne 1){throw 'Initial valid source did not trigger browse'}
Send-State 13 65 1
Send-State 13 0 0
Send-State 13 65 1
Send-State 14 66 1
if(@($script:diagnostics | Where-Object {$_ -like 'Connect and initialize Sources before service browsing*'}).Count -ne 1){throw 'Duplicate/transient/other-room state triggered extra browse'}
Send-State 13 66 1
if(@($script:diagnostics | Where-Object {$_ -like 'Connect and initialize Sources before service browsing*'}).Count -ne 2){throw 'Changed source did not trigger browse'}
Send-State 13 0 1
if($roomType.GetField('_mediaValid',$instance).GetValue($room)){throw 'Room Off retained list validity'}
[void]$coreType.GetMethod('InvalidateRoomCatalogs',$instance).Invoke($core,@())
Send-State 13 66 1
if(@($script:diagnostics | Where-Object {$_ -like 'Connect and initialize Sources before service browsing*'}).Count -ne 3){throw 'Reconnect invalidation did not allow same source browse'}
if($coreType.GetField('_busy',$instance).GetValue($core) -ne 0){throw 'Offline event test queued network work'}
$event.RemoveEventHandler($room,$handler)
'Automatic browse checks passed: measured changes, duplicate/transient/other-room suppression, Off clearing and reconnect re-arming.'
