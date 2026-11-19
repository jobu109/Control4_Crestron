$ErrorActionPreference='Stop'
$release=Join-Path $PSScriptRoot '../Control4Gateway/bin/Release'
Get-ChildItem $release -Filter '*.dll' | ForEach-Object {[void][Reflection.Assembly]::LoadFrom($_.FullName)}
$type=([Reflection.Assembly]::LoadFrom((Join-Path $release 'Control4Gateway.dll'))).GetType('Control4Crestron.GatewayCore',$true)
$method=$type.GetMethod('BuildMediaCommandsForSource',[Reflection.BindingFlags]'NonPublic,Static')
$fixture='[{"command":"SelectPreset","params":[{"name":"Preset","type":"C4:DynamicList","choices":[{"id":1,"label":"HEAVY","token":"PRIVATE_TOKEN"}],"default":"PRIVATE_DEFAULT"},{"name":"Room","type":"integer","required":true}]}]'
$result=$method.Invoke($null,[object[]]@($fixture,200,65)).ToString()
if($result -match 'HEAVY|PRIVATE_TOKEN|PRIVATE_DEFAULT'){throw 'Scalar choice/default data leaked'}
$report=$result | ConvertFrom-Json
$schema=$report.spotify_preset_parameter_schemas[0]
if($schema.type -ne 'Array' -or $schema.count -ne 2 -or $schema.children[0].fields.name.schema_value -ne 'Preset' -or $schema.children[1].fields.name.schema_value -ne 'Room'){throw 'Parameter schema lost'}
if($schema.children[0].fields.choices.count -ne 1 -or !$schema.children[1].fields.required.schema_value){throw 'Choice structure or required flag lost'}
if($schema.children[0].fields.type.schema_value -ne 'C4:DynamicList' -or $schema.children[0].other_field_shapes[0].name -ne 'default'){throw 'Vendor type or omitted field shape lost'}
$other=$method.Invoke($null,[object[]]@($fixture,200,20)).ToString() | ConvertFrom-Json
if($null -ne $other.spotify_preset_parameter_schemas){throw 'Spotify inspection applied to another source'}
$huge='[{"command":"SelectPreset","params":['+((1..400 | ForEach-Object {'{"name":"Preset","options":[1,2,3]}'} ) -join ',')+']}]'
$bounded=$method.Invoke($null,[object[]]@($huge,200,65)).ToString() | ConvertFrom-Json
if($bounded.spotify_preset_parameter_schemas[0].omitted -ne 368){throw 'Array bound failed'}
'Spotify schema checks passed: source scoping, parameter/choice structure, scalar suppression and bounded arrays.'
