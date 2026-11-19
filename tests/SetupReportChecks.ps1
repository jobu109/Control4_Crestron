$ErrorActionPreference='Stop'
$release=Join-Path $PSScriptRoot '../Control4Gateway/bin/Release'
Get-ChildItem $release -Filter '*.dll' | ForEach-Object {[void][Reflection.Assembly]::LoadFrom($_.FullName)}
$library=[Reflection.Assembly]::LoadFrom((Join-Path $release 'Control4Gateway.dll'))
$type=$library.GetType('Control4Crestron.GatewayCore',$true)
$jsonAssembly=[Reflection.Assembly]::LoadFrom((Join-Path $release 'SimplSharpNewtonsoft.dll'))
$parse=$jsonAssembly.GetType('Newtonsoft.Json.Linq.JToken').GetMethod('Parse',[type[]]@([string]))
$build=$type.GetMethod('BuildSetupReport',[Reflection.BindingFlags]'NonPublic,Static')
$inventory=$parse.Invoke($null,[object[]]@('{"all_items":[{"item_id":13,"name":"Living Room","type":4},{"item_id":682,"name":"TV","type":7},{"item_id":340,"name":"Audio Matrix Switch","type":7},{"item_id":65,"name":"Spotify","type":7},{"item_id":437,"name":"Hall Light","type":7}],"categories":{"lights":[{"item_id":437,"name":"Hall Light","type":7}]},"errors":[]}'))
$mappings=$parse.Invoke($null,[object[]]@('[{"room_id":13,"watch_id":682,"listen_id":340,"listen_output_id":4003}]'))
$ui='{"token":"DO_NOT_EXPORT","experiences":{"experience":[{"type":"listen","room_id":13,"sources":{"source":[{"id":65,"name":"","account":"DO_NOT_EXPORT"},{"id":100002,"name":"Digital Media"},{"id":65},{"id":0}]}},{"type":"watch","room_id":14,"sources":{"source":{"id":542,"name":"SHIELD"}}},{"type":"listen","room_id":14,"sources":{"source":[{"id":20,"name":"TuneIn"}]}}]}}'
$report=$build.Invoke($null,[object[]]@($inventory,$ui,$mappings,[ushort]1))
$serialized=$report.ToString()
$data=$serialized | ConvertFrom-Json
if($serialized -match 'DO_NOT_EXPORT|account|token' -or $data.rooms.Count -ne 2){throw 'Privacy or room discovery failed'}
$living=$data.rooms | Where-Object room_id -eq 13
$other=$data.rooms | Where-Object room_id -eq 14
if($living.listen_sources.Count -ne 2 -or $living.listen_sources[0].name -ne 'Spotify' -or $living.listen_sources[1].'Source_ID$' -ne '100002'){throw 'Name fallback, deduplication or large source ID failed'}
if($living.audio_video_parameters.Listen_Output_ID -ne 4003 -or $living.endpoint_status -ne 'configured_in_program_not_automatically_verified'){throw 'Configured mapping status lost'}
if($other.endpoint_status -ne 'unresolved' -or $other.audio_video_parameters){throw 'Unconfigured room inherited mapping'}
if($data.category_candidates.lights[0].candidate_module_parameters.Item_ID -ne 437 -or $data.readable_text -notmatch 'Audio Matrix Switch' -or $data.readable_text -notmatch 'Watch_Feedback_ID=682'){throw 'Readable parameter guidance missing'}
if($data.omitted_source_entries -ne 1 -or $data.completeness -ne 'partial'){throw 'Partial discovery not explicit'}
foreach($bad in @('bad','[]','{}','{"experiences":"unexpected"}')){
 $failed=$build.Invoke($null,[object[]]@($inventory,$bad,$mappings,[ushort]1)).ToString() | ConvertFrom-Json
 if($failed.source_discovery_status -ne 'unrecognized_format' -or $failed.devices.Count -ne 5){throw 'Bad UI lost usable inventory or claimed success'}
}
$missing=$build.Invoke($null,[object[]]@($inventory,$null,$mappings,[ushort]1)).ToString() | ConvertFrom-Json
if($missing.source_discovery_status -ne 'unavailable' -or $missing.warnings.Count -eq 0){throw 'Missing discovery not reported'}
$flat='{"experiences":[{"type":"watch","room_id":"13","sources":[{"id":"2061","name":"DIRECTV\nStream"}]}]}'
$flatReport=$build.Invoke($null,[object[]]@($inventory,$flat,$mappings,[ushort]1)).ToString() | ConvertFrom-Json
if($flatReport.rooms[0].watch_sources[0].name -ne 'DIRECTV Stream'){throw 'Flat UI shape or readable name sanitation failed'}
Write-Output 'Setup report checks passed: room/source shapes, names, large IDs, deduplication, privacy allowlist, configured/unresolved mappings, exact parameter guidance and partial failures.'
