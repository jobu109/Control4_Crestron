$ErrorActionPreference='Stop'
$release=Join-Path $PSScriptRoot '../Control4Gateway/bin/Release'
Get-ChildItem $release -Filter '*.dll' | ForEach-Object {[void][Reflection.Assembly]::LoadFrom($_.FullName)}
$type=([Reflection.Assembly]::LoadFrom((Join-Path $release 'Control4Gateway.dll'))).GetType('Control4Crestron.GatewayCore',$true)
$inspect=$type.GetMethod('InspectNowPlayingVariables',[Reflection.BindingFlags]'Static,NonPublic')
$fixture='[{"varName":"CURRENT_TITLE","value":"Demo Track"},{"varName":"MEDIA_INFO","value":"{\"artist\":\"Demo Artist\",\"album\":\"Demo Album\",\"duration\":180,\"token\":\"PRIVATE_TOKEN\",\"coverart\":\"https://private.test/image?token=SECRET\"}"},{"varName":"ACCOUNT","value":"PRIVATE_ACCOUNT"}]'
$text=$inspect.Invoke($null,[object[]]@($fixture)).ToString()
foreach($expected in @('Demo Track','Demo Artist','Demo Album','observed_value','unverified','artwork_reference_present')){if($text -notmatch $expected){throw ('Missing metadata observation '+$expected)}}
if($text -match 'PRIVATE_TOKEN|PRIVATE_ACCOUNT|https://private.test|SECRET'){throw 'Private scalar/URL leaked'}
$xml='[{"varName":"MEDIA_INFO","value":"<media><title>PRIVATE_XML_TITLE</title><token>PRIVATE_XML_TOKEN</token></media>"}]'
$text=$inspect.Invoke($null,[object[]]@($xml)).ToString()
if($text -match 'PRIVATE_XML' -or $text -notmatch 'xml_outline_only'){throw 'XML outline policy failed'}
$invalid=$false
try{[void]$inspect.Invoke($null,[object[]]@('{}'))}catch{$invalid=$true}
if(!$invalid){throw 'Non-list variables accepted'}
$large='[{"varName":"TITLE","value":"'+('x'*65537)+'"}]'
$text=$inspect.Invoke($null,[object[]]@($large)).ToString()
if($text.Length -gt 2000 -or $text -notmatch 'truncated'){throw 'Oversized scalar not bounded'}
$core=[Activator]::CreateInstance($type,[object[]]@([ushort]62040))
$fixture='[{"varName":"Now Playing","value":{"mediainfo":{"title":"Current song","artist":"Artist","img":"https://images.example.test/art/PRIVATE_PATH.jpg?token=PRIVATE_QUERY#PRIVATE_FRAGMENT","deviceid":65}}},{"varName":"Queue Info","value":{"EVENT":{"NAME":"QueueChanged","ARGS":{"NowPlayingIndex":2,"List":{"item":[{"track":"Next song","album_cover_url":"/PRIVATE_RELATIVE_ART","artist_uri":"PRIVATE_ARTIST_URI"}]}}}}}]'
$text=$inspect.Invoke($null,[object[]]@($fixture)).ToString()
foreach($expected in @('Now Playing','Current song','Next song','QueueChanged','observed_value','absolute_http','relative_path','images.example.test','has_query')){if($text -notmatch [regex]::Escape($expected)){throw ('Missing identity/address observation '+$expected)}}
if($text -match 'PRIVATE_PATH|PRIVATE_QUERY|PRIVATE_FRAGMENT|PRIVATE_RELATIVE_ART|PRIVATE_ARTIST_URI'){throw 'Artwork path/query or unrelated URI leaked'}
$shape=$inspect.Invoke($null,[object[]]@('[{"varName":"ARTIST_URI","value":"https://private.test/not-artwork"}]'))
if($shape.ToString() -match 'artwork_reference_present'){throw 'Artist URI misclassified as artwork'}
$core.InspectNowPlaying(13)
$tune='[{"varName":"META","value":{"devicecommand":{"command":"Select Station","params":{"param":[{"name":"Room","value":{"type":"STRING","static":"13"}},{"name":"StationId","value":{"type":"STRING","static":"s12345"}},{"name":"Opaque","value":{"type":"STRING","static":"PRIVATE_SELECTOR"}}]}},"RESPONSE":{"DATA":{"List":{"item":[{"text":"Example station","guide_id":"s12345","default_action":"PLAY","URL":"https://private.test/?token=PRIVATE_URL"}]}}}}}]'
$text=$inspect.Invoke($null,[object[]]@($tune)).ToString()
foreach($expected in @('observed_command','Select Station','StationId','s12345','Example station','public_id','PLAY')){if($text -notmatch [regex]::Escape($expected)){throw ('Missing TuneIn inspection field '+$expected)}}
if($text -match 'PRIVATE_SELECTOR|PRIVATE_URL|https://private.test'){throw 'TuneIn command inspection leaked unknown selector/URL'}
$state='[{"varName":"META","value":{"NowPlaying":"PAUSED","Items":"play,pause,PRIVATE_SELECTOR","streamStatus":"playing"}}]'
$text=$inspect.Invoke($null,[object[]]@($state)).ToString()
if($text -notmatch 'exact_state_candidate' -or $text -notmatch 'paused' -or $text -match 'PRIVATE_SELECTOR'){throw 'Safe state observation failed'}
$encoded='[{"varName":"META","value":{"NowPlaying":"{&quot;playback_state&quot;:&quot;paused&quot;,&quot;token&quot;:&quot;PRIVATE_TOKEN&quot;}"}}]'
$text=$inspect.Invoke($null,[object[]]@($encoded)).ToString()
if($text -notmatch 'paused' -or $text -match 'PRIVATE_TOKEN'){throw 'Encoded structured state inspection failed'}
if($type.GetField('_busy',[Reflection.BindingFlags]'Instance,NonPublic').GetValue($core) -ne 0){throw 'Offline metadata inspection queued requests'}
'Now-playing inspection checks passed: variable and JSON metadata, scalar/URL suppression, XML outline, size bounds and offline rejection.'
