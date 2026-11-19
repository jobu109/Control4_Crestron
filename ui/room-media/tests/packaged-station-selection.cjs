const {chromium}=require('playwright');const fs=require('fs');
(async()=>{const b=await chromium.launch({channel:'msedge',headless:true});try{
const p=await b.newPage();await p.setContent('<div id="room"></div>');await p.addScriptTag({content:fs.readFileSync('reports/CP24a-built-room-shared.js','utf8')});
await p.evaluate(()=>{window.cb={};window.sent=[];const com={subscribeState(t,j,f){cb[t+j]=f;return t+j},unsubscribeState(){},publishEvent(...x){sent.push(x)}};
RoomMediaApp.mount(document.querySelector('#room'),{bridge:new CrestronRoomBridge(com,ROOM_MEDIA_CONFIG),roomId:13});cb.b7(true);cb.b10(true);cb.b9(true);
cb.s5(JSON.stringify({room_id:13,kind:'service_presets',source_id:65,valid:true,revision:'r',page:1,pages:1,total:1,selection_supported:true,items:[{id:'171',name:'HEAVY'}]}));
cb.b99(true);cb.s16(JSON.stringify({room_id:13,source_id:65,valid:true,title:'Live track',artist:'Artist',album:'Album',artwork:''}));});
console.log('station disabled',await p.locator('[data-item="171"]').isDisabled());await p.locator('[data-item="171"]').click();console.log(JSON.stringify(await p.evaluate(()=>sent)));
}finally{await b.close();}})().catch(e=>{console.error(e);process.exit(1)});
