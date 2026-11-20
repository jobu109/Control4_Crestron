$ErrorActionPreference='Stop'
# Checks the read-only site/weather probe filter: location and weather values are kept,
# secrets, street address, URLs and network identity are dropped.
$release=Join-Path $PSScriptRoot '../Control4Gateway/bin/Release'
Get-ChildItem $release -Filter '*.dll' | ForEach-Object {[void][Reflection.Assembly]::LoadFrom($_.FullName)}
$library=[Reflection.Assembly]::LoadFrom((Join-Path $release 'Control4Gateway.dll'))
$type=$library.GetType('Control4Crestron.GatewayCore',$true)
$json=[Reflection.Assembly]::LoadFrom((Join-Path $release 'SimplSharpNewtonsoft.dll'))
$parse=$json.GetType('Newtonsoft.Json.Linq.JToken').GetMethod('Parse',[type[]]@([string]))
$flags=[Reflection.BindingFlags]'NonPublic,Static'
$values=$type.GetMethod('SiteProbeValues',$flags)
$rows=$type.GetMethod('SiteVariableRows',$flags)
function Check($ok,$message){ if(-not $ok){ throw $message } }

$detail=$parse.Invoke($null,[object[]]@('{"id":1,"name":"Sample","city":"Sample City","state":"NSW","zip":"2000","latitude":-33.9,"longitude":151.0,"timezone":"Australia/Sydney","street_address":"1 Private St","api_key":"SECRET","ip_address":"10.0.0.5","location":{"lat":-33.9,"lon":151.0,"postal_code":"2000","owner_email":"x@y"},"icon_url":"https://example.invalid/i.png","condition":"<xml/>","deep":{"a":{"b":{"temp":1}}}}'))
$v=$values.Invoke($null,[object[]]@(,$detail))
$text=$v.ToString()
foreach($kept in 'city','state','zip','latitude','longitude','timezone','location.lat','location.lon','location.postal_code','name'){ Check ($null -ne $v[$kept]) "Missing allowed field $kept" }
foreach($dropped in 'street_address','api_key','ip_address','location.owner_email','icon_url','condition','deep.a.b.temp'){ Check ($null -eq $v[$dropped]) "Leaked field $dropped" }
Check (-not $text.Contains('SECRET') -and -not $text.Contains('Private St') -and -not $text.Contains('10.0.0.5') -and -not $text.Contains('x@y')) 'Sensitive text leaked'

# Director rows: varName is the variable name, name is the owning item (here "Floyd", which must not be used).
$vars=$parse.Invoke($null,[object[]]@('[{"varName":"TEMPERATURE","name":"Floyd","value":"22"},{"varName":"CONDITION","name":"Floyd","value":"Partly cloudy"},{"varName":"SUNRISE","name":"Floyd","value":"07:03"},{"varName":"API_KEY","name":"Floyd","value":"SECRET"},{"varName":"FORECAST_XML","name":"Floyd","value":"<forecast>..</forecast>"},{"varName":"OTHER","name":"Weather","value":"hidden"},{"varName":"bad name!","name":"Floyd","value":"x"}]'))
$r=$rows.Invoke($null,[object[]]@(,$vars))
$names=@($r | ForEach-Object { $_['name'].ToString() })
Check (($names -join ',') -eq 'TEMPERATURE,CONDITION,SUNRISE,FORECAST_XML,OTHER') "Unexpected variable rows: $($names -join ',')"
$byName=@{}; foreach($row in $r){ $byName[$row['name'].ToString()]=$row }
Check ($byName['TEMPERATURE']['value'].ToString() -eq '22') 'Temperature value missing'
Check ($byName['CONDITION']['value'].ToString() -eq 'Partly cloudy') 'Condition value missing'
Check ($null -eq $byName['OTHER']['value']) 'Unhinted variable value leaked'
Check ($null -eq $byName['FORECAST_XML']['value'] -and [int]$byName['FORECAST_XML']['length'].ToString() -gt 0) 'XML forecast should report length only'
Check (-not $r.ToString().Contains('SECRET')) 'Variable secret leaked'
# Revision 4: advertised URIs are limited to the item's own /api/v1 paths.
$paths=$type.GetMethod('SiteResourcePaths',$flags)
$describe=$type.GetMethod('DescribeSiteResource',$flags)
$uris=$parse.Invoke($null,[object[]]@('{"variables":"/api/v1/items/1/variables","bindings":"/api/v1/items/1/bindings","other":"/api/v1/items/2/variables","web":"https://example.invalid/x","bad":"/api/v1/items/1/../../x?y=1","n":5}'))
$p=$paths.Invoke($null,[object[]]@($uris,[uint16]1))
$pathList=@($p | ForEach-Object { $_['path'].ToString() })
Check (($pathList -join ',') -eq '/api/v1/items/1/variables,/api/v1/items/1/bindings') "Unexpected URI paths: $($pathList -join ',')"
$props=$parse.Invoke($null,[object[]]@('[{"name":"City","value":"Sample City"},{"name":"Location (City, State)","value":"Sample, ST"},{"name":"Temperature Units (°F)","value":"Imperial"},{"name":"Street Address","value":"1 Private St"},{"name":"API Key","value":"SECRET"},{"name":"Other","value":"hidden"}]'))
$row=$parse.Invoke($null,[object[]]@('{}'))
[void]$describe.Invoke($null,[object[]]@($props,$row))
$entryNames=@($row['entries'] | ForEach-Object { $_['name'].ToString() })
Check (($entryNames -join '|') -eq 'City|Location (City, State)|Temperature Units (?F)|Street Address|API Key|Other') "Unexpected property rows: $($entryNames -join '|')"
$e=$row['entries']
Check ($e[0]['value'].ToString() -eq 'Sample City' -and $e[1]['value'].ToString() -eq 'Sample, ST' -and $e[2]['value'].ToString() -eq 'Imperial') 'Location/unit property values missing'
Check ($null -eq $e[3]['value'] -and $e[3]['withheld'].ToString() -eq 'True' -and $null -eq $e[4]['value'] -and $null -eq $e[5]['value']) 'Blocked or unhinted values not withheld'
Check (-not $row.ToString().Contains('SECRET') -and -not $row.ToString().Contains('Private St')) 'Property secret leaked'
'Site/weather probe filter passed: advertised paths limited to the item; every property name listed, blocked values withheld; location and weather kept; secrets, street address, URLs, XML and network identity dropped'
