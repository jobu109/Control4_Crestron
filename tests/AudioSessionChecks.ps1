$ErrorActionPreference='Stop'
# Checks GatewayCore.BuildPanelAudioSessions against the Digital Media variable shapes seen on
# the EA-5: QUEUE_STATUS_V2 "queues" is an object while something plays and a string when idle.
$release=Join-Path $PSScriptRoot '../Control4Gateway/bin/Release'
Get-ChildItem $release -Filter '*.dll' | ForEach-Object {[void][Reflection.Assembly]::LoadFrom($_.FullName)}
$library=[Reflection.Assembly]::LoadFrom((Join-Path $release 'Control4Gateway.dll'))
$type=$library.GetType('Control4Crestron.GatewayCore',$true)
$build=$type.GetMethod('BuildPanelAudioSessions',[Reflection.BindingFlags]'NonPublic,Static')
function Check($ok,$message){ if(-not $ok){ throw $message } }
function Sessions($queues){
  $settings='{"varName":"ROOM_QUEUE_SETTINGS","value":{"room_queue_settings":{"room_info":[{"roomid":13,"shuffle":0,"repeat":0},{"roomid":89,"shuffle":0,"repeat":0},{"roomid":212,"shuffle":0,"repeat":0}]}}}'
  $status='{"varName":"QUEUE_STATUS_V2","value":{"queues":' + $queues + '}}'
  $json='[' + $settings + ',' + $status + ']'
  return $build.Invoke($null,[object[]]@($json,[uint16[]]@(13,89))).ToString() | ConvertFrom-Json
}

$idle=Sessions '""'
Check ($idle.available -eq $true -and @($idle.sessions).Count -eq 0) 'Idle (empty string) queues must be available with no sessions'
Check ((@($idle.rooms | ForEach-Object { $_.id }) -join ',') -eq '13,89') "Idle rooms not limited to the panel scope: $(@($idle.rooms | ForEach-Object { $_.id }) -join ',')"

$playing=Sessions '{"queue":{"id":5,"owner":13,"device_id":1648,"state":"play","rooms":{"id":[13,212]}}}'
Check ($playing.available -eq $true -and @($playing.sessions).Count -eq 1 -and $playing.sessions[0].queue_id -eq 5) 'Playing queue not read'
Check ((@($playing.sessions[0].room_ids) -join ',') -eq '13' -and $playing.sessions[0].owner_room_id -eq 13) 'Queue members not limited to scope'

$text=Sessions '"{\"queue\":{\"id\":7,\"rooms\":{\"id\":89}}}"'
Check ($text.available -eq $true -and $text.sessions[0].queue_id -eq 7) 'JSON-text queues not parsed'

$junk=Sessions '"<queues/>"'
Check ($junk.available -eq $false) 'Unreadable queues must stay unavailable'
# Now playing summary: only reported fields; and the revision basis ignores now_playing.
$json=[Reflection.Assembly]::LoadFrom((Join-Path $release 'SimplSharpNewtonsoft.dll'))
$parse=$json.GetType('Newtonsoft.Json.Linq.JToken').GetMethod('Parse',[type[]]@([string]))
$summary=$type.GetMethod('SessionNowPlaying',[Reflection.BindingFlags]'NonPublic,Static')
$p=$parse.Invoke($null,[object[]]@('{"source_id":1648,"valid":true,"title":"Song A","artist":"Artist B","album":"","station":"Radio","artwork":"https://art.example/a.jpg"}'))
$sum=$summary.Invoke($null,[object[]]@(,$p)).ToString() | ConvertFrom-Json
Check ($sum.title -eq 'Song A' -and $sum.artist -eq 'Artist B' -and $null -eq $sum.album -and $sum.station -eq 'Radio' -and $null -eq $sum.source_id) 'Now playing summary fields wrong'
$invalid=$parse.Invoke($null,[object[]]@('{"valid":false,"title":"Old"}'))
Check ($null -eq $summary.Invoke($null,[object[]]@(,$invalid))) 'Invalid now playing must be left out'
$panel=$library.GetType('Control4Crestron.Control4_Panel_Room',$true)
$core=$panel.GetMethod('AudioSessionsCore',[Reflection.BindingFlags]'NonPublic,Static')
$a=$parse.Invoke($null,[object[]]@('{"rooms":[{"id":13,"muted":false}],"sessions":[{"queue_id":5,"room_ids":[13],"now_playing":{"title":"One"}}]}'))
$b=$parse.Invoke($null,[object[]]@('{"rooms":[{"id":13,"muted":false}],"sessions":[{"queue_id":5,"room_ids":[13],"now_playing":{"title":"Two"}}]}'))
$c=$parse.Invoke($null,[object[]]@('{"rooms":[{"id":13,"muted":true}],"sessions":[{"queue_id":5,"room_ids":[13],"now_playing":{"title":"Two"}}]}'))
Check ($core.Invoke($null,[object[]]@(,$a)) -eq $core.Invoke($null,[object[]]@(,$b))) 'A track change must not change the revision basis'
Check ($core.Invoke($null,[object[]]@(,$b)) -ne $core.Invoke($null,[object[]]@(,$c))) 'A mute change must change the revision basis'
'Audio session checks passed: now playing summary filtered, revision ignores track changes but not mute; idle string available with no sessions, playing object and JSON text read, unreadable text unavailable, scope applied'

$activeRooms=$type.GetMethod('ActivePanelAudioRooms',[Reflection.BindingFlags]'NonPublic,Static')
$multi=$parse.Invoke($null,[object[]]@('{"sessions":[{"queue_id":5,"room_ids":[13,89,300]},{"queue_id":7,"room_ids":[89,212]}]}'))
$targets=$activeRooms.Invoke($null,[object[]]@($multi,[ushort[]]@(13,89,212,212,99)))
Check (($targets -join ',') -eq '13,89,212') 'Batch Off must select every scoped active room once'
'Batch audio off scope and deduplication checks passed'
$planShutdown=$type.GetMethod('PlanPanelAudioShutdown',[Reflection.BindingFlags]'NonPublic,Static')
$queues=$parse.Invoke($null,[object[]]@('{"sessions":[{"queue_id":5,"owner_room_id":13,"room_ids":[13,89,300]},{"queue_id":7,"owner_room_id":500,"room_ids":[212,500]}]}'))
$plan=$planShutdown.Invoke($null,[object[]]@($queues,[ushort[]]@(13,89,212,99)))
Check ($plan.Count -eq 3 -and $plan[[ushort]13] -eq 0 -and $plan[[ushort]89] -eq 13 -and $plan[[ushort]212] -eq 500) 'Shutdown must remove joined rooms, turn off owners, and leave out-of-scope rooms alone'
'Batch owner and member shutdown plan checks passed'
