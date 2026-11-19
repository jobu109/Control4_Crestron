(function(global){
  'use strict';
  if(global.RoomMediaPages)return;
  const views=new Map(),sessions=new Map();
  let selectedRoom=null,observer=null,scheduled=false,initializationTimer=null,initializationAttempts=0;
  let routerRooms=[], directory=null, sequence=0, homeView='home';
  const destinations = [
    ['home', 'Home'], ['rooms', 'Rooms'], ['lights', 'Lights'], ['shades', 'Shades'],
    ['security', 'Security'], ['comfort', 'Comfort'], ['cameras', 'Cameras']
  ];
  const sceneViews = new Set(['lights', 'shades']);
  const iconPaths = {
    home:'M3 11l9-8 9 8 M5 10v10h5v-6h4v6h5V10',
    rooms:'M3 4h18v16H3z M12 4v16 M3 12h9 M12 9h9',
    lights:'M8 16c0-2-3-3-3-7a7 7 0 0 1 14 0c0 4-3 5-3 7 M8 17h8 M9 20h6 M11 22h2',
    shades:'M4 4h16v16H4z M4 8h16 M4 11h16 M4 14h16 M4 17h16',
    security:'M12 3l8 3v5c0 5-3 8-8 11-5-3-8-6-8-11V6z M9 12l2 2 4-4',
    comfort:'M9 14V6a3 3 0 0 1 6 0v8a5 5 0 1 1-6 0z M12 9v8 M18 8h3 M18 12h2',
    cameras:'M3 6h12v11H3z M15 10l6-3v9l-6-3'
  };
  function icon(doc,id){
    const canvas=doc.createElement('canvas');
    canvas.width=48;canvas.height=48;canvas.className='c4-dock-icon';canvas.setAttribute('aria-hidden','true');
    const pen=canvas.getContext('2d');
    pen.scale(2,2);pen.lineWidth=1.4;pen.lineCap='round';pen.lineJoin='round';pen.strokeStyle='#f5f8fc';
    pen.stroke(new Path2D(iconPaths[id]));
    return canvas;
  }
  function openView(view){
    if(homeView===view)return;
    homeView=view;
    remountViews();
  }
  function mountHome(root, original){
    const previousClass=root.className;
    root.className='c4-home';
    root.innerHTML=`
      <header class="c4-home-header">
        <button class="c4-home-brand" type="button" data-home-view="home">Home</button>
        <time class="c4-home-clock"><span class="c4-home-time"></span><span class="c4-home-date"></span></time>
      </header>
      <main class="c4-home-main">
        <div class="c4-home-content"></div>
        <nav class="c4-home-nav" aria-label="Whole home"></nav>
      </main>`;
    const doc=root.ownerDocument;
    const content=root.querySelector('.c4-home-content');
    const nav=root.querySelector('.c4-home-nav');
    function button(title,description,action,selected,dockId){
      const node=doc.createElement('button');node.type='button';
      if(dockId)node.append(icon(doc,dockId));
      const heading=doc.createElement('strong');heading.textContent=title;
      node.append(heading);
      if(description){const sub=doc.createElement('span');sub.textContent=description;node.append(sub);}
      if(selected)node.setAttribute('aria-current','page');
      node.onclick=action;
      return node;
    }
    root.querySelector('[data-home-view]').onclick=()=>openView('home');
    for(const [id,title] of destinations)
      nav.append(button(title,'',()=>openView(id),homeView===id,id));
    if(homeView==='rooms'){
      const title=doc.createElement('h1');title.textContent='Rooms';content.append(title);
      const list=doc.createElement('div');list.className='c4-home-rooms';content.append(list);
      const current=room();
      if(current)list.append(button('Open '+current.name,'',()=>selectRoom(current.id)));
      for(const entry of rooms())if(entry.id!==current?.id)
        list.append(button(entry.name,'',()=>selectRoom(entry.id)));
      if(!current){const status=doc.createElement('p');status.textContent='Waiting for the Panel Room Router.';list.append(status);}
    }else{
      const item=destinations.find(([id])=>id===homeView);
      const title=doc.createElement('h1');title.textContent=item&&homeView!=='home'?item[1]:'Welcome home';content.append(title);
      if(sceneViews.has(homeView)){
        const heading=doc.createElement('h2');heading.textContent=homeView==='lights'?'Lighting scenes':'Shade scenes';
        const status=doc.createElement('p');status.setAttribute('role','status');
        status.textContent='No verified Control4 scenes are available.';
        content.append(heading,status);
        if(room())content.append(button('Open '+room().name,'',()=>selectRoom(room().id)));
      }else if(item&&homeView!=='home'){
        const status=doc.createElement('p');status.setAttribute('role','status');
        status.textContent=item[1]+' integration is unavailable.';
        content.append(status);
      }else{
        const summary=doc.createElement('p');summary.textContent=room()?'Rooms available':'Waiting for the Panel Room Router';content.append(summary);
        if(room())content.append(button('Open '+room().name,'',()=>selectRoom(room().id)));
      }
    }
    const clock=root.querySelector('.c4-home-clock');
    function updateClock(){
      const now=new Date();
      clock.dateTime=now.toISOString();
      clock.querySelector('.c4-home-time').textContent=new Intl.DateTimeFormat(undefined,{hour:'numeric',minute:'2-digit'}).format(now);
      clock.querySelector('.c4-home-date').textContent=new Intl.DateTimeFormat(undefined,{weekday:'long',month:'long',day:'numeric'}).format(now);
    }
    updateClock();
    const timer=global.setInterval(updateClock,15000);
    return {destroy(){global.clearInterval(timer);root.className=previousClass;root.innerHTML=original;}};
  }
  function rooms(){return global.ROOM_MEDIA_CONFIG?.enabled ? routerRooms : global.ROOM_MEDIA_ROOMS||[];}
  function connectDirectory(){
    if(directory||!global.ROOM_MEDIA_CONFIG?.enabled)return;
    const com=getCom();if(!com)return;
    const collector=new global.RoomSerialWire.Collector(0,'rooms');
    const subscriptions=[];
    const current={close(){for(const [type,join,id] of subscriptions)com.unsubscribeState(type,join,id);}};
    directory=current;
    let online=false;
    function request(op,value){
      queueMicrotask(()=>{if(directory===current&&online)com.publishEvent('s','101',JSON.stringify({op,value,q:++sequence}));});
    }
    function watch(type,join,callback){subscriptions.push([type,join,com.subscribeState(type,join,callback)]);}
    watch('s','102',value=>{
      if(!online||directory!==current)return;
      let result;
      try{result=collector.accept(JSON.parse(value));}catch(_){return;}
      if(result?.requestPage)request('rooms_page',result.requestPage);
      if(!Array.isArray(result?.packet?.rooms))return;
      const seen=new Set();
      const next=result.packet.rooms.filter(item=>item&&Number.isInteger(item.id)&&item.id>0&&item.id<=65535&&
        typeof item.name==='string'&&item.name.trim()&&!seen.has(item.id)&&seen.add(item.id))
        .map(item=>({...item,config:global.ROOM_MEDIA_CONFIG}));
      if(JSON.stringify(next.map(({id,name})=>[id,name]))===JSON.stringify(routerRooms.map(({id,name})=>[id,name])))return;
      routerRooms=next;
      queueMicrotask(()=>{if(directory===current)remountViews();});
    });
    watch('b',global.ROOM_MEDIA_CONFIG.receive.connected,value=>{
      if(online===Boolean(value))return;
      online=Boolean(value);
      collector.reset();
      if(online)request('rooms');
    });
  }
  function room(){return rooms().find(r=>r.id===selectedRoom)||rooms()[0];}
  function getCom(){
    const frames=[global];try{frames.push(global.parent);}catch(_){}try{frames.push(global.top);}catch(_){}
    for(const frame of frames){try{if(frame.CrComLib&&typeof frame.CrComLib.subscribeState==='function'&&typeof frame.CrComLib.publishEvent==='function')return frame.CrComLib;}catch(_){}}
    return null;
  }
  function session(entry){
    if(sessions.has(entry.id))return sessions.get(entry.id);
    const com=getCom();if(!com)return null;
    const config={...entry.config,roomId:entry.id,browser:false};
    const transport=global.PanelRoomTransport.create(com,config);
    const bridge=new global.CrestronRoomBridge(transport,config);
    const destroy=bridge.destroy.bind(bridge);
    bridge.destroy=()=>{destroy();transport.destroy();};
    const value={listeners:new Set(),state:{connected:false,selectionReady:false},bridge,transport};
    sessions.set(entry.id,value);
    bridge.subscribe(update=>{Object.assign(value.state,update);for(const listener of value.listeners)listener(update);});
    transport.onDeviceFeedback(devices=>{value.state.devices=devices;for(const listener of value.listeners)listener({devices});});
    transport.start();
    return value;
  }
  function facade(entry){
    const shared=session(entry);if(!shared)return null;
    let listener=null,closed=false;
    const adapter={subscribe(fn){listener=fn;shared.listeners.add(fn);fn(shared.state);},destroy(){if(closed)return;closed=true;if(listener)shared.listeners.delete(listener);if(!shared.listeners.size){shared.bridge.destroy();sessions.delete(entry.id);}}};
    for(const method of ['selectSource','selectItem','transport','volume','mute','off','shuffle'])adapter[method]=(...args)=>{if(!closed)return shared.bridge[method](...args);};
    adapter.sendDevice=(...args)=>{if(!closed)shared.transport.sendDevice(...args);};
    return adapter;
  }
  function mount(root){
    if(views.has(root))return;
    const original=root.innerHTML;
    const kind=root.dataset.c4Page;
    if(homeView!=='room'){
      views.set(root,mountHome(root,original));
      return;
    }
    const entry=room();if(!entry)return false;
    const live=!!entry.config.enabled;
    const bridge=live?facade(entry):null;
    if(live&&!bridge){
      const note=root.querySelector('[data-startup-status]');if(note&&note.textContent!=='Waiting for Construct communication. Layout preview only.')note.textContent='Waiting for Construct communication. Layout preview only.';
      return false; // Bounded initialization retries below; no network polling.
    }
    const view=global.RoomMediaApp.mount(root,{bridge,data:live?null:global.ROOM_MEDIA_PREVIEW,initialMode:kind==='watch'?'watch':'listen',rooms:rooms(),roomId:entry.id,onRoomChange:selectRoom});
    const header=root.querySelector('.rm-header');
    if(header){
      const home=root.ownerDocument.createElement('button');
      home.type='button';home.className='c4-room-home';home.textContent='Home';
      home.onclick=()=>openView('home');header.prepend(home);
    }
    views.set(root,{destroy(){view.destroy();root.innerHTML=original;}});
  }
  function attached(root){
    if(!root.isConnected)return false;
    try{const frame=root.ownerDocument.defaultView.frameElement;return !frame||frame.isConnected;}catch(_){return true;}
  }
  function detach(root){const view=views.get(root);if(view){view.destroy();views.delete(root);}}
  function scan(){
    scheduled=false;
    connectDirectory();
    for(const [root,view]of views)if(!attached(root)){view.destroy();views.delete(root);}
    let waiting=false;global.document.querySelectorAll('[data-c4-page]').forEach(root=>{if(mount(root)===false)waiting=true;});
    if(waiting&&!initializationTimer&&initializationAttempts<30)initializationTimer=setTimeout(()=>{initializationTimer=null;initializationAttempts++;scan();},100);
    if(!waiting){clearTimeout(initializationTimer);initializationTimer=null;initializationAttempts=0;}
  }
  function remountViews(){
    const previousRoots=Array.from(views.keys());
    for(const view of views.values())view.destroy();views.clear();
    for(const root of previousRoots)if(attached(root))mount(root);
    scan();
  }
  function selectRoom(id){
    if(!rooms().some(r=>r.id===id)||(id===room()?.id&&homeView==='room'))return;
    selectedRoom=id;homeView='room';
    remountViews();
    global.dispatchEvent(new CustomEvent('room-media:room-changed',{detail:{roomId:id}}));
  }
  function start(){
    if(!observer){observer=new MutationObserver(()=>{if(!scheduled){scheduled=true;queueMicrotask(scan);}});observer.observe(global.document.documentElement,{childList:true,subtree:true});}
    scan();
  }
  function destroy(){if(directory){const old=directory;directory=null;old.close();}routerRooms=[];clearTimeout(initializationTimer);initializationTimer=null;initializationAttempts=0;if(observer)observer.disconnect();observer=null;for(const view of views.values())view.destroy();views.clear();for(const value of sessions.values())value.bridge.destroy();sessions.clear();}
  global.RoomMediaPages={start,destroy,selectRoom,attach(root){mount(root);start();},detach,stats(){return {views:views.size,sessions:sessions.size,subscriptions:Array.from(sessions.values()).reduce((n,s)=>n+s.transport.subscriptionCount(),0)};}};
  global.addEventListener('pagehide',destroy);
  global.addEventListener('pageshow',start);
})(window);
