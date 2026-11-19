const {chromium}=require('playwright');const http=require('node:http');const fs=require('node:fs');const path=require('node:path');const assert=require('node:assert/strict');
(async()=>{const base=path.resolve('construct-pages');const server=http.createServer((req,res)=>{const p=path.resolve(base,'.'+decodeURIComponent(req.url.split('?')[0]));if(!p.startsWith(base+path.sep)){res.writeHead(403);return res.end();}if(!fs.existsSync(p)){res.writeHead(404);return res.end();}res.setHeader('Content-Type',p.endsWith('.js')?'text/javascript':p.endsWith('.css')?'text/css':'text/html');res.end(fs.readFileSync(p));});await new Promise(r=>server.listen(0,'127.0.0.1',r));const browser=await chromium.launch({channel:'msedge',headless:true});try{const url=`http://127.0.0.1:${server.address().port}/index.html`;const page=await browser.newPage({viewport:{width:1280,height:900}});await page.route('**/room-shared.js',r=>r.fulfill({contentType:'text/javascript',body:fs.readFileSync('construct-pages/room-shared.js','utf8').replace('enabled: true','enabled: false')}));const errors=[];page.on('pageerror',e=>errors.push(e.message));await page.goto(url);await page.waitForFunction(()=>window.RoomMediaPages?.stats().views===3);assert.match(await page.locator('[data-c4-page="listen"] .rm-count').innerText(),/162/);await page.locator('[data-show="watch"]').click();assert.equal(await page.locator('[data-c4-page="watch"] .rm-watch button').count(),50);await page.locator('[data-show="rooms"]').click();assert.match(await page.locator('[data-room-buttons]').innerText(),/Living Room/);assert.equal(await page.locator('script[src$="room-shared.js"]').count(),1);assert.equal(await page.locator('link[href$="room-shared.css"]').count(),1);await page.locator('[data-show="listen"]').click();await page.screenshot({path:'previews/construct-shared-pages.png',fullPage:true});assert.deepEqual(errors,[]);
const live=await browser.newPage();live.on('pageerror',e=>errors.push(e.message));await live.addInitScript(()=>{window.sent=[];window.callbacks=new Map();let id=0;window.CrComLib={subscribeState(t,j,f){const key=++id;callbacks.set(key,{t,j,f});if(t==='b'){const r=window.ROOM_MEDIA_CONFIG.receive;f([r.connected,r.mediaValid,r.selectReady].includes(j));}return key;},unsubscribeState(t,j,key){callbacks.delete(key);},publishEvent(...v){sent.push(v);}};});await live.route('**/room-shared.js',r=>r.fulfill({contentType:'text/javascript',body:fs.readFileSync('construct-pages/room-shared.js','utf8').replace('enabled: false','enabled: true')}));await live.goto(url);await live.waitForFunction(()=>window.RoomMediaPages?.stats().sessions===1);const stats=await live.evaluate(()=>RoomMediaPages.stats());assert.equal(stats.views,3);assert.ok(stats.subscriptions>0);assert.equal(await live.evaluate(()=>callbacks.size),stats.subscriptions);await live.evaluate(()=>{for(let i=0;i<5;i++)RoomMediaPages.start();});assert.equal(await live.evaluate(()=>callbacks.size),stats.subscriptions);
await live.evaluate(()=>{window.removedPages=[...document.querySelectorAll('[data-c4-page]:not([data-c4-page="rooms"])')];removedPages.forEach(p=>p.remove());});await live.waitForFunction(()=>RoomMediaPages.stats().sessions===0);assert.equal(await live.evaluate(()=>callbacks.size),0);await live.evaluate(()=>removedPages.forEach(p=>document.querySelector('main').append(p)));await live.waitForFunction(()=>RoomMediaPages.stats().sessions===1);assert.equal(await live.evaluate(()=>callbacks.size),stats.subscriptions);await live.evaluate(()=>RoomMediaPages.selectRoom(212));
assert.equal(await live.evaluate(()=>callbacks.size),stats.subscriptions);
assert.ok(await live.evaluate(()=>[...callbacks.values()].every(c=>c.j==='7'||Number(c.j)>=102&&Number(c.j)<=108)));
assert.ok(await live.evaluate(()=>sent.some(([type,join,packet])=>type==='s'&&join==='101'&&JSON.parse(packet).room_id===212)));
await live.evaluate(()=>{
 const emit=(t,j,v)=>[...callbacks.values()].filter(c=>c.t===t&&c.j===j).forEach(c=>c.f(v));
 emit('s','102',JSON.stringify({r:212,mediaValid:1,selectReady:1}));
 emit('s','105',JSON.stringify({room_id:13,kind:'service_presets',valid:true,selection_supported:true,revision:'fixture',page:1,pages:1,total:1,items:[{id:'1',name:'Other-room station'}]}));
});
assert.equal(await live.locator('[data-c4-page="listen"] .rm-station').count(),0);
await live.evaluate(()=>{
 const emit=(t,j,v)=>[...callbacks.values()].filter(c=>c.t===t&&c.j===j).forEach(c=>c.f(v));
 emit('s','105',JSON.stringify({room_id:212,kind:'service_presets',valid:true,selection_supported:true,revision:'fixture212',page:1,pages:1,total:1,items:[{id:'2',name:'Kitchen station'}]}));
});
assert.equal(await live.locator('[data-c4-page="listen"] .rm-station').count(),1);
assert.equal(await live.locator('[data-c4-page="listen"] .rm-room').inputValue(),'212');
const panel=live.locator('[data-c4-page="listen"]');
assert.equal(await panel.locator('.rm-devices').isVisible(),false);
await live.evaluate(()=>RoomMediaPages.selectRoom(13));
const items=[{id:437,name:'Main Hall',shade:false,value:44},{id:438,name:'Counter Light',shade:false,value:22},
 {id:226,name:'Middle Shade',shade:true,value:25},{id:227,name:'Side Shade',shade:true,value:65}];
const payload=Buffer.from(JSON.stringify({room_id:13,devices:items}), 'utf8').toString('base64').match(/.{1,160}/g);
const frames=payload.map((d,i)=>({w:1,r:13,k:'d',e:'0000000000000013',p:i+1,n:payload.length,d}));
for(const frame of frames){
 assert.ok(JSON.stringify(frame).length<=255);
 await live.evaluate(packet=>{for(const {t,j,f} of callbacks.values())if(t==='s'&&j==='102')f(JSON.stringify(packet));},frame);
}
assert.ok(await live.evaluate(()=>sent.some(([t,j,data])=>{const p=JSON.parse(data);return t==='s'&&j==='101'&&p.room_id===13&&p.op==='device_page'&&p.value===2})));
await panel.locator('.rm-devices summary').click();
assert.deepEqual(await panel.locator('.rm-devices legend').allTextContents(),items.map(x=>x.name));
await live.evaluate(()=>{for(const {t,j,f} of callbacks.values())if(t==='s'&&j==='102'){
 f(JSON.stringify({r:212,u:438,v:100}));f(JSON.stringify({r:13,u:438,v:55}));
}});
assert.deepEqual(await panel.locator('.rm-devices fieldset p').allTextContents(),['Level 44%','Level 55%','Position 25%','Position 65%']);
await panel.locator('fieldset').filter({hasText:'Counter Light'}).getByRole('button',{name:'On',exact:true}).click();
await panel.locator('fieldset').filter({hasText:'Side Shade'}).getByRole('button',{name:'Stop',exact:true}).click();
assert.ok(await live.evaluate(()=>sent.some(([t,j,data])=>{const p=JSON.parse(data);return t==='s'&&j==='101'&&p.room_id===13&&p.device_id===438&&p.value===100})));
assert.ok(await live.evaluate(()=>sent.some(([t,j,data])=>{const p=JSON.parse(data);return t==='s'&&j==='101'&&p.room_id===13&&p.device_id===227&&p.action==='stop'})));
await live.evaluate(()=>RoomMediaPages.destroy());assert.equal(await live.evaluate(()=>callbacks.size),0);assert.deepEqual(errors,[]);console.log('Shared pages passed: preview, one script/style, one live session, idempotent mounts, removal/remount, room-switch isolation and cleanup');}finally{await browser.close();server.close();}})().catch(e=>{console.error(e);process.exit(1)});
