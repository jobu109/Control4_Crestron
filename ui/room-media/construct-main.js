import './join-map.js';
import './catalog.js';
import './serial-wire.js';
import './crestron-bridge.js';
import './app.js';
// Construct owns CrComLib and the native/browser connection. Do not initialize a second WebXPanel.
window.RoomMediaConstruct={mount(root,overrides={}){
  const config={...window.ROOM_MEDIA_CONFIG,...overrides,browser:false};
  if(!config.enabled){root.textContent='Configure the room join map before enabling live controls.';return;}
  if(!window.CrComLib)throw Error('Construct CrComLib is not ready');
  return window.RoomMediaApp.mount(root,{bridge:new window.CrestronRoomBridge(window.CrComLib,config)});
}};
