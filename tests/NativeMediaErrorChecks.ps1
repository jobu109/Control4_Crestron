$ErrorActionPreference='Stop'
$release=Join-Path $PSScriptRoot '../Control4Gateway/bin/Release'
Get-ChildItem $release -Filter '*.dll' | ForEach-Object {[void][Reflection.Assembly]::LoadFrom($_.FullName)}
$type=([Reflection.Assembly]::LoadFrom((Join-Path $release 'Control4Gateway.dll'))).GetType('Control4Crestron.GatewayCore',$true)
$method=$type.GetMethod('NativeMediaErrorSummary',[Reflection.BindingFlags]'Static,NonPublic')
$result=$method.Invoke($null,[object[]]@('{"error":"Missing required parameter deselect", "token":"SECRET_VALUE", "name":"HEAVY"}'))
if($result -ne 'error=Missing required parameter deselect'){throw 'Incorrect error details or sensitive output'}
foreach($body in @('not JSON','',('x'*4097),'{"token":"PRIVATE_VALUE"}')){
 $result=$method.Invoke($null,[object[]]@($body))
 if($result -match 'PRIVATE_VALUE|SECRET_VALUE|^x' -or $result -eq $body){throw 'Raw response leaked'}
}
$result=$method.Invoke($null,[object[]]@('{"error":{"code":17,"message":"FAILED_TO_EXECUTE"}}'))
if($result -ne 'code=17; message=FAILED_TO_EXECUTE'){throw 'Error codes or previously unrecognized messages lost'}
$result=$method.Invoke($null,[object[]]@('{"message":"Bearer abc123 token=SECRET_VALUE https://example.test/private"}'))
if($result -match 'abc123|SECRET_VALUE|example.test'){throw 'Message credential redaction failed'}
'Native media error checks passed: error codes/messages, bounds and credential redaction.'
$listener=[Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback,0)
$listener.Start()
try{
 $port=([Net.IPEndPoint]$listener.LocalEndpoint).Port
 $request=[Net.HttpWebRequest]::Create('http://127.0.0.1:'+$port+'/')
 $request.Proxy=$null
 $request.Timeout=5000
 $pending=$request.BeginGetResponse($null,$null)
 $client=$listener.AcceptTcpClient()
 try{
  $stream=$client.GetStream()
  $stream.ReadTimeout=5000
  $reader=[IO.StreamReader]::new($stream)
  while($reader.ReadLine() -ne ''){}
  $body='{"error":"Missing required parameter deselect","token":"SECRET_VALUE"}'
  $bytes=[Text.Encoding]::ASCII.GetBytes("HTTP/1.1 400 Bad Request`r`nContent-Type: application/json`r`nContent-Length: "+$body.Length+"`r`nConnection: close`r`n`r`n"+$body)
  $stream.Write($bytes,0,$bytes.Length)
  $stream.Flush()
 }finally{$client.Dispose()}
 $failure=$null
 try{[void]$request.EndGetResponse($pending)}catch{$failure=$_.Exception; while($failure -and $failure -isnot [Net.WebException]){$failure=$failure.InnerException}}
 if(!$failure){throw 'Expected HTTP WebException'}
 $format=$type.GetMethod('NativeMediaFailureMessage',[Reflection.BindingFlags]'Static,NonPublic')
 $result=$format.Invoke($null,[object[]]@($failure))
 if($result -ne 'HTTP 400; error=Missing required parameter deselect'){throw ('HTTP response body was unavailable or leaked: '+$result)}
}finally{$listener.Stop()}
'Actual loopback HTTP400 response test passed: body classified before response disposal.'
