function Read-RoomUiDocument($module,[string]$kind,[scriptblock]$read) {
 $method=switch($kind){'a'{'SetAudioListPage'} 'v'{'SetVideoListPage'} 'm'{'SetMediaPage'} default{throw 'Unknown stream'}}
 $module.$method([ushort]1)
 $first=(& $read) | ConvertFrom-Json
 if($first.w -ne 1){throw 'Expected bounded serial frame'}
 $data=[Text.StringBuilder]::new()
 for($i=1;$i -le $first.n;$i++){
  if($i -ne 1){$module.$method([ushort]$i)}
  $raw=& $read
  if([Text.Encoding]::UTF8.GetByteCount($raw) -gt 255){throw 'Serial frame exceeds255 bytes'}
  $frame=$raw | ConvertFrom-Json
  if($frame.e -ne $first.e -or $frame.p -ne $i -or $frame.n -ne $first.n){throw 'Unstable/inconsistent serial transfer'}
  [void]$data.Append($frame.d)
 }
 $text=[Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($data.ToString()))
 return ($text | ConvertFrom-Json)
}
