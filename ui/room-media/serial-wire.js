(function(global){
  'use strict';
  const keys=['Room_Off','Watch','Control4','Listen','List','Custom_1','Custom_2','Custom_3','Guide','Page_Up','Page_Down','Previous','Volume_Up','Volume_Down','Channel_Up','Channel_Down','Up','Down','Left','Right','Select','Mute','Info','Menu','Back','DVR','Rewind','Fast_Forward','Skip_Back','Play','Skip_Forward','Record','Pause','Stop','Red','Green','Yellow','Blue','Digit_1','Digit_2','Digit_3','Digit_4','Digit_5','Digit_6','Digit_7','Digit_8','Digit_9','Star','Digit_0','Pound'];
  function Collector(room,kind){this.room=room;this.kind=kind;this.retired=new Set();this.reset();}
  Collector.prototype.reset=function(){this.epoch=null;this.parts=new Map();this.count=0;this.pending=null;this.complete=false;};
  Collector.prototype.accept=function(frame){
    if(frame.r!==this.room||frame.k!==this.kind||this.retired.has(frame.e))return null;
    if(frame.w!==1||!Number.isInteger(frame.p)||!Number.isInteger(frame.n)||frame.p<1||frame.n<1||frame.p>frame.n||frame.n>2200||typeof frame.e!=='string'||!/^[a-f0-9]{16}$/.test(frame.e)||typeof frame.d!=='string'||frame.d.length>160||!/^[A-Za-z0-9+/]*={0,2}$/.test(frame.d))throw Error('Invalid serial frame');
    let changed=false;
    if(frame.e!==this.epoch){if(this.epoch)this.retired.add(this.epoch);if(this.retired.size>32)this.retired.delete(this.retired.values().next().value);this.reset();this.epoch=frame.e;this.count=frame.n;changed=true;}
    if(this.count!==frame.n)throw Error('Inconsistent serial frame count');
    if(this.complete)return null;
    if(this.parts.has(frame.p)&&this.parts.get(frame.p)!==frame.d)throw Error('Conflicting serial frame');
    this.parts.set(frame.p,frame.d);if(this.pending===frame.p)this.pending=null;
    let missing=0;for(let i=1;i<=this.count;i++)if(!this.parts.has(i)){missing=i;break;}
    if(missing){const requestPage=this.pending===missing?null:missing;this.pending=missing;return {changed,requestPage};}
    let text='';for(let i=1;i<=this.count;i++)text+=this.parts.get(i);
    const bytes=Uint8Array.from(global.atob(text),c=>c.charCodeAt(0));if(bytes.length>262144)throw Error('Serial document too large');
    const packet=JSON.parse(new global.TextDecoder('utf-8',{fatal:true}).decode(bytes));
    if(packet.room_id!==this.room||packet.wire_error)throw Error('Invalid serial document');
    this.complete=true;return {changed,packet};
  };
  function capabilities(packet){
    if(packet.wire!=='keys1')return packet;
    if(typeof packet.states!=='string'||packet.states.length!==keys.length||!/^[alu]+$/.test(packet.states))throw Error('Invalid key capabilities');
    return {room_id:packet.room_id,keys:keys.map((key,index)=>({key,state:{a:'advertised',l:'local_ui',u:'unavailable'}[packet.states[index]]}))};
  }
  global.RoomSerialWire={Collector,capabilities,keys};
})(window);
