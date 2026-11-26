$ErrorActionPreference = 'Stop'
$release = Join-Path $PSScriptRoot '../Control4Gateway/bin/Release'
Get-ChildItem $release -Filter '*.dll' | ForEach-Object { [void][Reflection.Assembly]::LoadFrom($_.FullName) }
$core = ([Reflection.Assembly]::LoadFrom((Join-Path $release 'Control4Gateway.dll'))).GetType('Control4Crestron.GatewayCore', $true)
$json = ([Reflection.Assembly]::LoadFrom((Join-Path $release 'SimplSharpNewtonsoft.dll'))).GetType('Newtonsoft.Json.Linq.JObject', $true)
$parse = $json.GetMethod('Parse', [type[]]@([string]))
$flags = [Reflection.BindingFlags]'Instance,NonPublic'
$pageType = $core.GetNestedType('TuneInPage', [Reflection.BindingFlags]::NonPublic)
$build = $core.GetMethod('BuildTuneInMenu', [Reflection.BindingFlags]'Static,NonPublic')
$root = [Activator]::CreateInstance($pageType, $true)
$pageType.GetField('Screen', $flags).SetValue($root, 'Browse')
$pageType.GetField('Name', $flags).SetValue($root, 'Browse')
$rows = '{"List":{"item":[{"text":"Local Radio","folder":true,"URL":"http://opml.radiotime.com/Browse.ashx?c=local&serial=PRIVATE"},{"text":"Example Station","type":"audio","guide_id":"s456","URL":"http://opml.radiotime.com/Tune.ashx?id=s456"},{"text":"External","folder":true,"URL":"https://example.org/Browse.ashx"}]}}'
$params = [object[]]@($parse.Invoke($null, [object[]]@($rows)), $root, $null, $false)
$actions = $build.Invoke($null, $params)
$public = $params[2].ToString() | ConvertFrom-Json
if ($actions.Count -ne 3 -or $public[0].name -notlike 'My Favorites*' -or $public[1].name -notlike 'Local Radio*' -or $public[2].name -ne 'Example Station') {
    throw 'Expected a Favorites tab, a folder and a station'
}
if ($params[2].ToString() -match 'PRIVATE|https?://|External') { throw 'Private or unsupported menu data exposed' }
$destination = $actions['2'].GetType().GetField('Destination', $flags).GetValue($actions['2'])
$argsXml = $actions['2'].GetType().GetField('Args', $flags).GetValue($actions['2'])
if ($argsXml -notmatch 'Browse.ashx' -or $pageType.GetField('Parent', $flags).GetValue($destination) -ne $root) { throw 'Folder lost its native action or parent' }
$params = [object[]]@($parse.Invoke($null, [object[]]@('{"List":{}}')), $destination, $null, $false)
$actions = $build.Invoke($null, $params)
if ($actions.Count -ne 1 -or $params[2].ToString() -notmatch 'Back to Browse') { throw 'Empty submenu lost its Back entry' }
'TuneIn hierarchy checks passed'
