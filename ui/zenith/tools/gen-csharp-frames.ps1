# Regenerates tests/fixtures/csharp-frames.json from the Release Control4Gateway.dll.
# Usage (repo root, after a Release build):
#   ./ui/zenith/tools/gen-csharp-frames.ps1 -Release Control4Gateway/bin/Release -Out ui/zenith/tests/fixtures/csharp-frames.json
param([string]$Release,[string]$Out)
$ErrorActionPreference='Stop'
Get-ChildItem $Release -Filter '*.dll' | ForEach-Object {[void][Reflection.Assembly]::LoadFrom($_.FullName)}
$library=[Reflection.Assembly]::LoadFrom((Join-Path $Release 'Control4Gateway.dll'))
$type=$library.GetType('Control4Crestron.UiSerialFrames',$true)
$flags=[Reflection.BindingFlags]'Instance,NonPublic'
$set=$type.GetMethod('Set',$flags);$read=$type.GetMethod('Read',$flags);$count=$type.GetProperty('Count',$flags)
$cases=@(
 @{room=13;kind='a';doc=[ordered]@{room_id=13;mode='listen';valid=$true;page=1;pages=1;total=40;items=@(1..40|ForEach-Object{[ordered]@{id=[string](1000+$_);name=('Station '+$_+' – 音楽 🎵 "q"')}})}},
 @{room=13;kind='d';doc=[ordered]@{room_id=13;devices=@(1..12|ForEach-Object{[ordered]@{id=400+$_;name=('Light '+$_+' café');shade=($_ -gt 10);value=$(if($_ -eq 3){$null}else{$_*5})}})}},
 @{room=0;kind='rooms';doc=[ordered]@{room_id=0;rooms=@([ordered]@{id=13;name='Living Room'},[ordered]@{id=212;name='Kitchen'})}}
)
$vectors=@()
foreach($c in $cases){
 $wire=[Activator]::CreateInstance($type,$true)
 $json=ConvertTo-Json $c.doc -Depth 8 -Compress
 [void]$set.Invoke($wire,[object[]]@([ushort]$c.room,[string]$c.kind,[string]$json))
 $frames=@(); for($i=1;$i -le $count.GetValue($wire);$i++){ $frames+= $read.Invoke($wire,[object[]]@([ushort]$i)) }
 $vectors+=[ordered]@{room=$c.room;kind=$c.kind;json=$json;frames=$frames}
}
[IO.File]::WriteAllText($Out,(ConvertTo-Json -InputObject $vectors -Depth 6),[Text.UTF8Encoding]::new($false))
"wrote $($vectors.Count) vectors"

