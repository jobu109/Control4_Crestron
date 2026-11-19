. (Join-Path $PSScriptRoot 'ReadRoomUiFrames.ps1')
$ErrorActionPreference='Stop'
$release=Join-Path $PSScriptRoot '../Control4Gateway/bin/Release'
Get-ChildItem $release -Filter '*.dll' | ForEach-Object {[void][Reflection.Assembly]::LoadFrom($_.FullName)}
$library=[Reflection.Assembly]::LoadFrom((Join-Path $release 'Control4Gateway.dll'))
$coreType=$library.GetType('Control4Crestron.GatewayCore',$true)
$type=$library.GetType('Control4Crestron.Control4_Room',$true)
$flags=[Reflection.BindingFlags]'NonPublic,Instance'
$static=[Reflection.BindingFlags]'NonPublic,Static'
$metadataReport=Get-Content (Join-Path $PSScriptRoot 'fixtures/Room13Commands_2026-11-15.json') -Raw | ConvertFrom-Json
# The historical normalized fixture omitted valueType/value. Supply explicit
# synthetic declarations to test serialization; these are not hardware evidence.
$raw=@($metadataReport.commands | ForEach-Object {
 $parameters=@($_.parameter_definitions | ForEach-Object {
  if($_.name -eq 'mediaid'){@{name='mediaid';type='list';valueType='integer'}}
  elseif($_.name -eq 'deselect'){@{name='deselect';valueType='boolean';value='false'}}
  else{$_}
 })
 [ordered]@{command=$_.command_name;parameters=$parameters}
})
$metadata=ConvertTo-Json -InputObject $raw -Depth 12 -Compress
$build=$coreType.GetMethod('BuildNativeMediaSelection',$static)
foreach($case in @(@('playlists','SELECT_AUDIO_MEDIA:PLAYLIST'),@('albums','SELECT_AUDIO_MEDIA:ALBUM'),@('audio_stations','SELECT_AUDIO_MEDIA:BROADCAST_AUDIO'),@('movies','SELECT_VIDEO_MEDIA:MOVIE'),@('video_stations','SELECT_VIDEO_MEDIA:BROADCAST_VIDEO'))){
 $body=$build.Invoke($null,[object[]]@([string]$metadata,[ushort]13,$case[0],'1')) | ConvertFrom-Json
 if(!$body.async -or $body.command -ne $case[1] -or $body.tParams.mediaid -is [array] -or $body.tParams.mediaid -ne 1 -or $body.tParams.deselect -ne $false -or @($body.tParams.psobject.Properties).Count -ne 2){throw 'Declared media type/fixed argument mapping incorrect'}
}
$heavy=$build.Invoke($null,[object[]]@([string]$metadata,[ushort]13,'audio_stations','171'))
if($heavy -notmatch '"mediaid":171' -or $heavy -notmatch '"deselect":false'){throw 'Declared numeric/fixed boolean values not serialized'}
$stringMetadata='[{"command":"SELECT_AUDIO_MEDIA:BROADCAST_AUDIO","params":[{"name":"mediaid","type":"LIST","valueType":"string"},{"name":"deselect","valueType":"string","value":"0"}]}]'
$stringBody=$build.Invoke($null,[object[]]@($stringMetadata,[ushort]13,'audio_stations','171')) | ConvertFrom-Json
if($stringBody.tParams.mediaid -isnot [string] -or $stringBody.tParams.deselect -cne '0'){throw 'Declared string values changed type'}
foreach($bad in @('0','-1','1;ROOM_OFF','not-an-id')){
 $failed=$false
 try{[void]$build.Invoke($null,[object[]]@([string]$metadata,[ushort]13,'playlists',$bad))}catch{$failed=$true}
 if(!$failed){throw 'Invalid native ID accepted'}
}
foreach($badMetadata in @('[]','[{"command":"SELECT_AUDIO_MEDIA:PLAYLIST"}]','[{"command":"SELECT_AUDIO_MEDIA:PLAYLIST","parameters":[{"name":"mediaid","type":"string"}]}]','[{"command":"SELECT_AUDIO_MEDIA:PLAYLIST","parameters":[{"name":"mediaid","type":"list"},{"name":"deselect","required":true}]}]','[{"command":"SELECT_AUDIO_MEDIA:PLAYLIST","parameters":[{"name":"mediaid","type":"list"},{"name":"UNESTABLISHED"}]}]')){
 $failed=$false
 try{[void]$build.Invoke($null,[object[]]@($badMetadata,[ushort]13,'playlists','1'))}catch{$failed=$true}
 if(!$failed){throw 'Unknown media argument contract accepted'}
}
$fixture=Get-Content (Join-Path $PSScriptRoot 'fixtures/Room13NativePlaylists_2026-11-16.json') -Raw | ConvertFrom-Json
$fixture=@($fixture)[0]
$jsonAssembly=[Reflection.Assembly]::LoadFrom((Join-Path $release 'SimplSharpNewtonsoft.dll'))
$parse=$jsonAssembly.GetType('Newtonsoft.Json.Linq.JToken').GetMethod('Parse',[type[]]@([string]))
$catalogJson=ConvertTo-Json -InputObject $fixture -Depth 12 -Compress
$catalog=$parse.Invoke($null,[object[]]@([string]$catalogJson))
$catalog['selection_supported']=$true
$contains=$coreType.GetMethod('CatalogContainsMedia',$static)
foreach($case in @(@('playlists',[string]$fixture.revision,'1',$true),@('playlists',[string]$fixture.revision,'2',$true),@('playlists',[string]$fixture.revision,'65',$false),@('albums',[string]$fixture.revision,'1',$false),@('playlists','stale','1',$false))){
 if($contains.Invoke($null,[object[]]@($catalog,$case[0],$case[1],$case[2])) -ne $case[3]){throw 'Media ID/kind/revision membership incorrect'}
}
$module=[Activator]::CreateInstance($type)
[void]$module.Initialize(62019,13)
$registry=$library.GetType('Control4Crestron.GatewayRegistry',$true)
$core=$registry.GetMethod('Get').Invoke($null,[object[]]@([ushort]62019))
$module.BrowsePlaylists()
$script:mediaPages=[Collections.Generic.List[object]]::new()
$event=$type.GetEvent('MediaPageChanged')
$handler=[Management.Automation.LanguagePrimitives]::ConvertTo([scriptblock]{param($sender,$feedback) $script:mediaPages.Add($feedback)},$event.EventHandlerType)
$event.AddEventHandler($module,$handler)
[void]$coreType.GetMethod('PublishRoomCatalog',$flags).Invoke($core,[object[]]@([ushort]13,'playlists',$catalog))
if($script:mediaPages[-1].SelectionAvailable -ne 1 -or !(Read-RoomUiDocument $module 'm' {$script:mediaPages[-1].Json}).selection_supported){throw 'Selection readiness not forwarded'}
$resolve=$type.GetMethod('TryMediaSelection',$flags)
foreach($case in @(@('0001','playlists',[string]$fixture.revision,$true),@('65','playlists',[string]$fixture.revision,$false),@('1','albums',[string]$fixture.revision,$false),@('1','playlists','old',$false))){
 $args=[object[]]@($case[0],$case[1],$case[2],$null,$null,$null)
 if($resolve.Invoke($module,$args) -ne $case[3]){throw 'Widget selection validation failed'}
}
$module.SelectMediaId('1')
$module.SelectMediaIndex(2)
$request=@{room_id=13;kind='playlists';revision=[string]$fixture.revision;id='1'} | ConvertTo-Json -Compress
$module.SelectMediaRequest($request)
$module.SelectMediaRequest('bad JSON')
$module.SelectMediaRequest('{"room_id":14,"kind":"playlists","revision":"old","id":"1"}')
if($coreType.GetField('_busy',$flags).GetValue($core) -ne 0){throw 'Offline selection queued I/O'}
[void]$coreType.GetMethod('StopRealtime',$flags).Invoke($core,@())
if($script:mediaPages[-1].SelectionAvailable -ne 0){throw 'Disconnect retained media selection readiness'}
$event.RemoveEventHandler($module,$handler)
Write-Output 'Native selection checks passed: observed command mappings, strict arguments/IDs, exact kind/revision membership, widget readiness/validation, malformed requests and offline/disconnect rejection.'
