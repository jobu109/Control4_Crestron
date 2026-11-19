$ErrorActionPreference='Stop'
$release=Join-Path $PSScriptRoot '../Control4Gateway/bin/Release'
Get-ChildItem $release -Filter '*.dll' | ForEach-Object {[void][Reflection.Assembly]::LoadFrom($_.FullName)}
$library=[Reflection.Assembly]::LoadFrom((Join-Path $release 'Control4Gateway.dll'))
$videoType=$library.GetType('Control4Crestron.Control4_Video',$true)
$audioType=$library.GetType('Control4Crestron.Control4_Audio',$true)
$coreType=$library.GetType('Control4Crestron.GatewayCore',$true)
$registry=$library.GetType('Control4Crestron.GatewayRegistry',$true)
$flags=[Reflection.BindingFlags]'NonPublic,Instance'
$audio=[Activator]::CreateInstance($audioType)
$video=[Activator]::CreateInstance($videoType)
if($audio.Initialize(62011,13,682,340,4003) -ne 1 -or $video.Initialize(62011,13,682,340,4003) -ne 1){throw 'Shared Audio/Video registration failed'}
$core=$registry.GetMethod('Get').Invoke($null,[object[]]@([ushort]62011))
$rooms=$coreType.GetField('_audioRooms',$flags).GetValue($core)
$room=$rooms[[ushort]13]
$roomType=$room.GetType()
if($roomType.GetField('References',$flags).GetValue($room) -ne 2){throw 'Video did not share room registration'}
[void]$video.Initialize(62011,13,682,340,4003)
if($roomType.GetField('References',$flags).GetValue($room) -ne 2){throw 'Video reinitialization leaked registration'}
$script:videoResults=[Collections.Generic.List[object]]::new()
$event=$videoType.GetEvent('FeedbackChanged')
$handler=[Management.Automation.LanguagePrimitives]::ConvertTo([scriptblock]{param($sender,$feedback) $script:videoResults.Add($feedback)},$event.EventHandlerType)
$event.AddEventHandler($video,$handler)
$roomType.GetField('Power',$flags).SetValue($room,[ushort]1)
$roomType.GetField('PowerValid',$flags).SetValue($room,[ushort]1)
$core.PublishCurrentAudio(13)
if($script:videoResults.Count -ne 1 -or $script:videoResults[0].Power -ne 1 -or $script:videoResults[0].PowerValid -ne 1){throw 'Measured room power not forwarded'}
$other=[Activator]::CreateInstance($audioType)
[void]$other.Initialize(62011,14,0,0,0)
$core.PublishCurrentAudio(14)
if($script:videoResults.Count -ne 1){throw 'Video received another room state'}
$conflict=[Activator]::CreateInstance($videoType)
if($conflict.Initialize(62011,13,683,340,4003) -ne 0){throw 'Conflicting video mapping accepted'}
if($roomType.GetField('References',$flags).GetValue($room) -ne 2){throw 'Conflict damaged shared Audio room'}
$video.SelectSource('542')
$video.RoomOff()
if($coreType.GetField('_busy',$flags).GetValue($core) -ne 0){throw 'Offline video command queued'}
$coreType.GetMethod('StopRealtime',$flags).Invoke($core,@())
if($script:videoResults[$script:videoResults.Count-1].PowerValid -ne 0){throw 'Disconnect retained valid video power'}
$event.RemoveEventHandler($video,$handler)
Write-Output 'Video checks passed: shared Audio/Video registration, reinitialization, measured power forwarding, room isolation, conflict rejection, offline no-queue and disconnect invalidation.'
