(function(global){
  'use strict';
  function Bridge(com,config){
    this.destroyed=false;this.started=false;this.com=com;this.config=config;this.subs=[];this.listener=null;this.connected=false;this.gatewayConnected=false;this.transportConnected=!config.browser;this.ready=false;this.catalogReady=false;this.sequence=0;this.timers=[];this.activePulses=new Set();this.capabilities={};this.feedback={};
    this.progress=null;this.nowPlaying=null;this.playingValid=false;this.wire={};for(const kind of ['a','v','m','n'])if(global.RoomSerialWire)this.wire[kind]=new global.RoomSerialWire.Collector(config.roomId,kind);this.pageRequests=new Map();
    this.collector=new RoomMediaCatalog.Collector(config.roomId,'service_presets');this.sources={listen:new Map(),watch:new Map()};
  }
  Bridge.prototype.emit=function(update){if(this.listener)this.listener(update);};
  Bridge.prototype.watch=function(type,join,fn){if(join)this.subs.push([type,String(join),this.com.subscribeState(type,String(join),fn)]);};
  Bridge.prototype.subscribe=function(listener){
    if(this.destroyed)throw Error('Bridge already destroyed');if(this.started)throw Error('Bridge already subscribed');this.started=true;
    this.listener=listener;const r=this.config.receive;
    this.watch('b',r.connected,value=>{this.gatewayConnected=!!value;this.refreshConnection();if(!value){this.catalogReady=false;this.collector.reset();this.emit({catalog:{items:[],valid:false,pending:false,revision:'',source_id:null}});}});
    this.watch('b',r.selectReady,value=>{this.ready=!!value;this.emit({selectionReady:this.ready});});
    this.watch('b',r.mediaValid,value=>{if(!value){this.catalogReady=false;this.collector.reset();this.emit({catalog:{items:[],valid:false,pending:this.connected,revision:'',source_id:null}});}});
    this.watch('n',r.volume,value=>this.emit({volume:Number(value)}));this.watch('b',r.muted,value=>this.emit({muted:!!value}));
    this.watch('s',r.roomName,value=>this.emit({roomName:String(value)}));
    this.watch('b',r.volumeValid,value=>this.emit({volumeValid:!!value}));
    this.watch('b',r.sourceValid,value=>this.emit({sourceValid:!!value}));
    this.watch('b',r.power,value=>this.emit({power:!!value}));
    for(const signal of this.config.extraReceive||[])this.watch(signal.type,signal.join,value=>{
      this.feedback[signal.module+'.'+signal.pin]=value;
      this.emit({moduleFeedback:{...this.feedback}});this.publishSelectedSources();
    });
    this.watch('s',r.transport,value=>{try{let p=JSON.parse(value);if(global.RoomSerialWire)p=global.RoomSerialWire.capabilities(p);if(p.room_id!==this.config.roomId)return;const capabilities={};for(const item of p.keys||[])capabilities[item.key]=item.state==='advertised'||item.state==='local_ui';this.capabilities=capabilities;this.emit({capabilities});}catch(_){/* Empty initial state is normal. */}});
    for(const mode of ['listen','watch'])this.watch('s',mode==='listen'?r.audioSources:r.videoSources,value=>{
      try{if(!value)return;const p=this.decode(value,mode==='listen'?'a':'v',mode==='listen'?this.config.send.audioPage:this.config.send.videoPage);if(!p)return;if(p.room_id!==this.config.roomId||p.mode!==mode)return;const cache=this.sources[mode];if(p.page===1)cache.clear();if(!p.valid){cache.clear();this.emit(mode==='listen'?{sources:[]}:{videoSources:[]});return;}if(!Number.isInteger(p.page)||!Number.isInteger(p.pages)||!Array.isArray(p.items))return;cache.set(p.page,p.items);let missing=0;for(let i=1;i<=p.pages;i++)if(!cache.has(i)){missing=i;break;}if(missing){this.requestPage(mode==='listen'?this.config.send.audioPage:this.config.send.videoPage,missing);return;}const items=[];for(let i=1;i<=p.pages;i++)items.push(...cache.get(i));if(items.length===p.total){this.emit(mode==='listen'?{sources:items}:{videoSources:items});this.publishSelectedSources();}}catch(_){this.sources[mode].clear();this.emit(mode==='listen'?{sources:[]}:{videoSources:[]});this.publishSelectedSources();this.emit({status:'Source list unavailable'});}
    });
    this.watch('s',r.mediaList,value=>{
      if(!value)return;
      try{const packet=this.decode(value,'m',this.config.send.mediaPage);if(!packet)return;const result=this.collector.accept(packet);if(!result)return;this.catalogReady=result.valid;this.emit({catalog:result});if(result.requestPage)this.requestPage(this.config.send.mediaPage,result.requestPage);}
      catch(_){this.catalogReady=false;this.collector.reset();this.emit({catalog:{items:[],valid:false,pending:false,revision:'',source_id:null},status:'Station list unavailable'});}
    });
    this.watch('s',r.progress,value=>{
      try{const p=JSON.parse(value);if(p.room_id!==this.config.roomId)return;
        if(typeof p.valid!=='boolean')throw Error('Invalid progress');
        if(p.valid&&(!Number.isInteger(p.source_id)||p.source_id<=0||typeof p.track_revision!=='string'||!/^[a-f0-9]{16}$/.test(p.track_revision)||!Number.isInteger(p.position_seconds)||!Number.isInteger(p.duration_seconds)||p.position_seconds<0||p.duration_seconds<=0||p.duration_seconds>172800||p.position_seconds>p.duration_seconds))throw Error('Invalid progress range');
        this.progress=p.valid?p:null;this.emit({progress:this.connected?this.progress:null});
      }catch(_){this.progress=null;this.emit({progress:null});}
    });
    this.watch('s',r.nowPlaying,value=>{
      if(!value)return;
      try{const packet=this.decode(value,'n',this.config.send.nowPlayingPage);if(!packet)return;
        if(packet.room_id!==this.config.roomId||typeof packet.valid!=='boolean')throw Error('Invalid metadata document');
        this.nowPlaying=packet.valid?packet:null;this.emit({nowPlaying:this.connected&&this.playingValid?this.nowPlaying:null});
      }catch(_){this.nowPlaying=null;this.emit({nowPlaying:null});}
    });
    this.watch('b',r.nowPlayingValid,value=>{
      this.playingValid=!!value;
      if(!value){this.nowPlaying=null;const wire=this.wire.n;if(wire&&wire.epoch){wire.retired.add(wire.epoch);wire.reset();}}
      this.emit({nowPlaying:this.connected&&this.playingValid?this.nowPlaying:null});
    });
    this.publishSelectedSources();
  };
  Bridge.prototype.decode=function(value,kind,join){
    const frame=JSON.parse(value);if(frame.w!==1)return frame;
    if(!this.wire[kind])throw Error('Serial frame decoder not loaded');
    const result=this.wire[kind].accept(frame);if(!result)return null;
    if(result.changed){
      if(kind==='m'){this.catalogReady=false;this.collector.reset();this.emit({catalog:{items:[],valid:false,pending:true,revision:'',source_id:null}});}
      else if(kind==='n'){this.nowPlaying=null;this.emit({nowPlaying:null});}
      else {const mode=kind==='a'?'listen':'watch';this.sources[mode].clear();this.emit(mode==='listen'?{sources:[]}:{videoSources:[]});this.publishSelectedSources();}
    }
    if(result.requestPage)this.requestPage(join,result.requestPage);
    return result.packet||null;
  };
  Bridge.prototype.requestPage=function(join,page){
    if(!this.connected||!join)return;
    const previous=this.pageRequests.get(join);if(previous&&previous.timer)clearTimeout(previous.timer);
    const request={page,timer:null};this.pageRequests.set(join,request);
    const publish=()=>{if(this.pageRequests.get(join)===request&&!this.destroyed)this.send('n',join,page);};
    if(!previous||previous.page===page){this.send('n',join,0);request.timer=setTimeout(publish,25);}
    else publish();
  };
  Bridge.prototype.publishSelectedSources=function(){
    const selectedSources={};
    for(const [mode,prefix]of [['listen','Audio'],['watch','Video']]){
      const index=Number(this.feedback['Sources.'+prefix+'_Source_Selected_FB']||0);
      const valid=!!this.feedback['Sources.'+prefix+'_Source_Selected_Valid_FB'];
      const rows=[];for(const key of Array.from(this.sources[mode].keys()).sort((a,b)=>a-b))rows.push(...this.sources[mode].get(key));
      selectedSources[mode]={index,valid:this.connected&&valid&&index>0&&index<=rows.length,id:valid&&index>0&&index<=rows.length?rows[index-1].id:null};
    }
    this.emit({selectedSources});
  };
  Bridge.prototype.refreshConnection=function(){
    const wasConnected=this.connected;
    this.connected=this.gatewayConnected&&this.transportConnected;
    this.emit({status:this.connected?'Control4 connected':!this.transportConnected?'Crestron connection unavailable':'Control4 disconnected',connected:this.connected});
    this.publishSelectedSources();
    if(wasConnected&&!this.connected){this.progress=null;this.nowPlaying=null;if(this.wire.n)this.wire.n.reset();}
    this.emit({nowPlaying:this.connected&&this.playingValid?this.nowPlaying:null});
    this.emit({progress:this.connected?this.progress:null});
    if(this.connected&&!wasConnected){
      // Serial state replay can arrive before Connected_FB. Resume its pending
      // packet request instead of waiting for the same serial value to change.
      const joins={a:this.config.send.audioPage,v:this.config.send.videoPage,m:this.config.send.mediaPage,n:this.config.send.nowPlayingPage};
      for(const [kind,collector]of Object.entries(this.wire))if(!collector.complete&&collector.pending)this.requestPage(joins[kind],collector.pending);
    }
  };
  Bridge.prototype.setTransportConnected=function(value){this.transportConnected=!!value;this.refreshConnection();};
  Bridge.prototype.send=function(type,join,value){if(this.destroyed||!this.connected||!join)return false;this.com.publishEvent(type,String(join),value);return true;};
  Bridge.prototype.pulse=function(join){if(this.send('b',join,true)){this.activePulses.add(String(join));this.timers.push(setTimeout(()=>{this.com.publishEvent('b',String(join),false);this.activePulses.delete(String(join));},100));}};
  Bridge.prototype.selectSource=function(mode,id){if(this.send('s',this.config.send.sourceId,String(id)))this.pulse(mode==='watch'?this.config.send.selectVideo:this.config.send.selectAudio);};
  Bridge.prototype.selectItem=function(kind,revision,id){
    if(!this.catalogReady||!this.ready||this.collector.revision!==revision)return;
    const items=[];for(const rows of this.collector.pages.values())items.push(...rows);if(!items.some(x=>x.id===String(id)))return;
    this.send('s',this.config.send.mediaRequest,JSON.stringify({room_id:this.config.roomId,kind,revision,id:String(id),request_id:Date.now()+'-'+(++this.sequence)}));
  };
  Bridge.prototype.transport=function(key){if(!this.capabilities[key])return;if(key==='Room_Off'){this.off();return;}this.pulse(this.config.send.transport[key]);};
  Bridge.prototype.volume=function(value){this.send('n',this.config.send.volume,Math.max(0,Math.min(100,Math.round(value))));};
  Bridge.prototype.mute=function(value){this.pulse(value?this.config.send.muteOn:this.config.send.muteOff);};
  Bridge.prototype.off=function(){this.pulse(this.config.send.off);};
  Bridge.prototype.shuffle=function(value){this.send('b',this.config.send.shuffle,!!value);};
  Bridge.prototype.destroy=function(){if(this.destroyed)return;this.destroyed=true;for(const request of this.pageRequests.values())if(request.timer)clearTimeout(request.timer);this.pageRequests.clear();for(const [type,join,id]of this.subs)this.com.unsubscribeState(type,join,id);for(const t of this.timers)clearTimeout(t);for(const join of this.activePulses)this.com.publishEvent('b',join,false);this.activePulses.clear();this.subs=[];this.listener=null;};
  global.CrestronRoomBridge=Bridge;
})(window);
