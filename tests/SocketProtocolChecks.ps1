$ErrorActionPreference = 'Stop'
$release = Join-Path $PSScriptRoot '../Control4Gateway/bin/Release'
Get-ChildItem $release -Filter '*.dll' | ForEach-Object { [void][Reflection.Assembly]::LoadFrom($_.FullName) }
$library = [Reflection.Assembly]::LoadFrom((Join-Path $release 'Control4Gateway.dll'))
$protocol = $library.GetType('Control4Crestron.Control4SocketProtocol', $true)
$flags = [Reflection.BindingFlags]'NonPublic,Static'
$parseEvent = $protocol.GetMethod('TryEvent', $flags)
$parseLight = $protocol.GetMethod('TryLight', $flags)
$makeEvent = $protocol.GetMethod('Event', $flags)
$jsonAssembly = [Reflection.Assembly]::LoadFrom((Join-Path $release 'SimplSharpNewtonsoft.dll'))
$tokenType = $jsonAssembly.GetType('Newtonsoft.Json.Linq.JToken', $true)
$parseJson = $tokenType.GetMethod('Parse', [type[]]@([string]))
$count = 0
foreach ($case in @(
    @('42["clientId","fixture-client"]', $true, 'clientId'),
    @('421["clientId","fixture-client"]', $true, 'clientId'),
    @('42/,23["clientId","fixture-client"]', $true, 'clientId'),
    @('42/other,["clientId","fixture-client"]', $false, ''),
    @('42["subscription",{"evtName":"OnDataToUI","iddevice":437,"data":{"light_level":79}}]', $true, 'subscription'),
    @('40', $false, ''), @('3', $false, ''), @('42{}', $false, ''),
    @('42[]', $false, ''), @('42[1,2]', $false, ''), @('42[bad', $false, '')
)) {
    $arguments = [object[]]@($case[0], $null, $null)
    $actual = $parseEvent.Invoke($null, $arguments)
    if ($actual -ne $case[1] -or ($actual -and $arguments[1] -ne $case[2])) { throw ('Event case failed: ' + $case[0]) }
    $count++
}
foreach ($case in @(
    @('{"evtName":"OnDataToUI","iddevice":437,"data":{"light_level":79}}', $true, 79),
    @('{"evtName":"OnDataToUI","iddevice":437,"data":{"light_level":0}}', $true, 0),
    @('{"evtName":"OnDataToUI","iddevice":437,"data":{"light_level":"100"}}', $true, 100),
    @('{"evtName":"OnDataToUI","iddevice":437,"data":{"light_level":101}}', $false, 0),
    @('{"evtName":"OnDataToUI","iddevice":437,"data":{"light_level":-1}}', $false, 0),
    @('{"evtName":"OnDataToUI","iddevice":437,"data":{"light_level":50.5}}', $false, 0),
    @('{"evtName":"OnDataToUI","iddevice":437,"data":{"light_level":true}}', $false, 0),
    @('{"evtName":"OnDataToUI","iddevice":0,"data":{"light_level":50}}', $false, 0),
    @('{"evtName":"OnDataToUI","iddevice":70000,"data":{"light_level":50}}', $false, 0),
    @('{"evtName":"OnDataToUI","iddevice":437,"data":null}', $false, 0),
    @('{"evtName":{},"iddevice":437,"data":{"light_level":50}}', $false, 0),
    @('{"evtName":"Other","iddevice":437,"data":{"light_level":50}}', $false, 0),
    @('{}', $false, 0), @('[]', $false, 0)
)) {
    $json = $parseJson.Invoke($null, [object[]]@($case[0]))
    $arguments = [object[]]@($json, [ushort]0, [ushort]0)
    $actual = $parseLight.Invoke($null, $arguments)
    if ($actual -ne $case[1] -or ($actual -and ($arguments[1] -ne 437 -or $arguments[2] -ne $case[2]))) { throw ('Light case failed: ' + $case[0]) }
    $count++
}
$packet = $makeEvent.Invoke($null, [object[]]@('startSubscription', 'fixture-id'))
if ($packet -ne '42["startSubscription","fixture-id"]') { throw 'Subscription packet encoding failed' }
$count++
$packet = $makeEvent.Invoke($null, [object[]]@('2probe', $null))
if ($packet -ne '42["2probe"]') { throw 'Probe packet encoding failed' }
$count++
Write-Output "Socket protocol checks passed: $count"
$parseAcknowledged = $protocol.GetMethod('TryEventPacket', $flags)
$arguments = [object[]]@('42123["clientId","fixture-client"]', $null, $null, $null)
if (!$parseAcknowledged.Invoke($null, $arguments) -or $arguments[3] -ne '123') { throw 'Event acknowledgement ID was lost' }

# Exercise actual gateway routing without cloud, REST or processor/native activity.
$coreType = $library.GetType('Control4Crestron.GatewayCore', $true)
$core = [Activator]::CreateInstance($coreType, [object[]]@([ushort]1))
$instanceFlags = [Reflection.BindingFlags]'NonPublic,Instance'
$coreType.GetField('_generation', $instanceFlags).SetValue($core, 5)
$registered = $coreType.GetField('_registeredLights', $instanceFlags).GetValue($core)
[void]$registered.Add([ushort]437)
$route = $coreType.GetMethod('RouteRealtimeLight', $instanceFlags)
$message = $parseJson.Invoke($null, [object[]]@('{"evtName":"OnDataToUI","iddevice":437,"data":{"light_level":79}}'))
$lights = $coreType.GetField('_lights', $instanceFlags).GetValue($core)
[void]$route.Invoke($core, [object[]]@($message, 4))
if ($lights.Count -ne 0) { throw 'Stale session event was accepted' }
[void]$route.Invoke($core, [object[]]@($message, 5))
if ($lights.Count -ne 1) { throw 'Registered proxy event was not routed' }
$snapshot = $lights[[ushort]437]
if ($snapshot.GetType().GetField('Level').GetValue($snapshot) -ne 79) { throw 'Incorrect cached feedback' }
$unknown = $parseJson.Invoke($null, [object[]]@('{"evtName":"OnDataToUI","iddevice":999,"data":{"light_level":50}}'))
[void]$route.Invoke($core, [object[]]@($unknown, 5))
if ($lights.Count -ne 1) { throw 'Unregistered item was cached' }
[void]$coreType.GetMethod('StopRealtime', $instanceFlags).Invoke($core, @())
if ($lights.Count -ne 0) { throw 'Stopped session retained valid light state' }
Write-Output 'Gateway routing checks passed: stale session rejection, registered proxy routing, cached level, unknown item filtering, disconnect invalidation.'

$describe = $coreType.GetMethod('FailureKind', $flags, $null, [type[]]@([Exception]), $null)
$failure = [AggregateException]::new('PRIVATE-OUTER', [IO.IOException]::new('PRIVATE-URL-JWT', [Net.Sockets.SocketException]::new(10061)))
$description = $describe.Invoke($null, [object[]]@($failure))
if ($description -notmatch 'AggregateException > IOException > SocketException ConnectionRefused' -or $description -match 'PRIVATE') { throw 'Nested error description failed or exposed exception text' }
Write-Output 'Diagnostic checks passed: nested aggregate/socket cause exposed without private exception messages.'

# Endpoints alone do not identify a dimmer. Preserve explicit switch capability in later feedback.
$storeLight = $coreType.GetMethod('StoreLightFeedback', $instanceFlags)
[void]$storeLight.Invoke($core, [object[]]@([ushort]572, [ushort]100, $true, [ushort]0))
$snapshot = $lights[[ushort]572]
$capability = $snapshot.GetType().GetField('Dimming')
if ($capability.GetValue($snapshot) -ne 0) { throw '100 percent alone was classified as a dimmer' }
[void]$storeLight.Invoke($core, [object[]]@([ushort]572, [ushort]100, $false, [ushort]1))
$core.AcceptLightEvent(572, 0)
if ($capability.GetValue($lights[[ushort]572]) -ne 1) { throw 'Endpoint feedback changed a switch capability' }
$core.AcceptLightEvent(572, 48)
$core.AcceptLightEvent(572, 100)
if ($capability.GetValue($lights[[ushort]572]) -ne 2) { throw 'Measured intermediate level did not override the switch fallback' }
$core.AcceptLightEvent(424, 48)
$core.AcceptLightEvent(424, 100)
if ($capability.GetValue($lights[[ushort]424]) -ne 2) { throw 'Intermediate level did not establish sustained dimmer capability' }
'Light capability checks passed'
