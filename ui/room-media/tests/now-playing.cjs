const fs=require('node:fs'),vm=require('node:vm'),assert=require('node:assert/strict');
const ctx={window:{atob,TextDecoder},setTimeout,clearTimeout,Date};vm.createContext(ctx);
for(const name of ['join-map.js','catalog.js','serial-wire.js'])vm.runInContext(fs.readFileSync(name,'utf8'),ctx);
ctx.RoomMediaCatalog=ctx.window.RoomMediaCatalog;vm.runInContext(fs.readFileSync('crestron-bridge.js','utf8'),ctx);
const callbacks=new Map(),sent=[],updates=[];
const com={subscribeState(t,j,f){callbacks.set(t+j,f);return t+j},unsubscribeState(){},publishEvent(...x){sent.push(x)}};
const cfg=ctx.window.ROOM_MEDIA_CONFIG,b=new ctx.window.CrestronRoomBridge(com,cfg);b.subscribe(x=>updates.push(x));
callbacks.get('b'+cfg.receive.nowPlayingValid)(true);
const receive=x=>callbacks.get('s'+cfg.receive.nowPlaying)(JSON.stringify(x));
const progress={room_id:13,source_id:65,valid:true,track_revision:'1111111111111111',position_seconds:11,duration_seconds:235};
callbacks.get('s'+cfg.receive.progress)(JSON.stringify(progress));assert.equal(b.progress.position_seconds,11);
callbacks.get('s'+cfg.receive.progress)(JSON.stringify({...progress,room_id:99,position_seconds:25}));assert.equal(b.progress.position_seconds,11);
callbacks.get('s'+cfg.receive.progress)(JSON.stringify({...progress,position_seconds:236}));assert.equal(b.progress,null);

function frames(packet,epoch){const data=Buffer.from(JSON.stringify(packet)).toString('base64'),out=[];for(let i=0;i<data.length;i+=160)out.push({w:1,r:packet.room_id,k:'n',e:epoch,p:out.length+1,n:Math.ceil(data.length/160),d:data.slice(i,i+160)});return out;}
const packet={room_id:13,source_id:65,valid:true,title:'Circle The Drain · 🎵',artist:'Machine Head',album:'Circle The Drain',artwork:'https://i.scdn.co/image/example'};
const wire=frames(packet,'1111111111111111');receive(wire[0]);assert.equal(sent.length,0);
callbacks.get('b'+cfg.receive.connected)(true);
setTimeout(()=>{try{
  assert.ok(sent.some(x=>x[0]==='n'&&x[1]==='20'&&x[2]===2),'Late connection must resume metadata packet2');
  for(const frame of wire.slice(1))receive(frame);
  assert.equal(b.nowPlaying.title,packet.title);assert.equal(b.nowPlaying.artwork,packet.artwork);
  for(const frame of frames({...packet,room_id:99,title:'Other room'},'2222222222222222'))receive(frame);
  assert.equal(b.nowPlaying.title,packet.title);
  for(const frame of frames({...packet,title:'White Wine Spritzer',artwork:''},'3333333333333333'))receive(frame);
  assert.equal(b.nowPlaying.title,'White Wine Spritzer');assert.equal(b.nowPlaying.artwork,'');
  receive(wire[0]);assert.equal(b.nowPlaying.title,'White Wine Spritzer','Retired frame must not replace current track');
  for(const frame of frames({room_id:13,valid:false},'4444444444444444'))receive(frame);
  assert.equal(b.nowPlaying,null);
  for(const frame of frames(packet,'5555555555555555'))receive(frame);
  callbacks.get('b'+cfg.receive.connected)(false);assert.equal(b.nowPlaying,null);assert.equal(updates.filter(x=>Object.hasOwn(x,"nowPlaying")).at(-1).nowPlaying,null);
  b.destroy();console.log('Now-playing bridge passed: late-connected paging, Unicode/artwork, track replacement, wrong-room/stale frames, invalidation and disconnect.');
}catch(e){b.destroy();console.error(e);process.exitCode=1;}},60);
