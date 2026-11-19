$ErrorActionPreference = 'Stop'
$release = Join-Path $PSScriptRoot '../Control4Gateway/bin/Release'
Get-ChildItem $release -Filter '*.dll' | ForEach-Object { [void][Reflection.Assembly]::LoadFrom($_.FullName) }
$library = [Reflection.Assembly]::LoadFrom((Join-Path $release 'Control4Gateway.dll'))
$core = $library.GetType('Control4Crestron.GatewayCore', $true)
$flags = [Reflection.BindingFlags]'NonPublic,Static'
$capability = $core.GetMethod('BuildMediaCapabilitiesForSource',$flags)
$command = $core.GetMethod('BuildMediaCommandsForSource',$flags)
$compare = $core.GetMethod('BuildMediaComparisonReport',$flags)
$jsonAssembly = [Reflection.Assembly]::LoadFrom((Join-Path $release 'SimplSharpNewtonsoft.dll'))
$arrayType = $jsonAssembly.GetType('Newtonsoft.Json.Linq.JArray')
$tokenType = $jsonAssembly.GetType('Newtonsoft.Json.Linq.JToken')
$parse = $tokenType.GetMethod('Parse',[type[]]@([string]))
$sources = [Activator]::CreateInstance($arrayType)
foreach ($id in @(20,65,599,100002)) {
    $fixture = '[{"id":' + $id + ',"URIs":{"GET":"/api/v1/items/' + $id + '/catalog?JWT=DO_NOT_EXPORT"}}]'
    $cap = $capability.Invoke($null,[object[]]@($fixture,'192.168.1.160',200,$id))
    $decoded = $cap.ToString() | ConvertFrom-Json
    if ($decoded.source_id -ne $id -or $decoded.advertised_local_get_paths[0] -ne ('/api/v1/items/' + $id + '/catalog') -or $cap.ToString() -match 'DO_NOT_EXPORT') { throw 'Capability ID/path/redaction failed' }
    $cmd = $command.Invoke($null,[object[]]@('[]',200,$id))
    $decoded = $cmd.ToString() | ConvertFrom-Json
    if ($decoded.source_id -ne $id -or $decoded.request_path -ne ('/api/v1/items/' + $id + '/commands') -or $decoded.command_count -ne 0) { throw 'Command source ID/path failed (including IDs above 65535)' }
    $sources.Add($cmd)
}
$report = $compare.Invoke($null,[object[]]@($sources,$true))
$decoded = $report.ToString() | ConvertFrom-Json
if ($decoded.schema_version -ne 2 -or $decoded.source_count -ne 4 -or $decoded.successful_source_count -ne 4 -or $decoded.error_count -ne 0 -or @($decoded.sources).Count -ne 4 -or $decoded.browse_endpoint_status -ne 'not_established') { throw 'Combined success report invalid' }
$partial = $parse.Invoke($null,[object[]]@('[{"source_id":20,"http_status":200},{"source_id":65,"http_status":200},{"source_id":599,"http_status":404,"error_type":"HTTP 404"},{"source_id":100002,"http_status":null,"error_type":"InvalidDataException"}]'))
$report = $compare.Invoke($null,[object[]]@($partial,$false))
$decoded = $report.ToString() | ConvertFrom-Json
if ($decoded.successful_source_count -ne 2 -or $decoded.error_count -ne 2 -or $decoded.report_kind -ne 'item_capability_comparison') { throw 'Partial failures were lost or counted as success' }
foreach ($fixture in @('[]','[{"source_id":20},{"source_id":20},{"source_id":599},{"source_id":100002}]','[{"source_id":20},{"source_id":65},{"source_id":599},{"source_id":1}]')) {
    $array = $parse.Invoke($null,[object[]]@($fixture))
    $rejected = $false
    try { [void]$compare.Invoke($null,[object[]]@($array,$true)) } catch { $rejected = $true }
    if (!$rejected) { throw 'Incomplete/duplicate/unapproved comparison accepted' }
}
$rejected = $false
try { [void]$capability.Invoke($null,[object[]]@('[{"id":20}]','192.168.1.160',200,65)) } catch { $rejected = $true }
if (!$rejected) { throw 'Mismatched response item ID accepted' }
$rejected = $false
try { [void]$command.Invoke($null,[object[]]@('[]',200,1)) } catch { $rejected = $true }
if (!$rejected) { throw 'Unapproved source accepted' }
Write-Output 'Media comparison checks passed: four source IDs, Digital Media 100002, redaction, per-source paths, aggregate counts, partial failures and invalid batch rejection.'
