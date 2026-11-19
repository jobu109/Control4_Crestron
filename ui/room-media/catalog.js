(function(global){
  'use strict';
  function Collector(roomId,kind){this.roomId=roomId;this.kind=kind;this.reset();}
  Collector.prototype.reset=function(){this.revision=null;this.pages=new Map();this.total=null;this.count=null;this.pending=null;};
  Collector.prototype.accept=function(packet){
    if(!packet||packet.room_id!==this.roomId||packet.kind!==this.kind)return null;
    if(!packet.valid){this.reset();return {valid:false,pending:true,items:[],revision:'',source_id:null};}
    if(typeof packet.revision!=='string'||!packet.revision||!Number.isInteger(packet.page)||!Number.isInteger(packet.pages)||packet.page<1||packet.pages<1||packet.page>packet.pages||packet.pages>2048||!Number.isInteger(packet.total)||packet.total<0||packet.total>2048||!Array.isArray(packet.items))throw Error('Invalid media packet');
    if(packet.items.some(x=>typeof x.id!=='string'||typeof x.name!=='string'))throw Error('Invalid media item');
    if(this.revision!==packet.revision){this.reset();this.revision=packet.revision;this.total=packet.total;this.count=packet.pages;}
    if(packet.total!==this.total||packet.pages!==this.count)throw Error('Inconsistent media revision');
    this.pages.set(packet.page,packet.items);
    if(this.pending===packet.page)this.pending=null;
    let missing=0;for(let i=1;i<=this.count;i++)if(!this.pages.has(i)){missing=i;break;}
    if(missing){const request=this.pending===missing?null:missing;this.pending=missing;return {valid:false,pending:true,items:[],revision:this.revision,source_id:packet.source_id,requestPage:request};}
    const items=[];for(let i=1;i<=this.count;i++)items.push(...this.pages.get(i));
    if(items.length!==this.total||new Set(items.map(x=>x.id)).size!==items.length)throw Error('Incomplete or duplicate media list');
    return {valid:!!packet.selection_supported,pending:false,items,revision:this.revision,source_id:packet.source_id,driver_id:packet.driver_id,shuffle:packet.pandora_shuffle==='On'};
  };
  global.RoomMediaCatalog={Collector};
})(typeof window!=='undefined'?window:globalThis);
