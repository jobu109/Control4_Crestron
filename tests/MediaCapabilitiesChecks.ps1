$ErrorActionPreference = 'Stop'
$release = Join-Path $PSScriptRoot '../Control4Gateway/bin/Release'
Get-ChildItem $release -Filter '*.dll' | ForEach-Object { [void][Reflection.Assembly]::LoadFrom($_.FullName) }
$library = [Reflection.Assembly]::LoadFrom((Join-Path $release 'Control4Gateway.dll'))
$coreType = $library.GetType('Control4Crestron.GatewayCore', $true)
$flags = [Reflection.BindingFlags]'NonPublic,Static'
$build = $coreType.GetMethod('BuildMediaCapabilitiesReport', $flags)
$pathFilter = $coreType.GetMethod('MediaLocalPath', $flags)
$fixture = @'
[{
  "id":20,"name":"DO-NOT-EXPORT-TITLE","profile":"DO-NOT-EXPORT-PROFILE",
  "URIs":{
    "GET":"/api/v1/items/20/catalog?JWT=DO-NOT-EXPORT-JWT#private",
    "POST":{"href":"/api/v1/items/20/commands"},
    "links":[
      {"method":"GET","href":"https://192.168.1.160/api/v1/items/20/browse?token=DO-NOT-EXPORT-TOKEN"},
      {"method":"GET","href":"/api/v1/items/20/catalog"},
      {"method":"POST","href":"/api/v1/items/20/search"},
      {"href":"/api/v1/items/20/variables"},
      {"method":"GET","href":"https://example.com/api/v1/items/20/browse"},
      {"method":"GET","href":"/api/v1/items/20/DO-NOT-EXPORT-PATH"}
    ],
    "account":{"GET":"/api/v1/items/20/favorites"},
    "DO-NOT-EXPORT-KEY":{"token":"DO-NOT-EXPORT-NESTED"}
  }
}]
'@
$report = $build.Invoke($null, [object[]]@($fixture,'192.168.1.160',200))
$serialized = $report.ToString()
$decoded = ConvertFrom-Json -InputObject $serialized
if ($serialized -match 'DO-NOT-EXPORT|example.com|JWT|private') { throw 'Private or external metadata leaked' }
if ($decoded.source_id -ne 20 -or $decoded.http_status -ne 200 -or $decoded.item_count -ne 1 -or !$decoded.uris_present) { throw 'Incorrect report envelope' }
$paths = @($decoded.advertised_local_get_paths)
if ($paths.Count -ne 2 -or $paths -notcontains '/api/v1/items/20/catalog' -or $paths -notcontains '/api/v1/items/20/browse') { throw 'Explicit GET extraction/deduplication failed' }
if (@($decoded.advertised_local_paths_method_unknown).Count -ne 1 -or $decoded.advertised_local_paths_method_unknown[0] -ne '/api/v1/items/20/variables') { throw 'Unknown method was misclassified' }
if ($paths -contains '/api/v1/items/20/commands' -or $paths -contains '/api/v1/items/20/search' -or $paths -contains '/api/v1/items/20/favorites') { throw 'Write or private subtree was exported as GET' }
if ($decoded.browse_endpoint_status -ne 'not_established') { throw 'Metadata was misrepresented as working browsing' }
foreach ($fixture in @('[{"id":20}]','[{"id":20,"URIs":null}]','[{"id":20,"URIs":["DO-NOT-EXPORT-BARE"]}]')) {
    $report = $build.Invoke($null,[object[]]@($fixture,'192.168.1.160',200))
    if ($report.ToString() -match 'DO-NOT-EXPORT' -or @((ConvertFrom-Json -InputObject $report.ToString()).advertised_local_get_paths).Count -ne 0) { throw 'Unknown URI structure interpreted as a link' }
}
foreach ($fixture in @('{}','[]','[null]','[{"id":21}]','[{"id":20},{"id":20}]','not JSON')) {
    $rejected = $false
    try { [void]$build.Invoke($null,[object[]]@($fixture,'192.168.1.160',200)) } catch { $rejected = $true }
    if (!$rejected) { throw 'Invalid media item response accepted' }
}
$rejected = $false
try { [void]$build.Invoke($null,[object[]]@('[{"id":20}]','192.168.1.160',302)) } catch { $rejected = $true }
if (!$rejected) { throw 'Redirect response reported as success' }
foreach ($raw in @('http://192.168.1.160/api/v1/items/20','https://192.168.1.160:444/api/v1/items/20','https://user:secret@192.168.1.160/api/v1/items/20','//example.com/api/v1/items/20','/api/v1/accounts/123','/api/v1/items/20/%73ecret','/api/v1/items/20/../catalog',"/api/v1/items/20`r`n")) {
    if ($null -ne $pathFilter.Invoke($null,[object[]]@($raw,'192.168.1.160'))) { throw 'Unsafe or unrecognized path accepted' }
}
# Ensure bounded traversal is explicit instead of silently suggesting exhaustive discovery.
$links = 1..80 | ForEach-Object { '"key' + $_ + '":{"href":"/api/v1/items/20"}' }
$large = '[{"id":20,"URIs":{' + ($links -join ',') + '}}]'
$report = $build.Invoke($null,[object[]]@($large,'192.168.1.160',200))
if (!(ConvertFrom-Json -InputObject $report.ToString()).inspection_truncated) { throw 'Traversal limit was not reported' }
Write-Output 'Media capability checks passed: GET/unknown method separation, deduplication, redaction, path restrictions, invalid responses, bounds and no false browse claim.'
