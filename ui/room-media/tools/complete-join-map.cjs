// Complete scalar UI bindings from the actual module interfaces.
const fs=require('node:fs'),vm=require('node:vm'),path=require('node:path');
const base=path.resolve(__dirname,'..'),workspace=path.resolve(base,'../..');const ctx={window:{}};vm.createContext(ctx);vm.runInContext(fs.readFileSync(path.join(base,'join-map.js'),'utf8'),ctx);const c=ctx.window.ROOM_MEDIA_CONFIG;
const previous=JSON.parse(JSON.stringify(c));
const records=[],omitted=[];const known=new Map();
function add(module,direction,type,pin,key,join){const row={module,direction,type,pin,key,join:String(join)};records.push(row);known.set(`${module}|${direction}|${pin}`,row);}
const send=c.send,r=c.receive;
r.progress='17';add('Sources','receive','s','Now_Playing_Progress_JSON$','progress',r.progress);
send.nowPlayingPage='20';r.nowPlaying='16';r.nowPlayingValid='99';
add('Sources','send','n','Now_Playing_Page_Set','nowPlayingPage',send.nowPlayingPage);
add('Sources','receive','s','Now_Playing_JSON$','nowPlaying',r.nowPlaying);
add('Sources','receive','b','Now_Playing_Valid_FB','nowPlayingValid',r.nowPlayingValid);
const playingJoins={'Sources|receive|Now_Playing_Page_FB':'20','Sources|receive|Now_Playing_Pages_FB':'21'};
for(const [pin,key,type]of [['Source_ID$','sourceId','s'],['Media_Select_Request$','mediaRequest','s'],['Volume_Set','volume','n'],['Media_List_Page_Set','mediaPage','n'],['Audio_List_Page_Set','audioPage','n'],['Video_List_Page_Set','videoPage','n'],['Select_Audio_ID_btn','selectAudio','b'],['Select_Video_ID_btn','selectVideo','b'],['Mute_On_btn','muteOn','b'],['Mute_Off_btn','muteOff','b'],['Room_Off_btn','off','b'],['Pandora_Shuffle_On','shuffle','b']])add('Sources','send',type,pin,key,send[key]);
for(const [pin,key,type]of [['Audio_Source_List_JSON$','audioSources','s'],['Video_Source_List_JSON$','videoSources','s'],['Media_List_JSON$','mediaList','s'],['Diagnostic$','diagnostic','s'],['Room_Name$','roomName','s'],['Sources_Valid_FB','sourceValid','b'],['Media_List_Valid_FB','mediaValid','b'],['Media_Select_Available_FB','selectReady','b'],['Mute_FB','muted','b'],['Volume_Valid_FB','volumeValid','b'],['Power_FB','power','b'],['Volume_FB','volume','n']])add('Sources','receive',type,pin,key,r[key]);
add('Gateway','receive','b','Connected_FB','connected',r.connected);
for(const [key,join]of Object.entries(send.transport))add('Transport','send','b',key+'_btn','transport.'+key,join);
add('Transport','receive','s','Key_Capabilities_JSON$','transport',r.transport);
// Legitimate matching-function analog pairs; unrelated inputs/outputs stay distinct.
const pairs={'Sources|receive|Media_List_Page_FB':c.send.mediaPage,'Sources|receive|Audio_List_Page_FB':c.send.audioPage,'Sources|receive|Video_List_Page_FB':c.send.videoPage};
const occupied={b:new Set(),n:new Set(),s:new Set()};for(const row of [...records,...(previous.bindings||[])])occupied[row.type].add(Number(row.join));
const types={DIGITAL:'b',ANALOG:'n',STRING:'s'};c.send.sources={};c.send.transportSetup={};c.receive.sources={};c.receive.transportStatus={};c.extraReceive=[];
const privatePins=new Set(['Select_Service_Preset_btn','Service_Preset_Driver_ID','Service_Preset_Source_ID','Service_Preset$']);
function allocate(type){let value=1;while(occupied[type].has(value))value++;occupied[type].add(value);return String(value);}
for(const [module,file]of [['Sources','Control4_Room.usp'],['Transport','Control4_Room_Transport.usp']]){
 const text=fs.readFileSync(path.join(workspace,'SIMPLPlus',file),'utf8');
 for(const match of text.matchAll(/^(DIGITAL|ANALOG|STRING)_(INPUT|OUTPUT)\s+([^;]+);/gm)){
  const type=types[match[1]],direction=match[2]==='INPUT'?'send':'receive';
  for(const raw of match[3].split(',').map(x=>x.trim())){
   const name=raw.replace(/\[.*\]$/,'');if(name==='_skip_')continue;
   const id=`${module}|${direction}|${name}`;if(known.has(id))continue;
   const array=raw.match(/\[(VideoSources|AudioSources)\]/);
   let reason=array?'Fixed source-slot array; dynamic JSON/ID UI replaces individual panel joins':module==='Transport'&&name==='Room_Off_btn'?'Duplicate endpoint; UI Room Off is routed only to Sources.Room_Off_btn on digital5':module==='Sources'&&privatePins.has(name)?'Private manual service-selector commissioning path; keep opaque selectors out of the UI':null;
   if(reason){omitted.push({module,direction,type,pin:raw,reason});continue;}
   const group=direction==='send'?(module==='Sources'?'sources':'transportSetup'):(module==='Sources'?'sources':'transportStatus');
   // Preserve previous completed assignments when this helper is run again.
   const prior=previous[direction][group]?.[name];
   let join=playingJoins[id]||pairs[id]||prior||allocate(type);occupied[type].add(Number(join));
   c[direction][group][name]=join;add(module,direction,type,name,group+'.'+name,join);
   if(direction==='receive')c.extraReceive.push({module,pin:name,type,join});
  }
 }
}
// The source selected-index input and feedback describe the same source selection.
// Keep independently allocated values here to avoid changing any existing assignment.
c.bindings=records;c.moduleOnly=omitted;
fs.writeFileSync(path.join(base,'join-map.js'),'// UI join map. Account/Director credentials and opaque selectors never belong here.\n// Input/output sharing is limited to matching functions; module-only arrays use JSON.\nwindow.ROOM_MEDIA_CONFIG = '+JSON.stringify(c,null,2).replace('"enabled": ','enabled: ')+';\n');
const csv=['module,ui_direction,type,join,module_pin,config_key',...records.map(x=>[x.module,x.direction,x.type,x.join,x.pin,x.key].join(','))].join('\n')+'\n';fs.writeFileSync(path.join(workspace,'docs/UI_ALL_JOINS.csv'),csv);
fs.writeFileSync(path.join(workspace,'reports/ui-join-coverage.json'),JSON.stringify({mapped:records,unmappedByDesign:omitted},null,2));
console.log(`Mapped ${records.length} scalar pins; ${omitted.length} array/private declarations explicitly module-only`);
