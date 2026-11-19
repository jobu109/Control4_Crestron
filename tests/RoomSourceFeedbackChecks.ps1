$ErrorActionPreference='Stop'
$release=Join-Path $PSScriptRoot '../Control4Gateway/bin/Release'
Get-ChildItem $release -Filter '*.dll' | ForEach-Object {[void][Reflection.Assembly]::LoadFrom($_.FullName)}
$library=[Reflection.Assembly]::LoadFrom((Join-Path $release 'Control4Gateway.dll'))
$coreType=$library.GetType('Control4Crestron.GatewayCore',$true)
$type=$library.GetType('Control4Crestron.Control4_Room',$true)
$flags=[Reflection.BindingFlags]'NonPublic,Instance'
$static=[Reflection.BindingFlags]'NonPublic,Static'
$parseState=$coreType.GetMethod('ParseRoomSourceState',$static)
$snapshots=Get-Content (Join-Path $PSScriptRoot 'fixtures/Room13SourceStates_2026-11-15.json') -Raw | ConvertFrom-Json
$states=[Collections.Generic.List[object]]::new()
foreach($snapshot in $snapshots){
 $rows=@($snapshot.variables | Where-Object name -in 'POWER_STATE','CURRENT_AUDIO_DEVICE','CURRENT_VIDEO_DEVICE','PLAYING_AUDIO_DEVICE' | ForEach-Object {
   $value=if($_.name -eq 'POWER_STATE'){$_.measured_value}else{$_.numeric_candidate}
   if($_.value_type -eq 'String'){$value=[string]$value}
   [ordered]@{varName=$_.name;name='Living Room';value=$value}
 })
 $json=ConvertTo-Json -InputObject $rows -Compress
 $states.Add($parseState.Invoke($null,[object[]]@([string]$json,[ushort]13)))
}
if($states.Count -ne 3 -or $states[0].AudioValid -ne 1 -or $states[0].VideoValid -ne 1 -or $states[0].AudioId -ne 0){throw 'Measured Off state failed'}
if($states[1].AudioId -ne 65 -or $states[1].AudioValid -ne 1 -or $states[1].VideoId -ne 0){throw 'Spotify service confused with generic renderer100002'}
if($states[2].VideoId -ne 2061 -or $states[2].VideoValid -ne 1 -or $states[2].AudioId -ne 0 -or $states[2].AudioValid -ne 1){throw 'Measured video/none state failed'}
foreach($bad in @('[]','[{"varName":"POWER_STATE","value":"Undefined"}]','[{"varName":"POWER_STATE","value":1},{"varName":"POWER_STATE","value":0}]')){
 $state=$parseState.Invoke($null,[object[]]@($bad,[ushort]13))
 if($state.AudioValid -ne 0 -or $state.VideoValid -ne 0){throw 'Unknown or duplicate power asserted valid selection'}
}
$unresolved='[{"varName":"POWER_STATE","value":1},{"varName":"CURRENT_AUDIO_DEVICE","value":100002},{"varName":"PLAYING_AUDIO_DEVICE","value":0},{"varName":"CURRENT_VIDEO_DEVICE","value":2061}]'
$state=$parseState.Invoke($null,[object[]]@($unresolved,[ushort]13))
if($state.AudioValid -ne 0 -or $state.VideoValid -ne 1){throw 'Unresolved renderer treated as exact source or invalidated unrelated video'}
$module=[Activator]::CreateInstance($type)
[void]$module.Initialize(62016,13)
$registry=$library.GetType('Control4Crestron.GatewayRegistry',$true)
$core=$registry.GetMethod('Get').Invoke($null,[object[]]@([ushort]62016))
$coreType.GetField('_generation',$flags).SetValue($core,4)
$coreType.GetField('_feedbackEpoch',$flags).SetValue($core,3)
$coreType.GetField('_online',$flags).SetValue($core,$true)
$jsonAssembly=[Reflection.Assembly]::LoadFrom((Join-Path $release 'SimplSharpNewtonsoft.dll'))
$parse=$jsonAssembly.GetType('Newtonsoft.Json.Linq.JToken').GetMethod('Parse',[type[]]@([string]))
$discovery=$parse.Invoke($null,[object[]]@('{"source_discovery_status":"discovered","rooms":[{"room_id":13,"name":"Living Room","watch_sources":[{"source_id":545,"name":"Netflix","Source_ID$":"545"},{"source_id":2061,"name":"DIRECTV","Source_ID$":"2061"}],"listen_sources":[{"source_id":20,"name":"TuneIn","Source_ID$":"20"},{"source_id":65,"name":"Spotify","Source_ID$":"65"},{"source_id":100002,"name":"Digital Media","Source_ID$":"100002"}]}]}'))
$coreType.GetField('_roomDiscovery',$flags).SetValue($core,$discovery)
$coreType.GetField('_roomDiscoveryGeneration',$flags).SetValue($core,4)
$coreType.GetField('_roomDiscoveryEpoch',$flags).SetValue($core,3)
$coreType.GetMethod('PublishRoomDiscovery',$flags).Invoke($core,[object[]]@([ushort]13))
$rooms=$coreType.GetField('_audioRooms',$flags).GetValue($core)
$room=$rooms[[ushort]13]
$roomType=$room.GetType()
$workType=$coreType.GetNestedType('AudioQueryWork',[Reflection.BindingFlags]'NonPublic')
$work=[Activator]::CreateInstance($workType,$true)
$workType.GetField('Generation',$flags).SetValue($work,4)
$workType.GetField('Epoch',$flags).SetValue($work,3)
$workType.GetField('Configuration',$flags).SetValue($work,$room)
$event=$type.GetEvent('SelectedSlotsChanged')
$script:selected=[Collections.Generic.List[object]]::new()
$handler=[Management.Automation.LanguagePrimitives]::ConvertTo([scriptblock]{param($sender,$feedback) $script:selected.Add($feedback)},$event.EventHandlerType)
$event.AddEventHandler($module,$handler)
$apply=$coreType.GetMethod('ApplyRoomSourceFeedback',$flags)
[void]$apply.Invoke($core,[object[]]@([ushort]13,$work,$states[1],0))
if($script:selected[-1].AudioIndex -ne 2 -or $script:selected[-1].AudioValid -ne 1 -or $script:selected[-1].VideoIndex -ne 0 -or $script:selected[-1].VideoValid -ne 1){throw 'Measured service ID was not resolved to source slot'}
$count=$script:selected.Count
$roomType.GetField('SourceVersion',$flags).SetValue($room,1)
[void]$apply.Invoke($core,[object[]]@([ushort]13,$work,$states[2],0))
if($script:selected.Count -ne $count){throw 'Older source read overwrote newer event version'}
$workType.GetField('Epoch',$flags).SetValue($work,2)
[void]$apply.Invoke($core,[object[]]@([ushort]13,$work,$states[2],1))
if($script:selected.Count -ne $count){throw 'Stale-session source result accepted'}
$workType.GetField('Epoch',$flags).SetValue($work,3)
[void]$apply.Invoke($core,[object[]]@([ushort]13,$work,$states[2],1))
if($script:selected[-1].VideoIndex -ne 2 -or $script:selected[-1].VideoValid -ne 1 -or $script:selected[-1].AudioIndex -ne 0){throw 'Measured video slot incorrect'}
[void]$module.ConfigureSourceSlots(1,15)
if($script:selected[-1].VideoValid -ne 0 -or $script:selected[-1].VideoIndex -ne 0){throw 'Source beyond defined slots asserted valid slot'}
[void]$module.ConfigureSourceSlots(15,15)
if($script:selected[-1].VideoIndex -ne 2){throw 'Capacity change failed to remap measured source'}
$coreType.GetField('_online',$flags).SetValue($core,$false)
$count=$script:selected.Count
$module.SelectAudioSourceIndex(1)
if($script:selected.Count -ne $count -or $script:selected[-1].VideoIndex -ne 2){throw 'Requested source echoed into actual feedback'}
[void]$coreType.GetMethod('InvalidateRoomSource',$flags).Invoke($core,[object[]]@([ushort]13,$work))
if($script:selected[-1].AudioValid -ne 0 -or $script:selected[-1].VideoValid -ne 0){throw 'Source-changing command invalidation retained validity'}
[void]$coreType.GetMethod('StopRealtime',$flags).Invoke($core,@())
if($script:selected[-1].AudioValid -ne 0 -or $script:selected[-1].VideoValid -ne 0){throw 'Disconnect retained selected-source validity'}
$event.RemoveEventHandler($module,$handler)
Write-Output 'Measured source checks passed: captured Off/Spotify/DIRECTV semantics, renderer/service distinction, independent validity, slot mapping/capacity, stale reads, no command echo and disconnect invalidation.'
