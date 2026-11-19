. (Join-Path $PSScriptRoot 'ReadRoomUiFrames.ps1')
$ErrorActionPreference='Stop'
$release=Join-Path $PSScriptRoot '../Control4Gateway/bin/Release'
Get-ChildItem $release -Filter '*.dll' | ForEach-Object {[void][Reflection.Assembly]::LoadFrom($_.FullName)}
$library=[Reflection.Assembly]::LoadFrom((Join-Path $release 'Control4Gateway.dll'))
$coreType=$library.GetType('Control4Crestron.GatewayCore',$true)
$type=$library.GetType('Control4Crestron.Control4_Room',$true)
$flags=[Reflection.BindingFlags]'NonPublic,Instance'
$static=[Reflection.BindingFlags]'NonPublic,Static'
$build=$coreType.GetMethod('BuildRoomCatalog',$static)
$fixture='[{"id":1,"title":"First playlist","token":"DO_NOT_EXPORT"},{"mediaid":"9007199254740993","name":"Quoted \"name\"\nnext","profile":"DO_NOT_EXPORT"},{"id":1,"name":"duplicate"},{"id":"invalid","name":"invalid"},{"id":3}]'
$report=$build.Invoke($null,[object[]]@($fixture,[ushort]13,'playlists'))
$text=$report.ToString(); $data=$text | ConvertFrom-Json
if($text -match 'DO_NOT_EXPORT|profile|token' -or $data.total -ne 3 -or $data.omitted -ne 2 -or $data.unnamed -ne 1){throw 'Catalog normalization/privacy/counts failed'}
if($data.items[0].name -ne 'First playlist' -or $data.items[1].id -cne '9007199254740993' -or $data.items[1].name -ne 'Quoted "name" next'){throw 'Catalog labels, escaping or large IDs failed'}
if($data.selection_supported -ne $false -or $data.scope -ne 'native_room_catalog_not_service_library'){throw 'Catalog scope/playback claim incorrect'}
$empty=$build.Invoke($null,[object[]]@('[]',[ushort]13,'movies')).ToString() | ConvertFrom-Json
if($empty.total -ne 0 -or $empty.items.Count -ne 0){throw 'Empty catalog not recognized'}
$bad=$false
try{[void]$build.Invoke($null,[object[]]@('{}',[ushort]13,'playlists'))}catch{$bad=$true}
if(!$bad){throw 'Unknown catalog accepted'}
$path=$coreType.GetMethod('RoomCatalogPath',$static)
if($path.Invoke($null,[object[]]@([ushort]13,'playlists')) -ne '/api/v1/locations/rooms/13/media/albums/playlists'){throw 'Wrong observed playlist route'}
$module=[Activator]::CreateInstance($type)
[void]$module.Initialize(62018,13)
$registry=$library.GetType('Control4Crestron.GatewayRegistry',$true)
$core=$registry.GetMethod('Get').Invoke($null,[object[]]@([ushort]62018))
$script:pages=[Collections.Generic.List[object]]::new()
$event=$type.GetEvent('MediaPageChanged')
$handler=[Management.Automation.LanguagePrimitives]::ConvertTo([scriptblock]{param($sender,$feedback) $script:pages.Add($feedback)},$event.EventHandlerType)
$event.AddEventHandler($module,$handler)
$module.BrowsePlaylists()
if($coreType.GetField('_busy',$flags).GetValue($core) -ne 0 -or $script:pages[-1].Valid -ne 0){throw 'Offline browsing queued I/O or asserted valid list'}
$coreType.GetField('_online',$flags).SetValue($core,$true)
$publish=$coreType.GetMethod('PublishRoomCatalog',$flags)
[void]$publish.Invoke($core,[object[]]@([ushort]13,'playlists',$report))
$packet=Read-RoomUiDocument $module 'm' {$script:pages[-1].Json}
if(!$packet.valid -or !$packet.partial -or $packet.total -ne 3 -or $packet.revision -ne $data.revision -or $packet.items[1].id -cne '9007199254740993'){throw 'Media widget packet or partial flag incorrect'}
$count=$script:pages.Count
[void]$publish.Invoke($core,[object[]]@([ushort]14,'playlists',$report))
[void]$publish.Invoke($core,[object[]]@([ushort]13,'albums',$report))
if($script:pages.Count -ne $count){throw 'Wrong room or superseded media kind changed active widget'}
$long=@(1..40 | ForEach-Object {[ordered]@{id=$_;name=('x'*500)}})
$longJson=ConvertTo-Json -InputObject $long -Compress
$many=$build.Invoke($null,[object[]]@([string]$longJson,[ushort]13,'playlists'))
[void]$publish.Invoke($core,[object[]]@([ushort]13,'playlists',$many))
$first=$script:pages[-1]
if($first.Pages -le 1 -or $first.Count -ne 40){throw 'Long media list was not paged'}
$decoded=Read-RoomUiDocument $module 'm' {$script:pages[-1].Json}
if($decoded.items.Count -ne 40){throw 'Media framing dropped items'}
$module.SetMediaPage(65535)
if($script:pages[-1].Page -ne $first.Pages){throw 'Media page clamp failed'}
$rooms=$coreType.GetField('_audioRooms',$flags).GetValue($core)
$coreType.GetField('_generation',$flags).SetValue($core,4)
$coreType.GetField('_feedbackEpoch',$flags).SetValue($core,3)
$works=$coreType.GetField('_roomCatalogWork',$flags).GetValue($core)
$work=[object]::new(); $works[[ushort]13]=$work
$current=$coreType.GetMethod('CatalogWorkCurrent',$flags)
if(!$current.Invoke($core,[object[]]@([ushort]13,$rooms[[ushort]13],4,3,$work))){throw 'Current media work rejected'}
if($current.Invoke($core,[object[]]@([ushort]13,$rooms[[ushort]13],4,2,$work))){throw 'Old connection media work accepted'}
$works[[ushort]13]=[object]::new()
if($current.Invoke($core,[object[]]@([ushort]13,$rooms[[ushort]13],4,3,$work))){throw 'Superseded media work accepted'}
[void]$coreType.GetMethod('StopRealtime',$flags).Invoke($core,@())
if($script:pages[-1].Valid -ne 0 -or $script:pages[-1].Count -ne 0){throw 'Disconnect retained valid content list'}
$event.RemoveEventHandler($module,$handler)
Write-Output 'Native catalog checks passed: labels/IDs/privacy, empty/unknown lists, native read-only scope, widget revisions/partial flags, lossless bounded paging, room/kind isolation, stale-work rejection and disconnect clearing.'
