$ErrorActionPreference='Stop'
$release=Join-Path $PSScriptRoot '../Control4Gateway/bin/Release'
Get-ChildItem $release -Filter '*.dll' | ForEach-Object {[void][Reflection.Assembly]::LoadFrom($_.FullName)}
$type=([Reflection.Assembly]::LoadFrom((Join-Path $release 'Control4Gateway.dll'))).GetType('Control4Crestron.GatewayCore',$true)
$flags=[Reflection.BindingFlags]'Static,NonPublic'
$candidate=$type.GetMethod('ServiceDriverCandidate',$flags)
$identity=$type.GetMethod('ServiceDriverIdentity',$flags)
# Synthetic metadata shapes, not a claim that the real controller returns parent_id.
foreach($case in @(@('[{"id":65,"type":7,"parent_id":64}]',64),@('[{"id":65,"type":7,"driver_id":64,"parent_id":13}]',64),@('[{"id":65,"type":7,"parent":{"id":64}}]',64),@('[{"id":65,"type":7}]',0),@('[{"id":65,"type":7,"parent_id":64,"parentId":66}]',0),@('[{"id":65,"type":8,"parent_id":64}]',0),@('[{"id":65,"type":7,"parent_id":65}]',0))){
 $args=[object[]]@($case[0],[ushort]65,$null)
 if($candidate.Invoke($null,$args) -ne $case[1]){throw 'Explicit relationship resolution failed'}
}
$args=[object[]]@('[{"id":65,"type":7,"parent_id":64,"password":"DO_NOT_EXPORT","name":"DO_NOT_EXPORT"}]',[ushort]65,$null)
[void]$candidate.Invoke($null,$args)
if($args[2].ToString() -match 'DO_NOT_EXPORT|password'){throw 'Mapping report leaked scalar/credential data'}
if(!$identity.Invoke($null,[object[]]@('[{"id":64,"type":6}]',[ushort]64)) -or $identity.Invoke($null,[object[]]@('[{"id":13,"type":8}]',[ushort]13))){throw 'Protocol identity validation failed'}
$rejected=$false
try{[void]$identity.Invoke($null,[object[]]@('[{"id":66,"type":6}]',[ushort]64))}catch{$rejected=$true}
if(!$rejected){throw 'Wrong identity accepted'}
'Automatic mapping checks passed: explicit relationships, ambiguity/self/type/identity rejection and report privacy. Runtime relationship evidence is tracked in docs/ROOM_SERVICE_PRESETS.md.'
