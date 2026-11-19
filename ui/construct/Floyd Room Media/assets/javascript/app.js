(function(global){
  'use strict';
  function mount(root, options){
    if(!root || root.dataset.mounted) return;
    root.dataset.mounted='true';
    const data=options.data||{}, bridge=options.bridge||null;
    const state={mode:options.initialMode==='watch'?'watch':'listen',source:'1648',items:[],page:1,search:'',selected:'380',shuffle:false,volume:29,mute:false,valid:!bridge,revision:'preview',pending:false};
    const sample=data.metadata||{};
    root.className='rm';
    root.innerHTML=`<header class="rm-header"><div><div class="rm-brand">HOME / ENTERTAINMENT</div><select class="rm-room" aria-label="Room"><option>Living Room</option></select></div><nav class="rm-tabs" aria-label="Media mode"><button data-mode="listen" class="active">Listen</button><button data-mode="watch">Watch</button></nav><div class="rm-status">${bridge?'Connecting to Crestron':'Interactive preview'}</div></header><main class="rm-body"><aside class="rm-sources"><div class="rm-label">Sources</div><div class="rm-source-buttons"></div><div class="rm-sources-note">Your room.<br>Your music.</div></aside><section class="rm-browser"><div class="rm-list-head"><h1 class="rm-service-title">Pandora</h1><span class="rm-count"></span></div><div class="rm-subtitle">Saved stations</div><input class="rm-search" type="search" placeholder="Search saved stations" aria-label="Search stations"><div class="rm-list" role="list" aria-label="Saved stations"></div><div class="rm-page"><button data-page="previous" aria-label="Previous page">‹</button><span class="rm-page-text"></span><button data-page="next" aria-label="Next page">›</button></div></section><section class="rm-now"><div class="rm-label">Now playing</div><div class="rm-art"><small>ARTWORK PLACEHOLDER</small><strong>Sound<br>at home.</strong><span>Image feed to be connected</span></div><h2 class="rm-track"></h2><div class="rm-artist"></div><div class="rm-album"></div><div class="rm-playing-from"></div><div class="rm-progress" hidden><span class="rm-elapsed"></span><progress aria-label="Track progress"></progress><span class="rm-duration"></span></div><div class="rm-transport"><button data-key="Skip_Back" aria-label="Previous track">|‹</button><button data-key="Play" class="play" aria-label="Play">▶</button><button data-key="Pause" aria-label="Pause">Ⅱ</button><button data-key="Skip_Forward" aria-label="Next track">›|</button></div><label class="rm-shuffle"><input type="checkbox"> Shuffle on next station selection</label><div class="rm-meta-note"></div></section></main><footer class="rm-footer"><div class="rm-volume"><span>Volume</span><button class="rm-volume-down" data-key="Volume_Down" aria-label="Volume down">−</button><input type="range" min="0" max="100" value="29" aria-label="Room volume"><button class="rm-volume-up" data-key="Volume_Up" aria-label="Volume up">+</button><span class="rm-volume-value">29%</span></div><button class="rm-mute">Mute</button><button class="off">⏻ &nbsp; Room off</button></footer><div class="rm-preview">${bridge?'Live controls and metadata from Control4':'Preview only · station names come from your exports · controls do not operate the system'}</div><div class="rm-toast" role="status" hidden></div>`;
    const q=s=>root.querySelector(s);
    let pressedKey='',pressedTimer;
    function buttonKey(button){
      if(button.classList.contains('play'))return 'main-playback';
      for(const name of ['data-source','data-item','data-key','data-remote-key','data-mode','data-page','aria-label'])if(button.hasAttribute(name))return name+':'+button.getAttribute(name);
      return Array.from(button.classList).filter(name=>name!=='active'&&name!=='rm-pressed').join(' ')||button.textContent;
    }
    function paintPressed(){root.querySelectorAll('button').forEach(button=>button.classList.toggle('rm-pressed',!button.closest('.rm-devices')&&!!pressedKey&&!button.disabled&&buttonKey(button)===pressedKey));}
    function onPress(event){
      const button=event.target.closest('button');if(!button||!root.contains(button)||button.disabled)return;
      if(button.closest('.rm-devices'))return; pressedKey=buttonKey(button);clearTimeout(pressedTimer);paintPressed();queueMicrotask(paintPressed);
      pressedTimer=setTimeout(()=>{pressedKey='';paintPressed();},200);
    }
    root.addEventListener('click',onPress,true);
    const art=q('.rm-art'),artImage=document.createElement('img');
    artImage.className='rm-art-image';artImage.alt='Album artwork';artImage.referrerPolicy='no-referrer';art.prepend(artImage);
    artImage.onload=()=>art.classList.add('rm-art-loaded');artImage.onerror=()=>art.classList.remove('rm-art-loaded');
    let artworkUrl='';
    let toastTimer, metadata=bridge?null:sample, progress=null;
    let videoSources=bridge?[]:[{id:'2061',name:'DIRECTV Stream'}];
    let connected=!bridge,ready=!bridge,volumeValid=!bridge,muteValid=!bridge,capabilities={},selectedSources={};
    let sources=bridge?[]:[{id:'65',name:'Spotify'},{id:'1648',name:'Pandora'}];
    const roomSelect=q('.rm-room');
    const rooms=options.rooms||[{id:13,name:'Living Room'}];
    roomSelect.replaceChildren();
    rooms.forEach(room=>{const option=document.createElement('option');option.value=String(room.id);option.textContent=room.name;roomSelect.append(option);});
    roomSelect.value=String(options.roomId||rooms[0].id);
    roomSelect.onchange=()=>{if(options.onRoomChange)options.onRoomChange(Number(roomSelect.value));};
    const pageSize=6;
    function toast(text){q('.rm-toast').textContent=text;q('.rm-toast').hidden=false;clearTimeout(toastTimer);toastTimer=setTimeout(()=>q('.rm-toast').hidden=true,2400);}
    function selectedName(){const item=state.items.find(i=>i.id===state.selected);return item?item.name:'';}
    function renderProgress(){
      const valid=!!(metadata&&progress&&progress.valid&&metadata.source_id===progress.source_id&&metadata.track_revision===progress.track_revision);
      q('.rm-progress').hidden=!valid;if(!valid)return;
      const time=value=>Math.floor(value/60)+':'+String(value%60).padStart(2,'0');
      q('.rm-elapsed').textContent=time(progress.position_seconds);q('.rm-duration').textContent=time(progress.duration_seconds);
      q('.rm-progress progress').max=progress.duration_seconds;q('.rm-progress progress').value=progress.position_seconds;
    }
    function render(){
      q('.rm-source-buttons').replaceChildren();
      const available=state.mode==='watch'?videoSources:sources;
      available.forEach(source=>{const b=document.createElement('button');b.className='rm-source'+((bridge?(selectedSources[state.mode]?.valid&&selectedSources[state.mode].id===source.id):source.id===state.source)?' active':'');b.dataset.source=source.id;b.setAttribute('aria-pressed',String(bridge?!!(selectedSources[state.mode]?.valid&&selectedSources[state.mode].id===source.id):source.id===state.source));const icon=document.createElement('span');icon.className='rm-icon';icon.textContent=source.name.slice(0,1);const name=document.createElement('span');name.textContent=source.name;b.append(icon,name);b.disabled=!connected;b.onclick=()=>selectSource(source.id);q('.rm-source-buttons').append(b);});
      const source=sources.find(s=>s.id===state.source);
      q('.rm-service-title').textContent=state.mode==='watch'?'Video controls':source?source.name:'Choose a source';
      q('.rm-subtitle').textContent=state.mode==='watch'?'Navigation and transport':state.source==='65'?'Saved presets':'Saved stations';
      q('.rm-search').hidden=state.mode==='watch';
      const filtered=state.items.filter(i=>i.name.toLowerCase().includes(state.search.toLowerCase()));
      const pages=Math.max(1,Math.ceil(filtered.length/pageSize));state.page=Math.min(state.page,pages);
      q('.rm-count').textContent=state.pending?'Select a service':state.mode==='watch'?'Remote':state.items.length+' '+(state.source==='65'?'presets':'stations');
      q('.rm-list').replaceChildren();
      if(state.mode==='watch'){
        const remote=document.createElement('div');remote.className='rm-watch';
        const groups=[
          ['Navigation',['Guide','Up','Info','Left','Select','Right','Back','Down','Menu']],
          ['Playback',['Rewind','Play','Fast_Forward','Skip_Back','Pause','Skip_Forward','Record','Stop','DVR']],
          ['Room',['Room_Off','Watch','Control4','Listen','List','Previous']],
          ['Volume and channel',['Volume_Up','Volume_Down','Mute','Channel_Up','Channel_Down','Page_Up','Page_Down']],
          ['Keypad',['Digit_1','Digit_2','Digit_3','Digit_4','Digit_5','Digit_6','Digit_7','Digit_8','Digit_9','Star','Digit_0','Pound']],
          ['Functions',['Red','Green','Yellow','Blue','Custom_1','Custom_2','Custom_3']]
        ];
        groups.forEach(([title,keys])=>{
          const heading=document.createElement('h3');heading.textContent=title;remote.append(heading);
          const grid=document.createElement('div');grid.className='rm-key-grid';
          keys.forEach(key=>{const b=document.createElement('button');b.dataset.remoteKey=key;b.textContent=key.startsWith('Digit_')?key.slice(6):key==='Star'?'*':key==='Pound'?'#':key.replaceAll('_',' ');b.disabled=!!bridge&&(!connected||!capabilities[key]);b.onclick=()=>command(key);grid.append(b);});remote.append(grid);
        });q('.rm-list').append(remote);
      }else if(state.pending || !filtered.length){const p=document.createElement('div');p.className='rm-empty';p.textContent=state.pending?'Please select a service from the left':state.valid?'No matching saved items':'Please select a service from the left';q('.rm-list').append(p);
      }else filtered.slice((state.page-1)*pageSize,state.page*pageSize).forEach(item=>{
        const b=document.createElement('button');b.className='rm-station'+(state.selected===item.id?' active':'');b.dataset.item=item.id;b.setAttribute('role','listitem');b.disabled=!state.valid||!connected||!ready;
        const icon=document.createElement('span');icon.className='rm-row-icon';icon.textContent='♫';const name=document.createElement('span');name.className='rm-station-name';name.textContent=item.name;const arrow=document.createElement('span');arrow.className='rm-row-arrow';arrow.textContent=state.selected===item.id?'●':'›';b.append(icon,name,arrow);b.onclick=()=>selectItem(item);q('.rm-list').append(b);
      });
      q('.rm-page-text').textContent=state.mode==='watch'?'Control4 remote':`Page ${state.page} of ${pages}${state.search?' · '+filtered.length+' matches':''}`;
      q('[data-page="previous"]').disabled=state.page<=1||state.mode==='watch';q('[data-page="next"]').disabled=state.page>=pages||state.mode==='watch';
      q('.rm-now').classList.toggle('rm-waiting',!metadata);
      q('.rm-track').textContent=metadata&&metadata.title||'Waiting for selection';q('.rm-artist').textContent=metadata&&metadata.artist||'—';q('.rm-album').textContent=metadata&&metadata.album||'';
      q('.rm-playing-from').textContent=metadata&&metadata.station?'Playing from · '+metadata.station:selectedName()?'Selected in preview · '+selectedName():'';
      q('.rm-meta-note').textContent=bridge?(metadata?'Live from Control4':'Waiting for Control4 metadata'):(metadata?'Text sample from the metadata capture; not a live feed':'Preview metadata');
      let nextArt='';try{const url=new URL(metadata&&metadata.artwork||'');if(url.protocol==='https:'&&!url.username&&!url.password&&!url.search&&!url.hash)nextArt=url.href;}catch(_){}
      if(nextArt!==artworkUrl){artworkUrl=nextArt;art.classList.remove('rm-art-loaded');if(nextArt)artImage.src=nextArt;else artImage.removeAttribute('src');}
      const action=metadata&&metadata.playback_action;
      const knownAction=action==='play'||action==='pause';
      const mainPlayback=q('.rm-transport .play'),secondaryPause=q('.rm-transport button[aria-label="Pause"]:not(.play)');
      mainPlayback.dataset.key=knownAction&&action==='pause'?'Pause':'Play';mainPlayback.textContent=mainPlayback.dataset.key==='Pause'?'Ⅱ':'▶';mainPlayback.setAttribute('aria-label',mainPlayback.dataset.key);
      secondaryPause.hidden=knownAction;
      root.querySelectorAll('[data-key]').forEach(b=>b.disabled=!!bridge&&(!connected||!capabilities[b.dataset.key]));
      q('.rm-volume input').disabled=!connected||!volumeValid;q('.rm-mute').disabled=!connected||!muteValid;q('.off').disabled=!connected;
      q('.rm-shuffle').hidden=state.source!=='1648'||state.mode==='watch';q('.rm-shuffle input').checked=state.shuffle;q('.rm-shuffle input').disabled=!connected;
      q('.rm-volume input').value=state.volume;q('.rm-volume-value').textContent=volumeValid?state.volume+'%':'—';q('.rm-mute').textContent=state.mute?'Unmute':'Mute';
      renderProgress();paintPressed();
    }
    function selectSource(id){
      if(bridge){state.pending=true;state.items=[];state.valid=false;state.selected='';render();bridge.selectSource(state.mode,id);return;}
      state.source=id;state.items=state.mode==='watch'?[]:(data.catalogs&&data.catalogs[id]||[]);state.selected='';state.page=1;state.search='';q('.rm-search').value='';metadata=null;state.valid=true;render();
    }
    function selectItem(item){
      if(bridge){bridge.selectItem('service_presets',state.revision,item.id);toast('Selection sent. Waiting for playback feedback.');return;}
      state.selected=item.id;metadata=null;render();toast('Preview selection · '+item.name);
    }
    function command(key){
      if(bridge)bridge.transport(key);else if(key==='Volume_Up'||key==='Volume_Down'){state.volume=Math.max(0,Math.min(100,state.volume+(key==='Volume_Up'?1:-1)));render();}else toast('Preview command · '+key);
      if(['Watch','Listen','List'].includes(key)){
        state.mode=key==='Watch'?'watch':'listen';state.page=1;
        root.querySelectorAll('[data-mode]').forEach(b=>b.classList.toggle('active',b.dataset.mode===state.mode));render();
        if(key==='List')q('.rm-search').focus();
      }
    }
    q('.rm-search').addEventListener('input',e=>{state.search=e.target.value;state.page=1;render();});
    root.querySelectorAll('[data-mode]').forEach(b=>b.onclick=()=>{state.mode=b.dataset.mode;state.page=1;root.querySelectorAll('[data-mode]').forEach(x=>x.classList.toggle('active',x===b));render();if(!bridge&&state.mode==='listen')selectSource('1648');});
    q('[data-page="previous"]').onclick=()=>{state.page--;render();};q('[data-page="next"]').onclick=()=>{state.page++;render();};
    root.querySelectorAll('[data-key]').forEach(b=>b.onclick=()=>command(b.dataset.key));
    q('.rm-shuffle input').onchange=e=>{state.shuffle=e.target.checked;if(bridge)bridge.shuffle(state.shuffle);};
    q('.rm-volume input').oninput=e=>q('.rm-volume-value').textContent=e.target.value+'%';
    q('.rm-volume input').onchange=e=>{if(bridge)bridge.volume(Number(e.target.value));else state.volume=Number(e.target.value);};
    q('.rm-mute').onclick=()=>{if(bridge)bridge.mute(!state.mute);else{state.mute=!state.mute;render();}};
    q('.off').onclick=()=>{if(bridge)bridge.off();else{state.items=[];state.selected='';state.source='';state.valid=false;metadata=null;render();toast('Preview · Room off');}};
    state.items=data.catalogs&&data.catalogs['1648']||[];
    if(bridge)bridge.subscribe(update=>{
      if(update.progress!==undefined){progress=update.progress;if(Object.keys(update).length===1){renderProgress();return;}}
      if(update.selectedSources)selectedSources=update.selectedSources;
      if(update.volumeValid!==undefined)volumeValid=update.volumeValid;
      if(update.moduleFeedback&&update.moduleFeedback['Sources.Mute_Valid_FB']!==undefined)muteValid=!!update.moduleFeedback['Sources.Mute_Valid_FB'];
      if(update.sources)sources=update.sources;if(update.videoSources)videoSources=update.videoSources;if(update.connected!==undefined)connected=update.connected;if(update.selectionReady!==undefined)ready=update.selectionReady;if(update.capabilities)capabilities=update.capabilities;
      if(update.catalog){state.items=update.catalog.items;state.revision=update.catalog.revision;state.valid=update.catalog.valid;state.pending=update.catalog.pending;state.source=update.catalog.source_id?String(update.catalog.source_id):'';state.shuffle=update.catalog.shuffle||false;state.page=1;}
      if(update.volume!==undefined)state.volume=update.volume;if(update.muted!==undefined)state.mute=update.muted;
      if(update.roomName){const selected=Array.from(roomSelect.options).find(o=>o.value===roomSelect.value);if(selected)selected.textContent=update.roomName;}
      if(update.status)q('.rm-status').textContent=update.status;
      if(update.nowPlaying!==undefined)metadata=update.nowPlaying;
      render();
    });
    root.querySelectorAll('[data-mode]').forEach(b=>b.classList.toggle('active',b.dataset.mode===state.mode));
    render();
    return {destroy(){clearTimeout(toastTimer);clearTimeout(pressedTimer);root.removeEventListener('click',onPress,true);if(bridge)bridge.destroy();root.replaceChildren();delete root.dataset.mounted;}};
  }
  global.RoomMediaApp={mount};
})(window);
