"""Generate the single pasteable Construct Div from the shared source files."""
from pathlib import Path
import re
base = Path(__file__).resolve().parent.parent
read = lambda name: (base / name).read_text(encoding="utf-8-sig")
css = read("styles.css") + """
.c4-room-wrapper{position:fixed;inset:0;padding:clamp(8px,2vw,26px);background:#101416;overflow:hidden;box-sizing:border-box;}
#c4-room-root{width:100%;height:100%;overflow:auto;box-sizing:border-box;}
#c4-room-root.rm{min-height:0;height:100%;padding:16px;}
#c4-room-root button,#c4-room-root input{touch-action:manipulation;}
"""
# This real HTML is visible in editors even when script execution is disabled.
fallback = re.sub(r"<!--.*?-->\s*", "", read("construct-div-ready.html"), flags=re.S)
config = read("join-map.js")
helpers = "\n\n".join(read(name) for name in ["catalog.js", "serial-wire.js", "crestron-bridge.js", "app.js", "preview-data.js"])
boot = r"""
(function () {
  'use strict';
  /* CRESTRON COMMUNICATION — use the connection Construct already owns. */
  function getCrComLib() {
    var frames = [window];
    try { frames.push(window.parent); } catch (_) {}
    try { frames.push(window.top); } catch (_) {}
    for (var i = 0; i < frames.length; i++) {
      try {
        var candidate = frames[i].CrComLib;
        if (candidate && typeof candidate.publishEvent === 'function' &&
            typeof candidate.subscribeState === 'function') return candidate;
      } catch (_) { /* An ancestor may be on a different origin. */ }
    }
    return null;
  }
  /* ELEMENTS / INITIALIZATION */
  var root = document.getElementById('c4-room-root');
  if (!root || root.dataset.mounted) return;
  var settings = Object.assign({}, window.ROOM_MEDIA_CONFIG, {browser:false});
  if (!settings.enabled) {
    // Offline preview only. This branch never publishes Crestron joins.
    window.c4RoomView = window.RoomMediaApp.mount(root, {data:window.ROOM_MEDIA_PREVIEW});
    return;
  }
  var attempts = 0;
  function connect() {
    var lib = getCrComLib();
    if (lib) {
      window.c4RoomView = window.RoomMediaApp.mount(root, {
        bridge:new window.CrestronRoomBridge(lib, settings)
      });
    } else if (++attempts < 30) {
      // Bounded local initialization check; no Control4/network polling.
      setTimeout(connect, 100);
    } else {
      var note = document.createElement('div');
      note.style.cssText='padding:16px;color:#fff;background:#51372b;';
      note.textContent='Live mode could not find CrComLib. The layout below is a static preview; controls are not connected.';
      root.prepend(note);
    }
  }
  connect();
  // If the containing page is removed, call c4RoomView.destroy() first.
})();
"""
html = """<!-- COMPLETE CONSTRUCT DIV: CSS, visible HTML, then JavaScript. -->
<!-- Dark/teal scheme retained. enabled:false means interactive offline preview. -->
<style>\n""" + css + "\n</style>\n<div class=\"c4-room-wrapper\"><div id=\"c4-room-root\">\n" + fallback + "\n</div></div>\n<script>\n/* ROOM SIGNAL CONFIGURATION — edit the joins below before enabling live mode. */\n" + config + "\n/* SHARED LIST AND UI IMPLEMENTATION */\n" + helpers + "\n" + boot + "\n</script>\n"
(base / "construct-room-div.html").write_text(html, encoding="utf-8")
print('Generated standalone reference construct-room-div.html; native pages are owned by build-construct-pages.py')
