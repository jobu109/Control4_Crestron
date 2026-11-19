$ErrorActionPreference='Stop'
$folder=Join-Path $PSScriptRoot '../SIMPLPlus'
foreach($source in Get-ChildItem $folder -Filter '*.usp'){
 $text=[IO.File]::ReadAllText($source.FullName)
 if($text -match '(?<!\r)\n'){throw ("SIMPL+ requires CRLF source lines: "+$source.Name)}
 $signature=[IO.File]::ReadAllText([IO.Path]::ChangeExtension($source.FullName,'.ush'))
 $cues=@([regex]::Matches($signature,'(?m)^\s*(?:InputCue|InputList2Cue|OutputCue|OutputList2Cue|ParamCue)\d+=([^\r\n]*)') | ForEach-Object {$_.Groups[1].Value})
 $count=0
 foreach($declaration in [regex]::Matches($text,'(?m)^\s*(DIGITAL_INPUT|ANALOG_INPUT|STRING_INPUT|DIGITAL_OUTPUT|ANALOG_OUTPUT|STRING_OUTPUT|INTEGER_PARAMETER|STRING_PARAMETER)\s+([^;]+);')){
  foreach($entry in $declaration.Groups[2].Value.Split(',')){
   $name=($entry.Trim() -replace '\[.*$','').Trim()
   if($name -eq '_skip_'){continue}
   $dimension=[regex]::Match($entry.Trim(),'\[([^\]]+)\]')
   $isArray=$dimension.Success -and ($declaration.Groups[1].Value -notin 'STRING_INPUT','STRING_PARAMETER' -or $entry.Trim() -match '\]\[')
   $expected=@($name)
   if($isArray){
    $sizeText=$dimension.Groups[1].Value
    $size=0
    if(![int]::TryParse($sizeText,[ref]$size)){
     $constant=[regex]::Match($text,'(?m)^#DEFINE_CONSTANT\s+'+[regex]::Escape($sizeText)+'\s+(\d+)')
     if(!$constant.Success){throw ('Unknown array dimension: '+$sizeText)}
     $size=[int]$constant.Groups[1].Value
    }
    $expected=@(1..$size | ForEach-Object {$name+'['+$_+']'})
    if(($name+'[#]') -in $cues){$expected=@($name+'[#]')}
   }
   foreach($expectedName in $expected){
    if($expectedName -notin $cues){throw ("Missing generated symbol cue in "+$source.BaseName+": "+$expectedName)}
    $count++
   }
  }
 }
 if($source.BaseName -ne 'Control4_Library_Load_Probe' -and $count -eq 0){throw ('Empty interface: '+$source.Name)}
 Write-Output ($source.BaseName+": "+$count+" signal/parameter cues verified")
}
$roomText=[IO.File]::ReadAllText((Join-Path $folder 'Control4_Room.usp'))
$roomSignature=[IO.File]::ReadAllText((Join-Path $folder 'Control4_Room.ush'))
foreach($mode in 'Video','Audio'){
 $size=[int]([regex]::Match($roomText,'#DEFINE_CONSTANT\s+'+$mode+'Sources\s+(\d+)').Groups[1].Value)
 foreach($base in @(($mode+'_Source'),($mode+'_Source_Available'),($mode+'_Source_Name_FB$'))){
  foreach($index in 1..$size){
   if(!$roomSignature.Contains('='+$base+'['+$index+']'+[char]13)){throw ('Room array must expose every fixed slot: '+$base+'['+$index+']')}
  }
 }
}
Write-Output 'Room fixed source arrays verified through configured constants.'

