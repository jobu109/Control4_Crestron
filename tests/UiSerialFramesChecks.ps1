$ErrorActionPreference='Stop'
$release=Join-Path $PSScriptRoot '../Control4Gateway/bin/Release'
Get-ChildItem $release -Filter '*.dll' | ForEach-Object {[void][Reflection.Assembly]::LoadFrom($_.FullName)}
$library=[Reflection.Assembly]::LoadFrom((Join-Path $release 'Control4Gateway.dll'))
$type=$library.GetType('Control4Crestron.UiSerialFrames',$true)
$flags=[Reflection.BindingFlags]'Instance,NonPublic'
$set=$type.GetMethod('Set',$flags);$read=$type.GetMethod('Read',$flags);$count=$type.GetProperty('Count',$flags)
$vectors=[Collections.Generic.List[object]]::new()
foreach($kind in @('a','v','m')){
 $wire=[Activator]::CreateInstance($type,$true)
 $items=@(1..162 | ForEach-Object {[ordered]@{id=[string]$_;name=('Station '+$_+' – 音楽 🎵 '+('quoted " x\'*7))}})
 $doc=[ordered]@{room_id=13;mode= $(if($kind -eq 'v'){'watch'}else{'listen'});kind='service_presets';valid=$true;page=1;pages=1;total=$items.Count;items=$items;revision='0123456789abcdef0123456789abcdef';selection_supported=$true;source_id=65;driver_id=64}
 $json=ConvertTo-Json $doc -Depth 8 -Compress
 if(!$set.Invoke($wire,[object[]]@([ushort]13,[string]$kind,[string]$json))){throw 'First document not initialized'}
 if($set.Invoke($wire,[object[]]@([ushort]13,[string]$kind,[string]$json))){throw 'Same document changed epoch'}
 $frames=[Collections.Generic.List[object]]::new();$text=[Text.StringBuilder]::new()
 for($i=1;$i -le $count.GetValue($wire);$i++){
  $args=[object[]]@([ushort]$i);$raw=$read.Invoke($wire,$args)
  if($raw.Length -gt 255 -or [Text.Encoding]::UTF8.GetByteCount($raw) -ne $raw.Length){throw 'Non-ASCII/oversize frame'}
  $frame=$raw | ConvertFrom-Json
  if($frame.p -ne $i -or $frame.n -ne $count.GetValue($wire)){throw 'Wrong frame sequence'}
  [void]$text.Append($frame.d);$frames.Add($frame)
 }
 if([Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($text.ToString())) -cne $json){throw 'Framing changed IDs/names/Unicode'}
 $args=[object[]]@([ushort]65535);[void]$read.Invoke($wire,$args);if($args[0] -ne $frames.Count){throw 'Frame index not clamped'}
 $vectors.Add(@{kind=$kind;document=$doc;frames=@($frames)})
}
$path=Join-Path $PSScriptRoot '../reports/ui-serial-frame-vectors.json'
[IO.File]::WriteAllText($path,(ConvertTo-Json -InputObject @($vectors) -Depth 12),[Text.UTF8Encoding]::new($false))
'UI serial framing passed:255-byte ASCII bound, stable snapshots, lossless UTF8 IDs/names, bounds and cross-language vectors'

