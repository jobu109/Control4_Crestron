import { getWebXPanel, runsInContainerApp } from '@crestron/ch5-webxpanel';
import * as CrComLib from '@crestron/ch5-crcomlib/build_bundles/cjs/cr-com-lib.js';
import '@crestron/ch5-theme/output/themes/dark-theme.css';
import './join-map.js';
import './catalog.js';
import './serial-wire.js';
import './crestron-bridge.js';
import './app.js';

const config=window.ROOM_MEDIA_CONFIG;
if(!config.enabled){
  document.getElementById('room-media-app').textContent='Live mode is disabled. Review join-map.js and wire the corresponding SIMPL joins before enabling it.';
}else{
  const factory=getWebXPanel(!runsInContainerApp());
  const panel=factory.WebXPanel.default||factory.WebXPanel;
  config.browser=factory.isActive;
  window.CrComLib=CrComLib;
  const bridge=new CrestronRoomBridge(CrComLib,config);
  RoomMediaApp.mount(document.getElementById('room-media-app'),{bridge});
  if(factory.isActive){panel.addEventListener(factory.WebXPanelEvents.CONNECT_CIP,()=>bridge.setTransportConnected(true));panel.addEventListener(factory.WebXPanelEvents.DISCONNECT_CIP,()=>bridge.setTransportConnected(false));panel.initialize({host:config.host,ipId:config.ipId});}
}
