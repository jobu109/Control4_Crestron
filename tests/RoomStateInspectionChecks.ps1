$ErrorActionPreference='Stop'
$release=Join-Path $PSScriptRoot '../Control4Gateway/bin/Release'
Get-ChildItem $release -Filter '*.dll' | ForEach-Object {[void][Reflection.Assembly]::LoadFrom($_.FullName)}
$library=[Reflection.Assembly]::LoadFrom((Join-Path $release 'Control4Gateway.dll'))
$coreType=$library.GetType('Control4Crestron.GatewayCore',$true)
$build=$coreType.GetMethod('BuildRoomStateInspection',[Reflection.BindingFlags]'NonPublic,Static')
$fixture='[{"name":"CURRENT_AUDIO_SOURCE","value":"100002"},{"name":"VIDEO_SELECTION","value":2061},{"name":"POWER_STATE","value":true},{"name":"CURRENT_VOLUME","value":20},{"name":"IS_MUTED","value":"0"},{"name":"MEDIA_TITLE","value":"DO_NOT_EXPORT_TITLE"},{"name":"ACCESS_TOKEN","value":"DO_NOT_EXPORT_TOKEN"},{"name":"DEVICE_PASSWORD","value":"12345"},{"name":"SOURCE_LABEL","value":"DO_NOT_EXPORT_LABEL"},{"name":"CURRENT_AUDIO_DEVICE","value":-1},{"name":"bad name","value":65}]'
$report=$build.Invoke($null,[object[]]@($fixture,[ushort]13))
$text=$report.ToString(); $data=$text | ConvertFrom-Json
if($text -match 'DO_NOT_EXPORT|ACCESS_TOKEN|DEVICE_PASSWORD|12345|bad name'){throw 'Room state inspection leaked private values/names'}
if($data.variable_count -ne 11 -or $data.exported_variable_count -ne 8 -or $data.omitted_variable_count -ne 3){throw 'Variable filtering counts incorrect'}
if($data.variables[0].numeric_candidate -ne 100002 -or $data.variables[1].numeric_candidate -ne 2061 -or $data.variables[7].numeric_candidate -ne -1){throw 'Large IDs or unselected sentinel lost'}
if($data.variables[2].measured_value -ne 1 -or $data.variables[3].measured_value -ne 20 -or $data.variables[4].measured_value -ne 0){throw 'Measured state filtering failed'}
if($data.variables[5].numeric_candidate -or $data.variables[6].numeric_candidate){throw 'Arbitrary string value was exported'}
foreach($bad in @('{}','bad JSON','null')) {
 $failed=$false
 try{[void]$build.Invoke($null,[object[]]@($bad,[ushort]13))}catch{$failed=$true}
 if(!$failed){throw 'Invalid variable shape accepted'}
}
$large='['+((1..520 | ForEach-Object {'{"name":"CURRENT_SOURCE","value":65}'}) -join ',')+']'
$bounded=$build.Invoke($null,[object[]]@($large,[ushort]13)).ToString() | ConvertFrom-Json
if(!$bounded.inspection_truncated -or $bounded.exported_variable_count -ne 512){throw 'Variable inspection bound failed'}
$core=[Activator]::CreateInstance($coreType,[object[]]@([ushort]62015))
$core.InspectRoomState(13)
if($coreType.GetField('_busy',[Reflection.BindingFlags]'NonPublic,Instance').GetValue($core) -ne 0){throw 'Offline inspection queued network work'}
Write-Output 'Room state inspection checks passed: names/value allowlist, numeric source observations, large IDs/sentinels, independent measured values, privacy, bad schemas, bounds and no offline I/O.'
$names=$build.Invoke($null,[object[]]@('["CURRENT_AUDIO_SOURCE","CURRENT_VIDEO_DEVICE","POWER_STATE","MEDIA_TITLE","ACCESS_TOKEN","a private title"]',[ushort]13)).ToString() | ConvertFrom-Json
if($names.exported_variable_count -ne 4 -or $names.omitted_variable_count -ne 2 -or $names.first_entry_shape.type -ne 'String' -or $names.variables[0].value_type -ne 'missing'){throw 'Variable-name list support or privacy failed'}
foreach($alias in 'var_name','variable','key'){
 $fixture='[{"'+$alias+'":"CURRENT_SOURCE","value":65}]'
 $parsed=$build.Invoke($null,[object[]]@($fixture,[ushort]13)).ToString() | ConvertFrom-Json
 if($parsed.variables[0].numeric_candidate -ne 65){throw ('Variable name alias failed: '+$alias)}
}
$unknown=$build.Invoke($null,[object[]]@('[{"unexpected_field":"DO_NOT_EXPORT_PAYLOAD","value":{"private":"DO_NOT_EXPORT_NESTED"},"password":"DO_NOT_EXPORT_SECRET"}]',[ushort]13)).ToString()
if($unknown -match 'DO_NOT_EXPORT|password' -or ($unknown | ConvertFrom-Json).first_entry_shape.properties.Count -ne 2){throw 'Unknown variable shape leaked payload or lost safe structural keys'}
$filtered=$coreType.GetMethod('BuildFilteredRoomVariable',[Reflection.BindingFlags]'NonPublic,Static')
$parsed=$filtered.Invoke($null,[object[]]@('[{"value":"100002","private":"DO_NOT_EXPORT"}]','CURRENT_SOURCE')).ToString() | ConvertFrom-Json
if($parsed.numeric_candidate -ne 100002){throw 'Filtered numeric read failed'}
$hidden=$filtered.Invoke($null,[object[]]@('[{"value":"DO_NOT_EXPORT_TITLE"}]','CURRENT_SOURCE')).ToString()
if($hidden -match 'DO_NOT_EXPORT'){throw 'Filtered string response leaked'}
Write-Output 'Variable schema checks passed: identifier lists, aliases, safe unknown-entry structure and filtered value reads.'
$fixture='[{"id":13,"varName":"CURRENT_AUDIO_SOURCE","type":"NUMBER","hidden":0,"value":"65","variableId":1,"name":"Living Room","roomName":"Living Room"},{"varName":"POWER_STATE","name":"Living Room","value":"1"},{"varName":"ACCESS_TOKEN","name":"CURRENT_SOURCE","value":"DO_NOT_EXPORT_TOKEN"},{"varName":"bad name","name":"CURRENT_SOURCE","value":999}]'
$actualShape=$build.Invoke($null,[object[]]@($fixture,[ushort]13)).ToString()
$decoded=$actualShape | ConvertFrom-Json
if($decoded.exported_variable_count -ne 2 -or $decoded.variables[0].name -ne 'CURRENT_AUDIO_SOURCE' -or $decoded.variables[0].numeric_candidate -ne 65 -or $decoded.variables[1].measured_value -ne 1){throw 'Controller varName/friendly-name precedence incorrect'}
if($actualShape -match 'DO_NOT_EXPORT|999|Living Room'){throw 'Friendly labels or invalid preferred variable identifier leaked'}
Write-Output 'Actual controller entry-layout regression passed: varName takes priority over friendly name; sensitive/invalid identifiers cannot fall back to a misleading name.'
