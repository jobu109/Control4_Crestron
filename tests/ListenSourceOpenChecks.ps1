$ErrorActionPreference='Stop'
$release=Join-Path $PSScriptRoot '../Control4Gateway/bin/Release'
Get-ChildItem $release -Filter '*.dll' | ForEach-Object {[void][Reflection.Assembly]::LoadFrom($_.FullName)}
$assembly=[Reflection.Assembly]::LoadFrom((Join-Path $release 'Control4Gateway.dll'))
$coreType=$assembly.GetType('Control4Crestron.GatewayCore',$true)
$moduleType=$assembly.GetType('Control4Crestron.Control4_Room',$true)
$static=[Reflection.BindingFlags]'NonPublic,Static';$instance=[Reflection.BindingFlags]'NonPublic,Instance'
$probe=$coreType.GetMethod('HasSourceOwnedMedia',$static)
$json='[{"id":171,"device_id":65,"location":"opaque","name":"HEAVY"},{"id":357,"device_id":1648,"location":"ST:0:1","name":"Station"}]'
foreach($case in @(@(65,$true),@(1648,$true),@(2211,$false),@(100002,$false))){if($probe.Invoke($null,[object[]]@($json,[int]$case[0])) -ne $case[1]){throw 'Source-owned content classification failed'}}
if(!$probe.Invoke($null,[object[]]@('{"items":[{"device_id":"65"}]}',65))){throw 'Object catalog/string ID not recognized'}
foreach($bad in @('{}','null','not-json')){$rejected=$false;try{[void]$probe.Invoke($null,[object[]]@($bad,65))}catch{$rejected=$true};if(!$rejected){throw 'Unknown catalog allowed source route'}}
$module=[Activator]::CreateInstance($moduleType);[void]$module.Initialize(62040,13)
$core=$assembly.GetType('Control4Crestron.GatewayRegistry').GetMethod('Get').Invoke($null,[object[]]@([ushort]62040))
$coreType.GetField('_generation',$instance).SetValue($core,2);$coreType.GetField('_feedbackEpoch',$instance).SetValue($core,3);$coreType.GetField('_online',$instance).SetValue($core,$true)
$rooms=$coreType.GetField('_audioRooms',$instance).GetValue($core);$room=$rooms[[ushort]13]
$work=[object]::new();$open=$coreType.GetField('_listenOpenWork',$instance).GetValue($core);$open[[ushort]13]=$work
$current=$coreType.GetMethod('ListenOpenCurrent',$instance)
if(!$current.Invoke($core,[object[]]@([ushort]13,$room,2,3,$work))){throw 'Current Listen opening rejected'}
if($current.Invoke($core,[object[]]@([ushort]13,$room,1,3,$work))){throw 'Stale generation allowed'}
if($current.Invoke($core,[object[]]@([ushort]13,$room,2,2,$work))){throw 'Stale connection allowed'}
$open[[ushort]13]=[object]::new();if($current.Invoke($core,[object[]]@([ushort]13,$room,2,3,$work))){throw 'Superseded source open allowed'}
[void]$coreType.GetMethod('ClearRoomMediaForSourceChange',$instance).Invoke($core,[object[]]@([ushort]13));if($open.ContainsKey([ushort]13)){throw 'Actual source change retained stale browse intent'}
$coreType.GetField('_online',$instance).SetValue($core,$false)
$script:diagnostics=[Collections.Generic.List[string]]::new();$event=$moduleType.GetEvent('DiagnosticReceived');$handler=[Management.Automation.LanguagePrimitives]::ConvertTo([scriptblock]{param($s,$e)$script:diagnostics.Add($e.Message)},$event.EventHandlerType);$event.AddEventHandler($module,$handler)
$tokens=[Reflection.Assembly]::LoadFrom((Join-Path $release 'SimplSharpNewtonsoft.dll')).GetType('Newtonsoft.Json.Linq.JToken');$parse=$tokens.GetMethod('Parse',[type[]]@([string]))
$list=$parse.Invoke($null,[object[]]@('[{"source_id":65,"Source_ID$":"65","name":"Renamed source"}]'))
$moduleType.GetField('_listen',$instance).SetValue($module,$list);$moduleType.GetField('_sourcesValid',$instance).SetValue($module,$true)
$module.SelectAudioSourceId('65');$module.SelectAudioSourceIndex(1);$module.SelectAudioSourceName('Renamed source')
if(@($script:diagnostics | Where-Object {$_ -eq 'Connect and initialize Sources before opening a Listen source'}).Count -ne 3){throw 'ID/index/name did not share browse-first opening'}
if($coreType.GetField('_busy',$instance).GetValue($core) -ne 0){throw 'Offline source opening queued network activity'}
$event.RemoveEventHandler($module,$handler)
'Listen source opening checks passed: source-owned catalog decisions, renamed/unknown IDs, fail-closed schemas, generation/session/intent guards, shared ID/index/name path and offline no-I/O'
