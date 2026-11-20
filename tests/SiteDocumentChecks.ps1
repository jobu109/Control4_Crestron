$ErrorActionPreference='Stop'
# Checks GatewayCore.BuildPanelSite with the variable shapes observed on the EA-5
# (tests/fixtures/SiteWeatherProbe_rev2_2026-11-20.json): varName keys, Float coordinates,
# and an OpenWeather driver that reports 0 for everything when it is not updating.
$release=Join-Path $PSScriptRoot '../Control4Gateway/bin/Release'
Get-ChildItem $release -Filter '*.dll' | ForEach-Object {[void][Reflection.Assembly]::LoadFrom($_.FullName)}
$library=[Reflection.Assembly]::LoadFrom((Join-Path $release 'Control4Gateway.dll'))
$type=$library.GetType('Control4Crestron.GatewayCore',$true)
$json=[Reflection.Assembly]::LoadFrom((Join-Path $release 'SimplSharpNewtonsoft.dll'))
$parse=$json.GetType('Newtonsoft.Json.Linq.JToken').GetMethod('Parse',[type[]]@([string]))
$build=$type.GetMethod('BuildPanelSite',[Reflection.BindingFlags]'NonPublic,Static')
function Check($ok,$message){ if(-not $ok){ throw $message } }
function Site($root,$weather,$known=$null,$custom=$null){
  # Plain assignments: "$x = if (...) { ... }" would enumerate the JArray into its items.
  $r=$null; if($root){ $r=$parse.Invoke($null,[object[]]@($root)) }
  $w=$null; if($weather){ $w=$parse.Invoke($null,[object[]]@($weather)) }
  $c=$null; if($custom){ $c=$parse.Invoke($null,[object[]]@($custom)) }
  return $build.Invoke($null,[object[]]@($r,$w,$known,$c)).ToString()
}
$root='[{"varName":"Zipcode","name":"Floyd","value":""},{"varName":"Latitude","name":"Floyd","value":40.25},{"varName":"Longitude","name":"Floyd","value":-95.5}]'
$zeros='[{"varName":"CURRENT_TEMPERATURE","value":0},{"varName":"CURRENT_HUMIDITY","value":0},{"varName":"CURRENT_PRESSURE","value":0},{"varName":"TODAY_TEMPERATURE_MIN","value":0},{"varName":"TODAY_TEMPERATURE_MAX","value":0}]'
$live='[{"varName":"CURRENT_TEMPERATURE","value":68},{"varName":"CURRENT_HUMIDITY","value":48},{"varName":"CURRENT_PRESSURE","value":1016},{"varName":"TODAY_TEMPERATURE_MIN","value":55},{"varName":"TODAY_TEMPERATURE_MAX","value":72}]'

$a=Site $root $zeros | ConvertFrom-Json
Check ($a.latitude -eq 40.25 -and $a.longitude -eq -95.5) 'Coordinates not read by varName'
Check ($a.weather_status -eq 'not_reporting' -and $null -eq $a.weather) 'All-zero driver must be not_reporting'

$b=Site $root $live | ConvertFrom-Json
Check ($b.weather_status -eq 'reporting' -and $b.weather.temperature -eq 68 -and $b.weather.low -eq 55 -and $b.weather.high -eq 72 -and $b.weather.humidity -eq 48) 'Live weather not published'

$c=Site $null $null | ConvertFrom-Json
Check ($null -eq $c.latitude -and $c.weather_status -eq 'unavailable') 'Missing items must be unavailable'

$d=Site '[{"varName":"Latitude","value":0},{"varName":"Longitude","value":0}]' '[{"varName":"OTHER","value":1}]' | ConvertFrom-Json
Check ($null -eq $d.latitude -and $d.weather_status -eq 'unavailable') '0,0 coordinates and agents without CURRENT_TEMPERATURE must be rejected'

$e=Site '[{"varName":"Latitude","value":95},{"varName":"Longitude","value":10}]' $null | ConvertFrom-Json
Check ($null -eq $e.latitude) 'Out-of-range latitude accepted'
# Temperature scale from the driver's own readings (EA-5 values: 68, 56 %, dew point 51.75).
$f=Site $root '[{"varName":"CURRENT_TEMPERATURE","value":68},{"varName":"CURRENT_HUMIDITY","value":56},{"varName":"CURRENT_PRESSURE","value":1025},{"varName":"CURRENT_DEW_POINT","value":51.75}]' | ConvertFrom-Json
Check ($f.temperature_unit -eq 'F') "Fahrenheit readings not identified: $($f.temperature_unit)"
$g=Site $root '[{"varName":"CURRENT_TEMPERATURE","value":20},{"varName":"CURRENT_HUMIDITY","value":56},{"varName":"CURRENT_PRESSURE","value":1025},{"varName":"CURRENT_DEW_POINT","value":10.9}]' | ConvertFrom-Json
Check ($g.temperature_unit -eq 'C') "Celsius readings not identified: $($g.temperature_unit)"
$humid='[{"varName":"CURRENT_TEMPERATURE","value":68},{"varName":"CURRENT_HUMIDITY","value":95},{"varName":"CURRENT_PRESSURE","value":1025},{"varName":"CURRENT_DEW_POINT","value":66.5}]'
$h=Site $root $humid | ConvertFrom-Json
Check ($null -eq $h.temperature_unit) 'Near-saturated readings must stay undecided'
$i=Site $root $humid 'F' | ConvertFrom-Json
Check ($i.temperature_unit -eq 'F') 'Previously identified scale not kept'
Check ($null -eq $b.temperature_unit) 'Scale published without a dew point'
# Place name published by the weather driver (Director does not expose Composer's location).
$j=Site $root '[{"varName":"CURRENT_TEMPERATURE","value":68},{"varName":"CURRENT_HUMIDITY","value":56},{"varName":"CITY","value":"Fallback"},{"varName":"LOCATION_NAME","value":"  Sample City, ST  "}]' | ConvertFrom-Json
Check ($j.name -eq 'Sample City, ST') "Place name not published: $($j.name)"
$k=Site $root '[{"varName":"CURRENT_TEMPERATURE","value":0},{"varName":"LOCATION_NAME","value":""},{"varName":"CITY","value":"Sample City"}]' | ConvertFrom-Json
Check ($k.name -eq 'Sample City' -and $k.weather_status -eq 'not_reporting') 'Place name must not depend on weather reporting'
$l=Site $root '[{"varName":"CURRENT_TEMPERATURE","value":68},{"varName":"LOCATION_NAME","value":"<b>x</b>"},{"varName":"LOCATION","value":"https://example.invalid"}]' | ConvertFrom-Json
Check ($null -eq $l.name) 'Unsafe place name accepted'
Check ($null -eq $b.name) 'Place name invented without a driver variable'
# Composer custom variable (Programming > Variables) when the driver has none.
$m=Site $root $live $null '[{"varName":"Some_Flag","value":"1"},{"varName":"LOCATION_NAME","value":"Sample Town, ST"}]' | ConvertFrom-Json
Check ($m.name -eq 'Sample Town, ST') "Custom variable place name not published: $($m.name)"
$n2=Site $root '[{"varName":"CURRENT_TEMPERATURE","value":68},{"varName":"LOCATION_NAME","value":"Driver City"}]' $null '[{"varName":"LOCATION_NAME","value":"Custom City"}]' | ConvertFrom-Json
Check ($n2.name -eq 'Driver City') 'Driver place name must win over the custom variable'
'Site document checks passed: driver place name (preferred, independent of weather, unsafe rejected); temperature scale identified from readings (F, C, undecided, remembered); varName coordinates, all-zero weather rejected, live weather published, invalid coordinates and missing agents unavailable'
