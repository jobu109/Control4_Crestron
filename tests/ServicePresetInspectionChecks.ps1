$ErrorActionPreference='Stop'
$release=Join-Path $PSScriptRoot '../Control4Gateway/bin/Release'
Get-ChildItem $release -Filter '*.dll' | ForEach-Object {[void][Reflection.Assembly]::LoadFrom($_.FullName)}
$type=([Reflection.Assembly]::LoadFrom((Join-Path $release 'Control4Gateway.dll'))).GetType('Control4Crestron.GatewayCore',$true)
$inspect=$type.GetMethod('InspectServicePresetCatalog',[Reflection.BindingFlags]'Static,NonPublic')
$fixture='[{"id":171,"name":"PRIVATE_TITLE","deviceId":64,"location":"aabbccdd","token":"aabbccdd"},{"id":172,"location":"prefix-aabbccdd"}]'
$text=$inspect.Invoke($null,[object[]]@($fixture,'AABBCCDD')).ToString()
$report=$text | ConvertFrom-Json
if($report.exact_selector_matches.Count -ne 1 -or $report.exact_selector_matches[0].selector_paths.Count -ne 1 -or $report.exact_selector_matches[0].selector_paths[0] -cne '$.location'){throw 'Exact matching/sensitive-key suppression failed'}
if($text -match 'aabbccdd|PRIVATE_TITLE|prefix-|"token"'){throw 'Unrelated scalar/selector leaked'}
if($report.exact_selector_matches[0].record_shape.fields.id.numeric_id -cne '171' -or $report.exact_selector_matches[0].record_shape.fields.deviceId.numeric_id -cne '64'){throw 'Numeric identity evidence lost'}
$empty=$inspect.Invoke($null,[object[]]@('[]','aabbccdd')).ToString() | ConvertFrom-Json
if($empty.source_count -ne 0 -or $empty.scan_incomplete){throw 'Empty list misrepresented'}
$huge='['+((1..2050 | ForEach-Object {'{"id":1}'}) -join ',')+']'
$bounded=$inspect.Invoke($null,[object[]]@($huge,'aabbccdd')).ToString() | ConvertFrom-Json
if(!$bounded.scan_incomplete -or $bounded.visited -ne 2048){throw 'Catalog scan bound failed'}
$core=[Activator]::CreateInstance($type,[object[]]@([ushort]62029))
$core.InspectServicePresets(13,64,'aabbccdd')
if($type.GetField('_busy',[Reflection.BindingFlags]'Instance,NonPublic').GetValue($core) -ne 0){throw 'Offline inspection queued I/O'}
'Service preset inspection checks passed: exact matching, identity evidence, scalar suppression, bounds, empty lists and offline rejection.'
