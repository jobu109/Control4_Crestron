$ErrorActionPreference='Stop'
$release=Join-Path $PSScriptRoot '../Control4Gateway/bin/Release'
Get-ChildItem $release -Filter '*.dll' | ForEach-Object {[void][Reflection.Assembly]::LoadFrom($_.FullName)}
$type=([Reflection.Assembly]::LoadFrom((Join-Path $release 'Control4Gateway.dll'))).GetType('Control4Crestron.GatewayCore',$true)
$build=$type.GetMethod('BuildServicePresetSelection',[Reflection.BindingFlags]'Static,NonPublic')
$fixture=Get-Content (Join-Path $PSScriptRoot '../research/media_tests/Spotify_HEAVY_ComposerAction.json') -Raw | ConvertFrom-Json
$metadata='[{"command":"SelectPreset","params":[{"name":"Room"},{"name":"Preset"}]}]'
$body=$build.Invoke($null,[object[]]@($metadata,[ushort]13,[ushort]64,[string]$fixture.preset)) | ConvertFrom-Json
if($body.command -cne 'SelectPreset' -or $body.tParams.Room -isnot [string] -or $body.tParams.Room -cne '13' -or $body.tParams.Preset -cne $fixture.preset -or @($body.tParams.psobject.Properties).Count -ne 2){throw 'Composer exact command/string values changed'}
foreach($bad in @('','171','HEAVY','abc','aa;ROOM_OFF')){
 $rejected=$false
 try{[void]$build.Invoke($null,[object[]]@($metadata,[ushort]13,[ushort]64,$bad))}catch{$rejected=$true}
 if(!$rejected){throw 'Invalid selector accepted'}
}
foreach($bad in @('[]','[{"command":"SelectPreset","params":[{"name":"Room"}]}]','[{"command":"SelectPreset","params":[{"name":"Room"},{"name":"Preset"},{"name":"Other"}]}]')){
 $rejected=$false
 try{[void]$build.Invoke($null,[object[]]@($bad,[ushort]13,[ushort]64,[string]$fixture.preset))}catch{$rejected=$true}
 if(!$rejected){throw 'Unverified service contract accepted'}
}
$core=[Activator]::CreateInstance($type,[object[]]@([ushort]62028))
$core.SelectServicePreset(13,64,[string]$fixture.preset)
if($type.GetField('_busy',[Reflection.BindingFlags]'Instance,NonPublic').GetValue($core) -ne 0){throw 'Offline preset queued'}
'Service preset checks passed: exact Composer Room/Preset strings, strict selector/metadata validation and offline rejection.'
