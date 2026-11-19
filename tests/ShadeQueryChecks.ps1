$ErrorActionPreference = 'Stop'
$release = Join-Path $PSScriptRoot '../Control4Gateway/bin/Release'
Get-ChildItem $release -Filter '*.dll' | ForEach-Object { [void][Reflection.Assembly]::LoadFrom($_.FullName) }
$library = [Reflection.Assembly]::LoadFrom((Join-Path $release 'Control4Gateway.dll'))
$coreType = $library.GetType('Control4Crestron.GatewayCore', $true)
$shadeType = $library.GetType('Control4Crestron.Control4_Shade', $true)
$registry = $library.GetType('Control4Crestron.GatewayRegistry', $true)
$adapter = [Activator]::CreateInstance($shadeType)
$adapter.Initialize([ushort]62000, [ushort]1001)
$core = $registry.GetMethod('Get').Invoke($null, [object[]]@([ushort]62000))
$event = $shadeType.GetEvent('FeedbackChanged')
$script:shadeResults = [Collections.Generic.List[object]]::new()
$handler = [Management.Automation.LanguagePrimitives]::ConvertTo([scriptblock]{ param($sender, $feedback) $script:shadeResults.Add($feedback) }, $event.EventHandlerType)
$event.AddEventHandler($adapter, $handler)
$diagnosticEvent = $shadeType.GetEvent('DiagnosticReceived')
$script:shadeDiagnostics = [Collections.Generic.List[string]]::new()
$diagnosticHandler = [Management.Automation.LanguagePrimitives]::ConvertTo([scriptblock]{ param($sender, $message) $script:shadeDiagnostics.Add($message.Message) }, $diagnosticEvent.EventHandlerType)
$diagnosticEvent.AddEventHandler($adapter, $diagnosticHandler)
$flags = [Reflection.BindingFlags]'NonPublic,Instance'
$publish = $coreType.GetMethod('PublishShade', $flags)
foreach ($case in @(@(0, 1, 0, 1), @(50, 1, 0, 0), @(100, 1, 1, 0), @(0, 0, 0, 0))) {
    [void]$publish.Invoke($core, [object[]]@([ushort]1001, [ushort]$case[0], [ushort]$case[1]))
    $actual = $script:shadeResults[$script:shadeResults.Count - 1]
    if ($actual.Position -ne $case[0] -or $actual.Valid -ne $case[1] -or $actual.FullyOpen -ne $case[2] -or $actual.FullyClosed -ne $case[3]) { throw 'Shade endpoint feedback mismatch' }
}
$before = $script:shadeResults.Count
[void]$publish.Invoke($core, [object[]]@([ushort]1002, [ushort]50, [ushort]1))
if ($script:shadeResults.Count -ne $before) { throw 'Another proxy leaked into shade feedback' }
$adapter.Initialize([ushort]62000, [ushort]1001)
[void]$coreType.GetMethod('EmitMessage', $flags).Invoke($core, [object[]]@('fixture shade diagnostic'))
if ($script:shadeDiagnostics.Count -ne 1 -or $script:shadeDiagnostics[0] -ne 'fixture shade diagnostic') { throw 'Shade diagnostic forwarding missing or duplicated after reinitialization' }
[void]$publish.Invoke($core, [object[]]@([ushort]1001, [ushort]50, [ushort]1))
if ($script:shadeResults.Count -ne $before + 1) { throw 'Repeated initialization duplicated event subscriptions' }
$positions = $coreType.GetField('_shadePositions', $flags).GetValue($core)
$positions[[ushort]1001] = [ushort]75
$core.PublishCurrentShade([ushort]1001)
if ($script:shadeResults[$script:shadeResults.Count - 1].Position -ne 75) { throw 'Cached shade position not published' }
[void]$coreType.GetMethod('StopRealtime', $flags).Invoke($core, @())
if ($positions.Count -ne 0 -or $script:shadeResults[$script:shadeResults.Count - 1].Valid -ne 0) { throw 'Disconnect did not invalidate shade feedback' }
$parser = $coreType.GetMethod('TryParseLightLevel', [Reflection.BindingFlags]'NonPublic,Static')
foreach ($case in @(@('[{"value":0}]', $true), @('[{"value":"100"}]', $true), @('[{"value":101}]', $false), @('[{"value":0.5}]', $false), @('[{"value":"Undefined"}]', $false), @('[]', $false))) {
    $arguments = [object[]]@($case[0], [ushort]0)
    if ($parser.Invoke($null, $arguments) -ne $case[1]) { throw ('Shade variable parser failed: ' + $case[0]) }
}
$event.RemoveEventHandler($adapter, $handler)
$diagnosticEvent.RemoveEventHandler($adapter, $diagnosticHandler)
Write-Output 'Shade checks passed: position/endpoint semantics, invalid-state flags, proxy filtering, reinitialization, cached feedback, disconnect invalidation and variable validation.'

$staticFlags = [Reflection.BindingFlags]'NonPublic,Static'
$buildCommand = $coreType.GetMethod('BuildShadeCommand', $staticFlags)
foreach ($case in @(@('Open','SET_LEVEL_TARGET:LEVEL_TARGET_OPEN',0), @('Close','SET_LEVEL_TARGET:LEVEL_TARGET_CLOSED',0), @('Stop','STOP',0), @('SetLevel','SET_LEVEL_TARGET',45), @('SetLevel','SET_LEVEL_TARGET',0), @('SetLevel','SET_LEVEL_TARGET',100))) {
    $body = $buildCommand.Invoke($null, [object[]]@($case[0], [ushort]$case[2])) | ConvertFrom-Json
    if ($body.command -ne $case[1] -or $body.async -ne $true -or $null -eq $body.tParams) { throw 'Invalid shade command envelope' }
    if ($case[0] -eq 'SetLevel' -and $body.tParams.LEVEL_TARGET -ne $case[2]) { throw 'Invalid shade target value' }
    if ($case[0] -ne 'SetLevel' -and @($body.tParams.PSObject.Properties).Count -ne 0) { throw 'Unexpected non-level parameters' }
}
foreach ($case in @(@('SetLevel',101), @('SetLevel',65535), @('Unsupported',0))) {
    $rejected = $false
    try { [void]$buildCommand.Invoke($null, [object[]]@($case[0], [ushort]$case[1])) } catch { $rejected = $true }
    if (!$rejected) { throw 'Invalid shade command accepted' }
}
$registrations = $coreType.GetField('_registeredShades', $flags).GetValue($core)
if ($registrations[[ushort]1001] -ne 1) { throw 'Reinitialization leaked shade registration' }
$second = [Activator]::CreateInstance($shadeType)
$second.Initialize([ushort]62000,[ushort]1001)
if ($registrations[[ushort]1001] -ne 2) { throw 'Shared shade registration was lost' }
$second.Initialize([ushort]62000,[ushort]1002)
if ($registrations[[ushort]1001] -ne 1 -or $registrations[[ushort]1002] -ne 1) { throw 'Changing proxy ID invalidated another adapter registration' }
$jsonAssembly = [Reflection.Assembly]::LoadFrom((Join-Path $release 'SimplSharpNewtonsoft.dll'))
$parseJson = $jsonAssembly.GetType('Newtonsoft.Json.Linq.JToken').GetMethod('Parse',[type[]]@([string]))
$matchEvent = $coreType.GetMethod('TryRegisteredShadeEvent',$flags)
$coreType.GetField('_online',$flags).SetValue($core,$true)
foreach ($case in @(@('{"evtName":"OnDataToUI","iddevice":1001,"data":{"anything":1}}',0,$true), @('{"evtName":"OnDataToUI","iddevice":1001}',1,$false), @('{"evtName":"OnDataToUI","iddevice":999}',0,$false), @('{"evtName":"Other","iddevice":1001}',0,$false), @('{"evtName":{},"iddevice":1001}',0,$false))) {
    $message = $parseJson.Invoke($null,[object[]]@($case[0]))
    $arguments = [object[]]@($message,[int]$case[1],[ushort]0)
    if ($matchEvent.Invoke($core,$arguments) -ne $case[2]) { throw 'Shade event filtering failed' }
}
$coreType.GetField('_online',$flags).SetValue($core,$false)
# Seed an in-flight work item to check burst coalescing without starting network I/O.
$workType = $coreType.GetNestedType('ShadeQueryWork',[Reflection.BindingFlags]'NonPublic')
$work = [Activator]::CreateInstance($workType,$true)
$workType.GetField('Epoch',$flags).SetValue($work,$coreType.GetField('_feedbackEpoch',$flags).GetValue($core))
$queries = $coreType.GetField('_shadeQueries',$flags).GetValue($core)
$queries.Add([ushort]1001,$work)
[void]$coreType.GetMethod('QueueShadeQuery',$flags).Invoke($core,[object[]]@([ushort]1001,$true))
if ($queries.Count -ne 1 -or !$workType.GetField('Dirty',$flags).GetValue($work) -or !$workType.GetField('FromEvent',$flags).GetValue($work)) { throw 'Event burst was not combined with current shade read' }
$queries.Clear()
Write-Output 'Shade control/event checks passed: command payloads, validation, shared registrations, stale/unknown event filtering and burst coalescing.'
