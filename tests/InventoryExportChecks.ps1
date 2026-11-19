$ErrorActionPreference = 'Stop'
$release = Join-Path $PSScriptRoot '../Control4Gateway/bin/Release'
Get-ChildItem $release -Filter '*.dll' | ForEach-Object { [void][Reflection.Assembly]::LoadFrom($_.FullName) }
$library = [Reflection.Assembly]::LoadFrom((Join-Path $release 'Control4Gateway.dll'))
$core = $library.GetType('Control4Crestron.GatewayCore', $true)
$parse = $core.GetMethod('InventoryItems', [Reflection.BindingFlags]'NonPublic,Static')
$fixture = '[{"id":437,"name":"Main \"Hall\"","type":"light","room_id":12,"token":"DO-NOT-EXPORT","password":"DO-NOT-EXPORT","private":{"data":"DO-NOT-EXPORT"}},{"item_id":"436","name":"Wireless Dimmer","type":"driver"}]'
$items = $parse.Invoke($null, [object[]]@($fixture))
$serialized = $items.ToString()
$decoded = ConvertFrom-Json -InputObject $serialized
if ($decoded.Count -ne 2 -or $decoded[0].item_id -ne 437 -or $decoded[1].item_id -ne 436) { throw 'Inventory item IDs changed' }
if ($decoded[0].name -ne 'Main "Hall"' -or $decoded[0].room_id -ne 12) { throw 'Inventory metadata or escaping failed' }
if ($serialized -match 'DO-NOT-EXPORT|password|token|private') { throw 'Unexpected response fields leaked into export' }
foreach ($fixture in @('{}', '[{}]', '[{"id":0}]', '[{"id":true}]', '[{"id":"bad"}]', '[null]', 'not JSON')) {
    $rejected = $false
    try { [void]$parse.Invoke($null, [object[]]@($fixture)) } catch { $rejected = $true }
    if (!$rejected) { throw ('Invalid inventory accepted: ' + $fixture) }
}
$empty = $parse.Invoke($null, [object[]]@('[]'))
if ($empty.Count -ne 0) { throw 'Empty category not supported' }
$longName = 'x' * 700
$items = $parse.Invoke($null, [object[]]@('[{"id":1,"name":"' + $longName + '"}]'))
if ((ConvertFrom-Json -InputObject $items.ToString())[0].name.Length -ne 512) { throw 'Metadata length bound failed' }
Write-Output 'Inventory checks passed: IDs preserved, metadata/JSON escaping, response allowlist, malformed entries, empty categories and text bounds.'
