$ErrorActionPreference = 'Stop'
$release = Join-Path $PSScriptRoot '../Control4Gateway/bin/Release'
Get-ChildItem $release -Filter '*.dll' | ForEach-Object { [void][Reflection.Assembly]::LoadFrom($_.FullName) }
$library = [Reflection.Assembly]::LoadFrom((Join-Path $release 'Control4Gateway.dll'))
$coreType = $library.GetType('Control4Crestron.GatewayCore', $true)
$build = $coreType.GetMethod('BuildMediaCommandsReport',[Reflection.BindingFlags]'NonPublic,Static')
$fixture = @'
[
 {"command":"GET_LIST","parameters":[{"name":"FIRST","type":"INTEGER","required":true,"min":0,"max":100,"default":"DO_NOT_EXPORT_DEFAULT","description":"DO_NOT_EXPORT_DESCRIPTION"}],"token":"DO_NOT_EXPORT_TOKEN"},
 {"name":"PLAY","params":{"ITEM_ID":{"type":"STRING","required":false,"options":["DO_NOT_EXPORT_OPTIONS"]}}},
 {"commandName":"Search","tParams":[{"name":"QUERY","type":"STR","default":"DO_NOT_EXPORT_QUERY"}]},
 {"command":"AUTH_LOGIN","params":{"PASSWORD":"DO_NOT_EXPORT_PASSWORD"}},
 {"name":"DO NOT EXPORT TITLE"},
 "DO_NOT_EXPORT_STRING_ENTRY"
]
'@
$report = $build.Invoke($null,[object[]]@($fixture,200))
$serialized = $report.ToString()
$decoded = $serialized | ConvertFrom-Json
if ($serialized -match 'DO_NOT_EXPORT|AUTH_LOGIN|PASSWORD') { throw 'Private metadata content leaked' }
if ($decoded.request_method -ne 'GET' -or $decoded.request_path -ne '/api/v1/items/20/commands' -or $decoded.browse_endpoint_status -ne 'not_established') { throw 'Incorrect request semantics or false browse claim' }
if (!$decoded.command_list_recognized -or $decoded.command_count -ne 6 -or $decoded.exported_command_count -ne 3 -or $decoded.unreported_command_count -ne 3) { throw 'Incorrect recognition/filtering counts' }
if (@($decoded.browse_name_matches).Count -ne 2 -or $decoded.browse_name_matches -notcontains 'GET_LIST' -or $decoded.browse_name_matches -notcontains 'Search') { throw 'Heuristic candidate list incorrect' }
$first = $decoded.commands[0].parameter_definitions[0]
if ($first.name -ne 'FIRST' -or $first.type -ne 'integer' -or !$first.required -or $first.min -ne 0 -or $first.max -ne 100) { throw 'Parameter definition metadata lost' }
if ($decoded.commands[1].parameter_definitions[0].name -ne 'ITEM_ID' -or $decoded.commands[1].parameter_definitions[0].required -ne $false) { throw 'Map definitions or required=false lost' }
$wrapped = '{"commands":' + $fixture + ',"private":"DO_NOT_EXPORT_WRAPPER"}'
$decoded = $build.Invoke($null,[object[]]@($wrapped,200)).ToString() | ConvertFrom-Json
if ($decoded.command_count -ne 6) { throw 'Commands wrapper was not recognized' }
$report = $build.Invoke($null,[object[]]@('{"DO_NOT_EXPORT_KEY":{"value":"DO_NOT_EXPORT_VALUE"}}',200))
$decoded = $report.ToString() | ConvertFrom-Json
if ($decoded.command_list_recognized -or $null -ne $decoded.command_count -or $report.ToString() -match 'DO_NOT_EXPORT') { throw 'Unknown schema was misrepresented or leaked' }
$decoded = $build.Invoke($null,[object[]]@('[]',200)).ToString() | ConvertFrom-Json
if (!$decoded.command_list_recognized -or $decoded.command_count -ne 0) { throw 'Empty command list misclassified' }
foreach ($fixture in @('null','true','123','"scalar"','bad JSON')) {
    $rejected = $false
    try { [void]$build.Invoke($null,[object[]]@($fixture,200)) } catch { $rejected = $true }
    if (!$rejected) { throw 'Invalid command response accepted' }
}
$rejected = $false
try { [void]$build.Invoke($null,[object[]]@('[]',302)) } catch { $rejected = $true }
if (!$rejected) { throw 'Non-200 status reported as successful inspection' }
$large = '[' + ((1..260 | ForEach-Object { '{"command":"GET_LIST"}' }) -join ',') + ']'
$decoded = $build.Invoke($null,[object[]]@($large,200)).ToString() | ConvertFrom-Json
if (!$decoded.inspection_truncated -or $decoded.exported_command_count -ne 256) { throw 'Command count bound failed' }
$parameters = '{' + ((1..40 | ForEach-Object { '"PARAM' + $_ + '":{"type":"int"}' }) -join ',') + '}'
$fixture = '[{"command":"GET_LIST","params":' + $parameters + '}]'
$decoded = $build.Invoke($null,[object[]]@($fixture,200)).ToString() | ConvertFrom-Json
if (!$decoded.commands[0].parameters_truncated -or @($decoded.commands[0].parameter_definitions).Count -ne 32) { throw 'Parameter count bound failed' }
Write-Output 'Media command checks passed: list/wrapper shapes, command/schema metadata, privacy filtering, bounds, unknown responses and no false browse claim.'
