$ErrorActionPreference = 'Stop'
$release = Join-Path $PSScriptRoot '../Control4Gateway/bin/Release'
Get-ChildItem $release -Filter '*.dll' | ForEach-Object { [void][Reflection.Assembly]::LoadFrom($_.FullName) }
$library = [Reflection.Assembly]::LoadFrom((Join-Path $release 'Control4Gateway.dll'))
$type = $library.GetType('Control4Crestron.GatewayCore',$true)
$core = [Activator]::CreateInstance($type,[object[]]@([ushort]1))
$flags = [Reflection.BindingFlags]'NonPublic,Instance'
$snapshot = $type.GetMethod('AudioFeedbackCaptureReport',$flags)
$route = $type.GetMethod('RouteRealtimeMessage',$flags)
$jsonAssembly = [Reflection.Assembly]::LoadFrom((Join-Path $release 'SimplSharpNewtonsoft.dll'))
$parse = $jsonAssembly.GetType('Newtonsoft.Json.Linq.JToken').GetMethod('Parse',[type[]]@([string]))
if ($null -ne $snapshot.Invoke($core,@())) { throw 'Capture enabled at startup' }
$type.GetField('_online',$flags).SetValue($core,$true)
$type.GetField('_generation',$flags).SetValue($core,4)
$fixture = '{"evtName":"OnDataToUI","iddevice":340,"data":{"output_id":2001,"volume_level":18,"mute":true,"metadata":"DO_NOT_EXPORT_TITLE","password":"DO_NOT_EXPORT_PASSWORD","OutputStatus":{"OutputID":2000,"InputID":1001,"IsVideoBinding":false,"MuteFlag":true}}}'
$message = $parse.Invoke($null,[object[]]@($fixture))
[void]$route.Invoke($core,[object[]]@($message,4))
if ($null -ne $snapshot.Invoke($core,@())) { throw 'Event implicitly started capture' }
$core.StartAudioFeedbackCapture()
[void]$route.Invoke($core,[object[]]@($message,4))
$report = $snapshot.Invoke($core,@())
$serialized = $report.ToString()
$data = $serialized | ConvertFrom-Json
if ($serialized -match 'DO_NOT_EXPORT|password' -or $data.event_count -ne 1 -or $data.events[0].data.values.OutputStatus.values.MuteFlag -ne $true -or $data.events[0].data.values.output_id -ne 2001 -or $data.events[0].data.values.OutputStatus.values.InputID -ne 1001) { throw 'Capture filtering or channel metadata failed' }
foreach ($case in @(@('{"evtName":"OnDataToUI","iddevice":999,"data":{"volume_level":1}}',4),@($fixture,3),@('{"evtName":"OnDataToUI","iddevice":7,"data":{"active_state_changed":{"room_id":214,"type":"watch","active":true}}}',4))) {
    $event = $parse.Invoke($null,[object[]]@($case[0]))
    [void]$route.Invoke($core,[object[]]@($event,[int]$case[1]))
}
if (($snapshot.Invoke($core,@()).ToString() | ConvertFrom-Json).event_count -ne 1) { throw 'Other device, stale session or other room event captured' }
$event = $parse.Invoke($null,[object[]]@('{"evtName":"OnDataToUI","iddevice":7,"data":{"active_state_changed":{"room_id":13,"type":"listen","active":true}}}'))
[void]$route.Invoke($core,[object[]]@($event,4))
$event = $parse.Invoke($null,[object[]]@('{"evtName":"devicecommand","iddevice":339,"data":"DO_NOT_EXPORT_RAW_COMMAND"}'))
[void]$route.Invoke($core,[object[]]@($event,4))
$report = $snapshot.Invoke($core,@())
if (($report.ToString() | ConvertFrom-Json).event_count -ne 3 -or $report.ToString() -match 'DO_NOT_EXPORT') { throw 'Activity correlation or string-payload suppression failed' }
$report['events'].RemoveAll()
if (($snapshot.Invoke($core,@()).ToString() | ConvertFrom-Json).event_count -ne 3) { throw 'Snapshot modified live capture buffer' }
$type.GetField('_audioCaptureEndUtc',$flags).SetValue($core,[DateTime]::UtcNow.AddSeconds(-1))
[void]$route.Invoke($core,[object[]]@($message,4))
$data = $snapshot.Invoke($core,@()).ToString() | ConvertFrom-Json
if ($data.event_count -ne 3 -or $data.capture_state -ne 'complete') { throw 'Capture time limit failed' }
$core.StartAudioFeedbackCapture()
for($i=0;$i -lt 300;$i++){ [void]$route.Invoke($core,[object[]]@($message,4)) }
$data = $snapshot.Invoke($core,@()).ToString() | ConvertFrom-Json
if ($data.event_count -gt 256 -or $data.dropped_event_count -le 0 -or $type.GetField('_audioCaptureBytes',$flags).GetValue($core) -gt 131072) { throw 'Capture buffer bound failed' }
$type.GetField('_generation',$flags).SetValue($core,5)
if (($snapshot.Invoke($core,@()).ToString() | ConvertFrom-Json).capture_state -ne 'cancelled_connection_changed') { throw 'Changed connection not reported' }
Write-Output 'Audio capture checks passed: explicit start, routing integration, privacy filters, endpoint/channel fields, room correlation, immutable snapshots, expiry, bounded buffer and connection cancellation.'

