$ErrorActionPreference='Stop'
$release=Join-Path $PSScriptRoot '../Control4Gateway/bin/Release'
Get-ChildItem $release -Filter '*.dll' | ForEach-Object {[void][Reflection.Assembly]::LoadFrom($_.FullName)}
$assembly=[Reflection.Assembly]::LoadFrom((Join-Path $release 'Control4Gateway.dll'))
$type=$assembly.GetType('Control4Crestron.GatewayCore',$true)
$flags=[Reflection.BindingFlags]'NonPublic,Static';$instance=[Reflection.BindingFlags]'NonPublic,Instance'
$parse=$type.GetMethod('ParsePlayingQueue',$flags)
$jtoken=$parse.GetParameters()[0].ParameterType
$jparse=$jtoken.GetMethod('Parse',[type[]]@([string]))
function Read-Queue($json){$args2=[object[]]@($jparse.Invoke($null,[object[]]@([string]$json)),0);return ,$parse.Invoke($null,$args2)}
$pandora='{"iddevice":1648,"evtName":"OnDataToUI","data":{"EVENT":{"NAME":"QueueChanged","ARGS":{"NowPlayingIndex":1,"List":{"item":[{"isHeader":true,"title":"Playing From"},{"title":"Station name","image":"https://content-images.p-cdn.com/station.jpg"},{"isHeader":true,"title":"On Now"},{"title":"Amish Paradise","artist":"Weird Al","album":"Bad Hair Day","image":"https://content-images.p-cdn.com/current.jpg"},{"isHeader":true,"title":"You Just Heard"},{"title":"Advertisement","image":"https://content-images.p-cdn.com/previous.jpg"}]}}}}}'
$current=Read-Queue $pandora
if($current['station'].ToString() -ne 'Station name'){throw 'Pandora Playing From label missing'}
$withoutStation=Read-Queue $pandora.Replace('"title":"Playing From"','"title":"Unknown section"')
if($withoutStation['station'].ToString() -ne ''){throw 'Unknown section used as station'}
$ambiguousStation=Read-Queue $pandora.Replace('"title":"You Just Heard"','"title":"Playing From"')
if($ambiguousStation['station'].ToString() -ne '' -or $ambiguousStation['title'].ToString() -ne 'Amish Paradise'){throw 'Ambiguous station affected current song'}
if($current['title'].ToString() -ne 'Amish Paradise' -or $current['artwork'].ToString() -ne 'https://content-images.p-cdn.com/current.jpg' -or $current['source_id'].ToString() -ne '1648'){throw 'Pandora selected station/history rather than On Now'}
$ad=$pandora.Replace('"title":"Amish Paradise","artist":"Weird Al","album":"Bad Hair Day","image":"https://content-images.p-cdn.com/current.jpg"','"title":"Advertisement"')
$current=Read-Queue $ad
if($current['title'].ToString() -ne 'Advertisement' -or $current['artist'].ToString() -ne '' -or $current['album'].ToString() -ne '' -or $current['artwork'].ToString() -ne ''){throw 'Advertisement retained previous song metadata'}
foreach($bad in @($pandora.Replace('"isHeader":true,"title":"On Now"','"isHeader":false,"title":"On Now"'),$pandora.Replace('"isHeader":true,"title":"You Just Heard"','"title":"Extra song"'),$pandora.Replace('"isHeader":true,"title":"You Just Heard"','"isHeader":true,"title":"On Now"'))){if($null -ne (Read-Queue $bad)){throw 'Ambiguous Pandora current section accepted'}}
$json='{"iddevice":65,"evtName":"OnDataToUI","data":{"EVENT":{"NAME":"QueueChanged","ARGS":{"NowPlayingIndex":1,"List":{"item":[{"track":"HEAVY","isHeader":true},{"track":"Circle The Drain","artist":"Machine Head","album":"Circle The Drain","current":true,"album_cover_url":"https://i.scdn.co/image/123abc"},{"track":"White Wine Spritzer","artist":"Okilly Dokilly"}]}}}}}'
$first=Read-Queue $json
if($first['station'].ToString() -ne 'HEAVY'){throw 'Spotify preset header missing'}
if((Read-Queue $json.Replace('"track":"HEAVY","isHeader":true','"track":"HEAVY"'))['station'].ToString() -ne ''){throw 'Non-header Spotify row used as preset'}
if($first['title'].ToString() -ne 'Circle The Drain' -or $first['source_id'].ToString() -ne '65' -or $first['artwork'].ToString() -ne 'https://i.scdn.co/image/123abc'){throw 'Current queue row not normalized'}
$next=$json.Replace('"NowPlayingIndex":1','"NowPlayingIndex":2').Replace('"current":true,','')
if((Read-Queue $next)['title'].ToString() -ne 'White Wine Spritzer'){throw 'Index changed without updating current song'}
foreach($bad in @($json.Replace('"NowPlayingIndex":1','"NowPlayingIndex":0'),$json.Replace('"NowPlayingIndex":1','"NowPlayingIndex":99'),$json.Replace('"NowPlayingIndex":1','"NowPlayingIndex":2'),$json.Replace('"QueueChanged"','"DashboardChanged"'))){if($null -ne (Read-Queue $bad)){throw 'Ambiguous/header/out-of-range/other event accepted'}}
$art=$type.GetMethod('PlayingArtwork',$flags)
foreach($url in @('http://i.scdn.co/image/x','https://i.scdn.co/image/x?token=secret','https://user:pass@i.scdn.co/x','javascript:alert(1)','/controller/image','https://i.scdn.co/auth/x')){$artArgs=[object[]]::new(1);$artArgs[0]=$jparse.Invoke($null,[object[]]@([string]($url|ConvertTo-Json -Compress)));if($art.Invoke($null,$artArgs) -ne ''){throw 'Unsupported/private artwork accepted'}}
$room=$type.GetMethod('ParsePlayingRoom',$flags)
$roomJson='[{"varName":"CURRENT MEDIA INFO","value":{"mediainfo":{"title":"Circle The Drain","artist":"Machine Head","medSrcDev":65,"deviceid":100002,"img":"unknown:reference"}}}]'
if($room.Invoke($null,[object[]]@($roomJson,65))['title'].ToString() -ne 'Circle The Drain'){throw 'Measured room metadata missing'}
if($null -ne $room.Invoke($null,[object[]]@($roomJson,1648))){throw 'Other service metadata accepted'}
$moduleType=$assembly.GetType('Control4Crestron.Control4_Room',$true)
$module=[Activator]::CreateInstance($moduleType);[void]$module.Initialize(62041,13)
$core=$assembly.GetType('Control4Crestron.GatewayRegistry').GetMethod('Get').Invoke($null,[object[]]@([ushort]62041))
$argsType=$assembly.GetType('Control4Crestron.RoomCatalogEventArgs');$args3=[Activator]::CreateInstance($argsType);$args3.RoomId=13;$args3.Valid=1
$first['room_id']=$jparse.Invoke($null,[object[]]@('13'));$args3.Json=$first.ToString()
$events=[System.Collections.Generic.List[object]]::new();$module.add_NowPlayingChanged({param($s,$e) $events.Add($e)})
$moduleType.GetMethod('OnNowPlaying',$instance).Invoke($module,[object[]]@($core,$args3))
$frame=$events[$events.Count-1];$decoded='';$count=[int]$frame.Pages
for($i=1;$i -le $count;$i++){if($i -gt 1){$module.SetNowPlayingPage([ushort]$i);$frame=$events[$events.Count-1]};if($frame.Json.Length -gt 255){throw 'Metadata packet truncated'};$packet=$frame.Json|ConvertFrom-Json;if($packet.k -ne 'n'){throw 'Wrong metadata stream'};$decoded+=$packet.d}
$document=[Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($decoded))|ConvertFrom-Json
if($document.title -ne 'Circle The Drain' -or $document.artwork -ne 'https://i.scdn.co/image/123abc'){throw 'Metadata framing lost data'}
$args3.RoomId=99;$args3.Json='{}';$before=$events.Count;[void]$moduleType.GetMethod('OnNowPlaying',$instance).Invoke($module,[object[]]@($core,$args3));if($events.Count -ne $before){throw 'Other room reached wrapper'}
$type.GetField('_online',$instance).SetValue($core,$true);$type.GetField('_generation',$instance).SetValue($core,7);$type.GetField('_feedbackEpoch',$instance).SetValue($core,3)
$route=$type.GetMethod('RoutePlayingEvent',$instance)
$routeArgs=[object[]]@($jparse.Invoke($null,[object[]]@($json)),7);[void]$route.Invoke($core,$routeArgs)
$stateType=$assembly.GetType('Control4Crestron.RoomActiveSourceEventArgs');$state=[Activator]::CreateInstance($stateType);$state.RoomId=13;$state.AudioValid=1;$state.AudioId=65
$update=$type.GetMethod('UpdatePlayingSource',$instance);[void]$update.Invoke($core,[object[]]@([ushort]13,$state))
if($events[$events.Count-1].Valid -ne 1){throw 'Initial measured source did not adopt fresh queue event'}
$playing=$type.GetField('_playingRooms',$instance).GetValue($core)[[ushort]13]
$before=$events.Count;$wrong=[object[]]@($jparse.Invoke($null,[object[]]@($next)),6);[void]$route.Invoke($core,$wrong)
if($events.Count -ne $before){throw 'Old WebSocket generation accepted'}
$wrong=[object[]]@($jparse.Invoke($null,[object[]]@($next.Replace('"iddevice":65','"iddevice":1648'))),7);[void]$route.Invoke($core,$wrong)
if($events.Count -ne $before){throw 'Other service reached active room'}
$valid=[object[]]@($jparse.Invoke($null,[object[]]@($next)),7);[void]$route.Invoke($core,$valid)
if($events[$events.Count-1].Valid -ne 1 -or $events.Count -le $before){throw 'Track event did not reach room'}
$before=$events.Count
$indexOnly='{"iddevice":65,"evtName":"OnDataToUI","data":{"EVENT":{"NAME":"QueueChanged","ARGS":{"NowPlayingIndex":2,"NowPlaying":"notification"}}}}'
$delta=[object[]]@($jparse.Invoke($null,[object[]]@($indexOnly)),7);[void]$route.Invoke($core,$delta)
if($events.Count -ne $before -or $events[$events.Count-1].Valid -ne 1){throw 'Same-index notification cleared complete track'}
$changed=[object[]]@($jparse.Invoke($null,[object[]]@($indexOnly.Replace('"NowPlayingIndex":2','"NowPlayingIndex":3'))),7);[void]$route.Invoke($core,$changed)
if($events[$events.Count-1].Valid -ne 0){throw 'Changed-index notification retained stale track'}
[void]$route.Invoke($core,$valid)
$before=$events.Count
$malformedDelta=[object[]]@($jparse.Invoke($null,[object[]]@($indexOnly.Replace('"NowPlaying":"notification"','"NowPlaying":"notification","List":{}'))),7);[void]$route.Invoke($core,$malformedDelta)
if($events.Count -le $before -or $events[$events.Count-1].Valid -ne 0){throw 'Malformed full list treated as index notification'}
$bad=[object[]]@($jparse.Invoke($null,[object[]]@($json.Replace('"NowPlayingIndex":1','"NowPlayingIndex":99'))),7);[void]$route.Invoke($core,$bad)
if($events[$events.Count-1].Valid -ne 0){throw 'Incomplete queue change left stale song valid'}
$pandoraEvent=[object[]]@($jparse.Invoke($null,[object[]]@($pandora)),7);[void]$route.Invoke($core,$pandoraEvent)
$state.AudioId=1648;[void]$update.Invoke($core,[object[]]@([ushort]13,$state))
$before=$events.Count
$pandoraDelta=[object[]]@($jparse.Invoke($null,[object[]]@($indexOnly.Replace('"iddevice":65','"iddevice":1648').Replace('"NowPlayingIndex":2','"NowPlayingIndex":1'))),7);[void]$route.Invoke($core,$pandoraDelta)
if($events.Count -ne $before -or $events[$events.Count-1].Valid -ne 1){throw 'Pandora full queue followed by index1 cleared metadata'}
$pandoraNext=[object[]]@($jparse.Invoke($null,[object[]]@($pandora.Replace('Amish Paradise','My Hero').Replace('Weird Al','Foo Fighters'))),7);[void]$route.Invoke($core,$pandoraNext)
$newState=$type.GetField('_playingRooms',$instance).GetValue($core)[[ushort]13]
if($newState.GetType().GetField('Data',$instance).GetValue($newState)['title'].ToString() -ne 'My Hero'){throw 'Pandora same-index full queue did not replace current song'}
$before=$events.Count;[void]$route.Invoke($core,$pandoraDelta)
if($events.Count -ne $before){throw 'Pandora follow-up notification republished/cleared updated song'}
$dashboard='{"iddevice":1648,"evtName":"OnDataToUI","data":{"EVENT":{"NAME":"DashboardChanged","ARGS":{"Items":"pause"}}}}'
$dash=[object[]]@($jparse.Invoke($null,[object[]]@($dashboard)),7);[void]$route.Invoke($core,$dash)
if($newState.GetType().GetField('Action',$instance).GetValue($newState) -ne 'pause'){throw 'Reported Pause action not accepted'}
if($newState.GetType().GetField('Data',$instance).GetValue($newState)['title'].ToString() -ne 'My Hero'){throw 'Dashboard update replaced track data'}
$dash=[object[]]@($jparse.Invoke($null,[object[]]@($dashboard.Replace('"Items":"pause"','"Items":"play"'))),6);[void]$route.Invoke($core,$dash)
if($newState.GetType().GetField('Action',$instance).GetValue($newState) -ne 'pause'){throw 'Old-session dashboard applied'}
$dash=[object[]]@($jparse.Invoke($null,[object[]]@($dashboard.Replace('"Items":"pause"','"Items":"play,pause"'))),7);[void]$route.Invoke($core,$dash)
if($newState.GetType().GetField('Action',$instance).GetValue($newState) -ne 'unknown'){throw 'Ambiguous dashboard guessed a state'}
$dash=[object[]]@($jparse.Invoke($null,[object[]]@($dashboard.Replace('"Items":"pause"','"Items":"play"'))),7);[void]$route.Invoke($core,$dash)
if($newState.GetType().GetField('Action',$instance).GetValue($newState) -ne 'play'){throw 'Reported Play action not accepted'}
$progressEvents=[System.Collections.Generic.List[object]]::new();$core.add_RoomProgressChanged({param($s,$e) $progressEvents.Add($e)})
$progressJson='{"iddevice":1648,"evtName":"OnDataToUI","data":{"EVENT":{"NAME":"ProgressChanged","ARGS":{"length":235,"offset":11,"canSeek":true}}}}'
$progressEvent=[object[]]@($jparse.Invoke($null,[object[]]@($progressJson)),7);[void]$route.Invoke($core,$progressEvent)
$p=$progressEvents[$progressEvents.Count-1].Json|ConvertFrom-Json
if(!$p.valid -or $p.position_seconds -ne 11 -or $p.duration_seconds -ne 235 -or $p.track_revision.Length -ne 16 -or $progressEvents[$progressEvents.Count-1].Json.Length -gt 255){throw 'Progress packet incorrect or oversized'}
$count=$progressEvents.Count;[void]$route.Invoke($core,$progressEvent);if($progressEvents.Count -ne $count){throw 'Identical progress republished'}
$wrong=[object[]]@($jparse.Invoke($null,[object[]]@($progressJson.Replace('"iddevice":1648','"iddevice":65'))),7);[void]$route.Invoke($core,$wrong);if($progressEvents.Count -ne $count){throw 'Other source progress applied'}
$old=[object[]]@($jparse.Invoke($null,[object[]]@($progressJson.Replace('"offset":11','"offset":12'))),6);[void]$route.Invoke($core,$old);if($progressEvents.Count -ne $count){throw 'Old generation progress applied'}
$badProgress=[object[]]@($jparse.Invoke($null,[object[]]@($progressJson.Replace('"offset":11','"offset":236'))),7);[void]$route.Invoke($core,$badProgress)
if(($progressEvents[$progressEvents.Count-1].Json|ConvertFrom-Json).valid){throw 'Out of range progress retained'}
[void]$route.Invoke($core,$progressEvent);$priorRevision=($progressEvents[$progressEvents.Count-1].Json|ConvertFrom-Json).track_revision
$newSong=[object[]]@($jparse.Invoke($null,[object[]]@($pandora.Replace('Amish Paradise','New song'))),7);[void]$route.Invoke($core,$newSong)
$p=$progressEvents[$progressEvents.Count-1].Json|ConvertFrom-Json
if($p.valid -or $p.track_revision -eq $priorRevision){throw 'New song retained old progress identity'}
$state.AudioId=0;[void]$update.Invoke($core,[object[]]@([ushort]13,$state))
if(($progressEvents[$progressEvents.Count-1].Json|ConvertFrom-Json).valid){throw 'Room Off retained progress'}
if($events[$events.Count-1].Valid -ne 0){throw 'Room Off did not clear metadata'}
if($type.GetMethod('PlayingCurrent',$instance).Invoke($core,[object[]]@([ushort]13,$playing))){throw 'Superseded source work remains current'}
[void]$type.GetMethod('InvalidatePlaying',$instance).Invoke($core,@())
if($type.GetField('_playingQueues',$instance).GetValue($core).Count -ne 0 -or $type.GetField('_playingRooms',$instance).GetValue($core).Count -ne 0){throw 'Disconnect did not clear cached metadata'}
'Now-playing checks passed: current queue index, source/generation guards, clearing, safe artwork, room isolation and lossless <=255-character frames.'

# Switching back after the queue freshness window: a measured room track
# may recover cached artwork only when its source and track identity match.
$restore=$type.GetMethod('RestorePlayingArtwork',$flags)
$queue=Read-Queue $json
$measured='{"valid":true,"source_id":65,"title":"Circle The Drain","artist":"Machine Head","album":"Circle The Drain","artwork":""}'
foreach($case in @(
 @{json=$measured; art='https://i.scdn.co/image/123abc'},
 @{json=$measured.Replace('Machine Head','Someone Else'); art=''},
 @{json=$measured.Replace('"source_id":65','"source_id":1648'); art=''},
 @{json=$measured.Replace('"album":"Circle The Drain"','"album":"Live Version"'); art=''}
)){
 $track=$jparse.Invoke($null,[object[]]@($case.json))
 $restore.Invoke($null,[object[]]@($track,$queue))
 if($track['artwork'].ToString() -ne $case.art){throw 'Room-switch artwork identity check failed'}
}
Write-Output 'Room-switch artwork cache matching passed.'