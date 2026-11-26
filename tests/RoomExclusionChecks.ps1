$ErrorActionPreference = 'Stop'
$release = Join-Path $PSScriptRoot '../Control4Gateway/bin/Release'
Get-ChildItem $release -Filter '*.dll' | ForEach-Object { [void][Reflection.Assembly]::LoadFrom($_.FullName) }
$assembly = [Reflection.Assembly]::LoadFrom((Join-Path $release 'Control4Gateway.dll'))
$routerType = $assembly.GetType('Control4Crestron.Control4_Panel_Room', $true)
$roomType = $assembly.GetType('Control4Crestron.Control4_Room', $true)
$instance = [Reflection.BindingFlags]'Instance,NonPublic'
$static = [Reflection.BindingFlags]'Static,NonPublic'
$config = $routerType.GetField('_roomExclusions', $instance)
$left = [Activator]::CreateInstance($routerType)
$right = [Activator]::CreateInstance($routerType)
$left.SetRoomConfiguration(1, '212|524, 602,100002')
$left.SetRoomConfiguration(2, '212|437')
$left.SetRoomConfiguration(3, '13|524|Kitchen On, Kitchen Off')
$roomConfigs = $routerType.GetField('_roomConfigurations', $instance).GetValue($left)
$scenes = $roomConfigs[[ushort]3].LightScenes
if ($scenes.Count -ne 2 -or !$scenes.Contains('Kitchen On') -or !$scenes.Contains('Kitchen Off')) {
    throw 'Comma-separated scene names were not parsed individually'
}
$left.SetRoomConfiguration(3, '13|524|Kitchen On; Kitchen Off')
if ($roomConfigs[[ushort]3].LightScenes.Count -ne 2) { throw 'Legacy scene delimiter stopped working' }
$left.Initialize(64500, '13,212')
$right.Initialize(64500, '13,212')
$left.SetRoomConfiguration(1, '212|broken')
$map = $config.GetValue($left)
if ($map[[ushort]212].Count -ne 4 -or !$map[[ushort]212].Contains(100002) -or
    !$map[[ushort]13].Contains(524) -or $config.GetValue($right).Count -ne 0) {
    throw 'Exclusions lost, shared across routers, or truncated to 16-bit item IDs'
}
$jarray = ([Reflection.Assembly]::LoadFrom((Join-Path $release 'SimplSharpNewtonsoft.dll'))).GetType('Newtonsoft.Json.Linq.JArray', $true)
$parse = $jarray.GetMethod('Parse', [type[]]@([string]))
$devices = $parse.Invoke($null, [object[]]@('[{"id":524,"name":"Sink"},{"id":437,"name":"Hall"},{"id":603,"name":"Other"}]'))
$visible = $routerType.GetMethod('VisibleRoomItems', $static)
$kitchen = $visible.Invoke($null, [object[]]@($devices, $map, [ushort]212)).ToString() | ConvertFrom-Json
$living = $visible.Invoke($null, [object[]]@($devices, $map, [ushort]13)).ToString() | ConvertFrom-Json
if ($kitchen.id -ne 603 -or $living.Count -ne 2 -or $devices.Count -ne 3) { throw 'Room display filters or original catalog changed incorrectly' }
$room = [Activator]::CreateInstance($roomType)
$roomType.GetField('SourceExclusions', $instance).SetValue($room, $map[[ushort]212])
$sources = $parse.Invoke($null, [object[]]@('[{"source_id":100002},{"source_id":65}]'))
$filtered = $roomType.GetMethod('VisibleSources', $instance).Invoke($room, [object[]](,$sources)).ToString() | ConvertFrom-Json
if ($filtered.source_id -ne 65 -or $sources.Count -ne 2) { throw 'Source catalog exclusion failed' }
$left.SetRoomConfiguration(1, '')
if ($config.GetValue($left)[[ushort]212].Count -ne 1) { throw 'Removing one slot affected another slot' }
'Room exclusions checks passed: pre-init config, panel scope, union, invalid input, device and source filtering, and clearing'
