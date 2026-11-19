$ErrorActionPreference='Stop'
$release=Join-Path $PSScriptRoot '../Control4Gateway/bin/Release'
Get-ChildItem $release -Filter '*.dll' | ForEach-Object {[void][Reflection.Assembly]::LoadFrom($_.FullName)}
$library=[Reflection.Assembly]::LoadFrom((Join-Path $release 'Control4Gateway.dll'))
$type=$library.GetType('Control4Crestron.GatewayCore',$true)
$flags=[Reflection.BindingFlags]'NonPublic,Static'
$plan=$type.GetMethod('BuildRoomMediaPlan',$flags)
$fixture='[{"command":"SELECT_AUDIO_MEDIA:PLAYLIST","parameters":[{"name":"mediaid","type":"list","URIs":{"GET":"/api/v1/locations/rooms/13/media/playlists?token=DO_NOT_EXPORT","POST":"/api/v1/locations/rooms/13/media/playlists"},"options":[{"id":7,"name":"DO_NOT_EXPORT_TITLE","password":"DO_NOT_EXPORT_PASSWORD"}],"description":"DO_NOT_EXPORT_DESCRIPTION"}]},{"command":"SELECT_VIDEO_MEDIA:MOVIE","parameters":{"mediaid":{"type":"list","links":[{"href":"/api/v1/locations/rooms/13/media/movies"},{"method":"GET","href":"/api/v1/locations/rooms/14/media/movies"},{"method":"GET","href":"https://example.com/api/v1/locations/rooms/13/media/movies"},{"method":"GET","href":"/api/v1/locations/rooms/13/media/unrecognized"}]}}},{"command":"AUTH_LOGIN","parameters":[{"name":"mediaid","uri":"DO_NOT_EXPORT_PRIVATE"}]}]'
$result=$plan.Invoke($null,[object[]]@($fixture,'192.168.1.160',[ushort]13))
$text=$result.ToString(); $data=$text | ConvertFrom-Json
if($text -match 'DO_NOT_EXPORT|example.com|/rooms/14|password|unrecognized'){throw 'Media parameter inspection leaked private or off-scope data'}
if($data.definitions.Count -ne 2 -or $data.advertised_get_paths.Count -ne 1 -or $data.advertised_get_paths[0] -ne '/api/v1/locations/rooms/13/media/playlists'){throw 'GET method/room path gate failed'}
if($data.advertised_paths_method_unknown.Count -ne 1 -or $data.advertised_paths_method_unknown[0] -ne '/api/v1/locations/rooms/13/media/movies'){throw 'Unknown-method catalog path was lost or misclassified'}
$catalog=$type.GetMethod('BuildCatalogResponseInspection',$flags)
$contractFixture='[{"command":"SELECT_AUDIO_MEDIA:BROADCAST_AUDIO","params":[{"name":"mediaid","type":"list","condition":{"params":"PRIVATE_CONDITION"}},{"name":"deselect","type":"BOOLEAN_SELECTOR","default":false}]}]'
$contract=$plan.Invoke($null,[object[]]@($contractFixture,'192.168.1.160',[ushort]13)).ToString()
if($contract -match 'PRIVATE_CONDITION'){throw 'Contract string value leaked'}
$contract=($contract | ConvertFrom-Json).definitions[0].command_contract.fields.params.entries
if($contract[0].fields.condition.fields.params.type -ne 'String' -or $contract[1].fields.type.schema_value -ne 'BOOLEAN_SELECTOR' -or $contract[1].fields.default.schema_value -ne $false){throw 'Condition shape/deselect contract lost'}
$report=$catalog.Invoke($null,[object[]]@('{"items":[{"mediaid":"100002","name":"DO_NOT_EXPORT_TITLE","token":"DO_NOT_EXPORT_TOKEN"},{"id":"DO_NOT_EXPORT_OPAQUE","artwork":"DO_NOT_EXPORT_URL"}],"private":"DO_NOT_EXPORT_PRIVATE"}')).ToString()
$parsed=$report | ConvertFrom-Json
if($report -match 'DO_NOT_EXPORT' -or !$parsed.list_recognized -or $parsed.item_count -ne 2 -or $parsed.sample_numeric_ids[0] -cne '100002'){throw 'Catalog response schema/privacy/large numeric ID failed'}
$empty=$catalog.Invoke($null,[object[]]@('[]')).ToString() | ConvertFrom-Json
if(!$empty.list_recognized -or $empty.item_count -ne 0){throw 'Empty catalog not distinguished from failure'}
$unknown=$catalog.Invoke($null,[object[]]@('{"unexpected":"DO_NOT_EXPORT"}')).ToString() | ConvertFrom-Json
if($unknown.list_recognized -or $null -ne $unknown.item_count){throw 'Unknown schema presented as catalog'}
$bad=$false
try{[void]$plan.Invoke($null,[object[]]@('{}','192.168.1.160',[ushort]13))}catch{$bad=$true}
if(!$bad){throw 'Unknown command schema accepted'}
$core=[Activator]::CreateInstance($type,[object[]]@([ushort]62017))
$core.InspectRoomMediaCatalog(13)
if($type.GetField('_busy',[Reflection.BindingFlags]'NonPublic,Instance').GetValue($core) -ne 0){throw 'Offline media inspection queued network work'}
Write-Output 'Room media inspection checks passed: mediaid metadata, explicit GET/unknown-method distinction, same-room paths, private-field suppression, catalog/empty/unknown shapes, large numeric IDs and offline no-I/O.'
