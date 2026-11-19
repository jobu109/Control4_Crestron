$ErrorActionPreference='Stop'
$release=Join-Path $PSScriptRoot '../Control4Gateway/bin/Release'
Get-ChildItem $release -Filter '*.dll' | ForEach-Object {[void][Reflection.Assembly]::LoadFrom($_.FullName)}
$type=([Reflection.Assembly]::LoadFrom((Join-Path $release 'Control4Gateway.dll'))).GetType('Control4Crestron.GatewayCore',$true)
$static=[Reflection.BindingFlags]'Static,NonPublic'
$instance=[Reflection.BindingFlags]'Instance,NonPublic'
$build=$type.GetMethod('BuildServicePresetList',$static)
# Raw shape/device/location relation verified by CP17b; records here are synthetic.
$json='[{"id":171,"device_id":65,"name":"HEAVY","location":"aabbccdd"},{"id":172,"device_id":65,"name":"Other preset","location":"11223344"},{"id":231,"device_id":20,"name":"Radio","location":"https://private.test"},{"id":173,"device_id":65,"name":"Invalid","location":"nothex"}]'
$args=[object[]]@($json,[ushort]13,[ushort]64,[ushort]65,$null)
$public=$build.Invoke($null,$args)
$report=$public.ToString() | ConvertFrom-Json
$selectors=$args[4]
if($report.total -ne 2 -or $report.omitted -ne 1 -or $report.scope -ne 'service_saved_presets' -or !$report.selection_supported){throw 'Service list normalization failed'}
if($public.ToString() -match 'aabbccdd|11223344|private.test|Radio'){throw 'Selector or unrelated-source data leaked to widget'}
if($selectors['171'] -cne 'aabbccdd' -or $selectors['172'] -cne '11223344' -or $selectors.ContainsKey('231')){throw 'Private source/selector map failed'}
$pandora='[{"id":357,"device_id":1648,"name":"Instrumental Radio","location":"ST:0:4574543691136306420"},{"id":171,"device_id":65,"name":"HEAVY","location":"aabbccdd"}]'
$args=[object[]]@($pandora,[ushort]13,[ushort]1647,[ushort]1648,$null)
$pandoraList=$build.Invoke($null,$args)
if(($pandoraList.ToString() | ConvertFrom-Json).total -ne 1 -or $pandoraList.ToString() -match '4574543691136306420|HEAVY'){throw 'Pandora station filtering/privacy failed'}
$body=$type.GetMethod('BuildPandoraItemSelection',$static).Invoke($null,[object[]]@('[]',[ushort]13,[ushort]1647,[string]$args[4]['357'],$false)) | ConvertFrom-Json
if($body.command -cne 'Play Item' -or $body.tParams.Item -cne 'ST:0:4574543691136306420'){throw 'Discovered station mapping changed the copied selector'}
$duplicate='[{"id":171,"device_id":65,"name":"A","location":"aabb"},{"id":171,"device_id":65,"name":"B","location":"ccdd"}]'
$args=[object[]]@($duplicate,[ushort]13,[ushort]64,[ushort]65,$null)
$result=$build.Invoke($null,$args).ToString() | ConvertFrom-Json
if($result.total -ne 0 -or $args[4].Count -ne 0){throw 'Ambiguous duplicate ID remained selectable'}
$core=[Activator]::CreateInstance($type,[object[]]@([ushort]62031))
$core.BrowseServicePresets(13,64,65)
if($type.GetField('_busy',$instance).GetValue($core) -ne 0){throw 'Offline browse queued I/O'}
$listType=$type.GetNestedType('ServicePresetList',[Reflection.BindingFlags]'NonPublic')
$list=[Activator]::CreateInstance($listType,$true)
$lists=$type.GetField('_servicePresetLists',$instance).GetValue($core)
$lists.Add([ushort]13,$list)
$current=$type.GetMethod('ServiceListCurrent',$instance)
if(!$current.Invoke($core,[object[]]@([ushort]13,$list))){throw 'Current list rejected'}
[void]$type.GetMethod('InvalidateRoomCatalogs',$instance).Invoke($core,@())
if($current.Invoke($core,[object[]]@([ushort]13,$list)) -or $lists.Count -ne 0){throw 'Disconnected/stale list retained'}
'Dynamic service checks passed: source filtering, private selector map, widget privacy, duplicate rejection, offline browse and stale/disconnect invalidation.'
