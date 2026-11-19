$ErrorActionPreference='Stop'
$release=Join-Path $PSScriptRoot '../Control4Gateway/bin/Release'
Get-ChildItem $release -Filter '*.dll' | ForEach-Object {[void][Reflection.Assembly]::LoadFrom($_.FullName)}
$type=([Reflection.Assembly]::LoadFrom((Join-Path $release 'Control4Gateway.dll'))).GetType('Control4Crestron.GatewayCore',$true)
$flags=[Reflection.BindingFlags]'Static,NonPublic'
$build=$type.GetMethod('BuildPandoraItemSelection',$flags)
$fixture=Get-Content (Join-Path $PSScriptRoot '../research/media_tests/Pandora_Instrumental_ComposerAction.json') -Raw | ConvertFrom-Json
$body=$build.Invoke($null,[object[]]@('[]',[ushort]13,[ushort]1647,[string]$fixture.item,$true)) | ConvertFrom-Json
if($body.command -cne 'Play Item' -or $body.tParams.Room -isnot [string] -or $body.tParams.Room -cne '13' -or $body.tParams.Item -isnot [string] -or $body.tParams.Item -cne $fixture.item -or $body.tParams.Shuffle -cne 'On' -or @($body.tParams.psobject.Properties).Count -ne 3){throw 'Copied Pandora action values not preserved'}
foreach($bad in @('171','Instrumental Radio','ST:0:0','ST:0:123;ROOM_OFF')){
 $rejected=$false
 try{[void]$build.Invoke($null,[object[]]@('[]',[ushort]13,[ushort]1647,$bad,$false))}catch{$rejected=$true}
 if(!$rejected){throw 'Invalid Pandora selector accepted'}
}
$inspect=$type.GetMethod('InspectServicePresetCatalog',$flags)
$result=$inspect.Invoke($null,[object[]]@('[{"id":1,"device_id":1648,"location":"ST:0:4574543691136306420"}]',[string]$fixture.item)).ToString()
if(($result | ConvertFrom-Json).exact_selector_matches.Count -ne 1 -or $result -match '4574543691136306420'){throw 'Pandora exact-match inspection/privacy failed'}
'Pandora action checks passed: exact copied command/string parameters, selector validation and private discovery inspection. Runtime evidence is tracked in docs/ROOM_SERVICE_PRESETS.md.'
$off=$build.Invoke($null,[object[]]@('[]',[ushort]13,[ushort]1647,[string]$fixture.item,$false)) | ConvertFrom-Json
if($off.tParams.Shuffle -cne 'Off' -or $off.tParams.Item -cne $fixture.item){throw 'Shuffle Off changed station selector or serialized incorrectly'}
$core=[Activator]::CreateInstance($type,[object[]]@([ushort]62035))
$field=$type.GetField('_pandoraShuffle',[Reflection.BindingFlags]'Instance,NonPublic')
$modes=$field.GetValue($core)
if($modes.ContainsKey([ushort]13)){throw 'Fresh room has an unexpected shuffle override'}
$core.SetPandoraShuffle(13,$true)
if(!$modes[[ushort]13]){throw 'Shuffle On not applied'}
$core.SetPandoraShuffle(13,$false)
if($modes[[ushort]13]){throw 'Shuffle Off not applied'}
if($type.GetField('_busy',[Reflection.BindingFlags]'Instance,NonPublic').GetValue($core) -ne 0){throw 'Setting shuffle triggered network work'}
'Pandora shuffle checks passed: default false, explicit On/Off, selector preservation and no command on setting change.'
