"""Package plain shared assets and visible Div fragments. No npm required."""
from pathlib import Path
import re,uuid,json,html
base=Path(__file__).resolve().parent.parent
native=base.parent/'construct'/'Floyd Room Media'
out=base/'construct-pages'
out.mkdir(exist_ok=True)
read=lambda n:(base/n).read_text(encoding='utf-8-sig')
css=read('styles.css')+read('construct-fit.css')
js='\n;\n'.join(read(n) for n in ['join-map.js','rooms.js','catalog.js','serial-wire.js','crestron-bridge.js','app.js','preview-data.js','panel-room-transport.js','panel-room-devices.js','construct-pages.js'])
(out/'room-shared.css').write_text(css,encoding='utf-8')
(out/'room-shared.js').write_text(js,encoding='utf-8')
loader=r'''<script>
(function(global){
  // The installed Construct editor places scripted Divs in a non-interactive
  // *_iframe (pointer-events:none). Its canvas does not expose build assets.
  // Keep visible baseline HTML here; do not treat the canvas as a live client.
  var editorCanvas=false;
  try {
    var frame=global.frameElement;
    editorCanvas=!!(frame && /_iframe$/.test(frame.id) && frame.style.pointerEvents==='none');
  } catch (_) {}
  if(editorCanvas){
    document.querySelectorAll('[data-startup-status]').forEach(function(note){note.textContent='Layout preview · Construct editor';});
    return;
  }
  // Project output paths. Adjust if your Construct output uses another location.
  var paths={css:['app/project/assets/css/room-shared.css','assets/css/room-shared.css','room-shared.css'],js:['app/project/assets/javascript/room-shared.js','assets/javascript/room-shared.js','room-shared.js']};
  function load(tag,urls,index){
    return new Promise(function(resolve,reject){
      var element=document.createElement(tag);
      if(tag==='link'){element.rel='stylesheet';element.href=urls[index];}else{element.src=urls[index];element.async=false;}
      element.onload=resolve;
      element.onerror=function(){element.remove();if(index+1<urls.length)load(tag,urls,index+1).then(resolve,reject);else reject(Error('Room media shared asset missing: '+urls.join(' or ')));};
      document.head.appendChild(element);
    });
  }
  // Construct's built index loads the shared implementation in the host.
  // Reuse it across srcdoc Divs; each frame still needs its own stylesheet.
  var host=null;
  try { if(global.parent!==global && global.parent.RoomMediaPages)host=global.parent; } catch (_) {}
  if(host){
    load('link',paths.css,0).then(function(){
      var root=document.querySelector('[data-c4-page]');
      if(!root)return;
      host.RoomMediaPages.attach(root);
      global.addEventListener('pagehide',function(){host.RoomMediaPages.detach(root);},{once:true});
    }).catch(function(error){
      document.querySelectorAll('[data-startup-status]').forEach(function(note){note.textContent='Layout preview: shared CSS could not load. Check Construct output paths.';});
      console.error(error);
    });
    return;
  }
  if(!global.__roomMediaLoadPromise){
    global.__roomMediaLoadPromise=Promise.all([load('link',paths.css,0),load('script',paths.js,0)]).catch(function(error){global.__roomMediaLoadPromise=null;throw error;});
  }
  global.__roomMediaLoadPromise.then(function(){global.RoomMediaPages.start();}).catch(function(error){
    document.querySelectorAll('[data-startup-status]').forEach(function(note){note.textContent='Layout preview: shared assets could not load. Check Construct output paths.';});
    console.error(error);
  });
})(window);
</script>'''
fallback=re.sub(r'<!--.*?-->\s*','',read('construct-div-ready.html'),flags=re.S)
fallback=fallback.replace('<div style=', '<div class="c4-static" style=',1)
fallback=fallback.replace('>Layout preview</div>',' data-startup-status>Layout preview</div>')
baseline='<style>'+read('construct-fit.css')+'</style>'
listen=baseline+'<div data-c4-page="listen">'+fallback+'</div>\n'+loader
watch=r'''<div data-c4-page="watch" style="box-sizing:border-box;width:100%;min-height:780px;background:#101416;color:#f4f5f6;font:16px Segoe UI,Arial,sans-serif;padding:28px;">
<header style="display:flex;justify-content:space-between;align-items:center;"><h1>Living Room · Watch</h1><span data-startup-status style="color:#99a6ab;">Layout preview</span></header>
<div style="display:flex;gap:28px;margin-top:24px;">
<aside style="width:180px;"><h3>Video sources</h3><button disabled style="background:#26463e;color:white;padding:18px;border:1px solid #46776b;border-radius:14px;">DIRECTV Stream</button></aside>
<section style="box-sizing:border-box;width:540px;height:520px;overflow:auto;background:#1a2125;border:1px solid #39484e;border-radius:20px;padding:24px;"><h2>Remote controls</h2><div style="display:grid;grid-template-columns:repeat(3,1fr);gap:16px;">'''
for key in re.findall(r'\b([A-Za-z0-9_]+)_btn\b',(base.parent.parent/'SIMPLPlus'/'Control4_Room_Transport.usp').read_text(encoding='utf-8-sig').split('DIGITAL_OUTPUT',1)[0]):
 watch+='<button disabled style="min-height:72px;background:#29373d;color:white;border:1px solid #3e515b;border-radius:14px;font:inherit;">'+(key[6:] if key.startswith('Digit_') else '*' if key=='Star' else '#' if key=='Pound' else key.replace('_',' '))+'</button>'
watch+='</div><p style="color:#99a6ab;">Transport controls use advertised capabilities.</p></section></div><p style="color:#99a6ab;margin-top:32px;">Visual preview only · controls are not connected</p></div>\n'+loader
watch=baseline+watch
rooms=baseline+r'''<div data-c4-page="rooms" class="rm" style="box-sizing:border-box;width:100%;min-height:780px;padding:32px;background:#101416;color:#f4f5f6;font:16px Segoe UI,Arial,sans-serif;">
<header><div style="font-size:12px;letter-spacing:3px;color:#99a6ab;">HOME / ENTERTAINMENT</div><h1>Choose a room</h1><p data-startup-status style="color:#99a6ab;">Only configured rooms appear here.</p></header>
<div data-room-buttons style="max-width:420px;margin-top:32px;"><button disabled style="width:100%;text-align:left;padding:24px;color:white;background:#26463e;border:1px solid #46776b;border-radius:16px;font:inherit;">Living Room</button></div>
<p style="margin-top:30px;color:#99a6ab;">Select a room, then open Listen or Watch using the project navigation.</p>
</div>'''+loader
for name,content in [('listen',listen),('watch',watch),('rooms',rooms)]:
 (out/(name+'-div.html')).write_text(content,encoding='utf-8')
harness='<!doctype html><html><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Shared room pages preview</title><style>body{margin:0;background:#101416;}nav{padding:10px;background:#253036;display:flex;gap:12px;}nav button{padding:10px 24px;}[hidden]{display:none!important;}main{height:calc(100vh - 64px);}</style></head><body><nav><button data-show="listen">Listen</button><button data-show="watch">Watch</button><button data-show="rooms">Rooms</button></nav><main>'+listen+watch.replace('data-c4-page="watch"','data-c4-page="watch" hidden',1)+rooms.replace('data-c4-page="rooms"','data-c4-page="rooms" hidden',1)+'</main><script>document.querySelectorAll("[data-show]").forEach(b=>b.onclick=()=>document.querySelectorAll("[data-c4-page]").forEach(p=>p.hidden=p.dataset.c4Page!==b.dataset.show));</script></body></html>'
(out/'index.html').write_text(harness,encoding='utf-8')
for folder,name,content in [('css','room-shared.css',css),('javascript','room-shared.js',js)]:
 assetdir=native/'assets'/folder;assetdir.mkdir(parents=True,exist_ok=True)
 (assetdir/name).write_text(content,encoding='utf-8')
 (native/name).write_text(content,encoding='utf-8')
 meta=assetdir/(Path(name).stem+'.cuia')
 if not meta.exists():
  template=(native/'assets'/'javascript'/'app.cuia').read_text(encoding='utf-8-sig')
  template=template.replace('Name = "app"','Name = "room-shared"').replace('SourceUri = "app.js"','SourceUri = "room-shared.js"')
  if folder=='css':
   template=(native/'assets'/'css'/'styles.cuia').read_text(encoding='utf-8-sig').replace('Name = "styles"','Name = "room-shared"').replace('SourceUri = "styles.css"','SourceUri = "room-shared.css"')
  template=re.sub(r'Id = "[^"]+"','Id = "'+str(uuid.uuid4())+'"',template)
  meta.write_text(template,encoding='utf-8',newline='\r\n')
for filename,content,elementid,component in [
 ('Room Module.cuig',listen,'ic4listen','Room Media Listen'),
 ('WatchRemot.cuig',watch,'ic4watch','Room Media Watch'),
 ('RoomSelect.cuig',rooms,'ic4rooms','Room Selection')]:
 page=native/filename;text=page.read_text(encoding='utf-8-sig')
 # Construct loads its model from Elements, not arbitrary page Html alone.
 # The installed html-div component reads customHtmlContent from attributes.
 attributes={'labelmode':'advanced','ccid_lteHTMLOnly':'true',
             'componentName':component,'ccid_ComponentType':'Html-div',
             'id':elementid,'customHtmlContent':content,
             'devicesVisited':'["TSW-1070"]'}
 stub='<html-div '+ ' '.join(key+'="'+html.escape(value,quote=True)+'"' for key,value in attributes.items())+'></html-div>'
 text=re.sub(r'\{Html\}.*?\{Css\}',lambda _:'{Html}\n'+stub+'\n{Css}',text,count=1,flags=re.S)
 cssRule='@media (max-width: 99999px) { #'+elementid+' { display:block; position:absolute; left:0; top:0; width:1280px; height:800px; border:0; background-color:#101416; white-space:normal; } }'
 text=re.sub(r'\{Css\}.*?\{PageAttributes\}',lambda _:'{Css}\n'+cssRule+'\n\n{PageAttributes}',text,count=1,flags=re.S)
 text=text.split('[[Elements]]',1)[0].rstrip()+'\n\n'
 text+='[[Elements]]\nName = ""\nType = "html-div"\nStatus = ""\nContent = ""\n\n[Elements.Attributes]\n'
 text+='\n'.join(key+' = '+json.dumps(value,ensure_ascii=False) for key,value in attributes.items())+'\n'
 page.write_text(text,encoding='utf-8',newline='\r\n')
project=native/'Floyd Control 4.cuip'
project.write_text(project.read_text(encoding='utf-8-sig').replace('IncludeUnusedAsset = "False"','IncludeUnusedAsset = "True"'),encoding='utf-8',newline='\r\n')
print('Prepared shared assets, three visible Divs, preview harness and native solution copy')
