import { HouseStore, type Adapter } from './model/store';
import { MockAdapter } from './adapters/mock';
import { resolveCrComLib } from './adapters/crcomlib';
import { createConnectionNotice, loginUrlFor } from './ui/connection-notice';
import './ui/app';
// Montserrat ships inside the archive so TSW panels without internet keep the design font.
import '@fontsource/montserrat/latin-200.css';
import '@fontsource/montserrat/latin-300.css';
import '@fontsource/montserrat/latin-400.css';
import '@fontsource/montserrat/latin-400-italic.css';
import '@fontsource/montserrat/latin-500.css';
import '@fontsource/montserrat/latin-700.css';

declare const __DEFAULT_ADAPTER__: string;

// Picks the adapter: ?adapter=mock|sim|crestron|contract overrides the build default.
//   sim       -> real Panel Room Router adapter against a simulated router (no processor)
//   crestron  -> existing Panel Room Router joins (serial 101-108, digital 7)
//   contract  -> draft ZenithUI house contract (docs/CONTRACT.md, not on the processor yet)
// Crestron Web XPanel also reads ?host=<processor>&ipId=<0x03>.
async function start(): Promise<void> {
  const params = new URLSearchParams(location.search);
  const choice = params.get('adapter') ?? __DEFAULT_ADAPTER__;
  const store = new HouseStore();
  // Draw the UI first so a connection problem shows "NOT CONNECTED" or an error, never a blank page.
  const app = document.createElement('zenith-app') as HTMLElement & { store: HouseStore };
  app.store = store;
  document.body.appendChild(app);
  try {
    store.attach(await createAdapter(choice, params));
  } catch (error) {
    store.setConnection('offline');
    showStartupError(error);
  }
}

/** Visible startup failure, so a panel never shows a silent blank page. */
function showStartupError(error: unknown): void {
  console.error('Zenith UI startup failed', error);
  const note = document.createElement('div');
  note.setAttribute('role', 'alert');
  note.style.cssText = 'position:fixed;left:16px;right:16px;bottom:16px;padding:12px 16px;border-radius:8px;' +
    'background:rgba(240,71,60,0.92);color:#fff;font:14px/1.4 system-ui,sans-serif;z-index:10';
  note.textContent = `UI startup failed: ${error instanceof Error ? error.message : String(error)}`;
  document.body.appendChild(note);
}


async function createAdapter(choice: string, params: URLSearchParams): Promise<Adapter> {
  // Development only: a production panel must never run a simulated house.
  if (choice === 'sim' && import.meta.env.DEV) {
    const [{ PanelRouterAdapter }, { SimulatedRouter }] = await Promise.all([import('./adapters/panel-router'), import('./adapters/router-sim')]);
    return new PanelRouterAdapter(new SimulatedRouter());
  }
  if (choice !== 'crestron' && choice !== 'contract') return new MockAdapter();
  const [{ CrestronContractAdapter }, { PanelRouterAdapter }, crComLibModule, xpanel] = await Promise.all([
    import('./adapters/crestron'),
    import('./adapters/panel-router'),
    import('@crestron/ch5-crcomlib/build_bundles/cjs/cr-com-lib.js'),
    import('@crestron/ch5-webxpanel')
  ]);
  const CrComLib = resolveCrComLib(crComLibModule, (window as unknown as { CrComLib?: unknown }).CrComLib);
  (window as unknown as { CrComLib: unknown }).CrComLib = CrComLib;
  // On a TSW the container app provides the connection; in a browser start Web XPanel.
  const factory = xpanel.getWebXPanel(!xpanel.runsInContainerApp());
  if (factory.isActive) {
    const panel = ((factory.WebXPanel as { default?: unknown }).default ?? factory.WebXPanel) as {
      initialize(o: { host: string; ipId: string }): void;
      addEventListener?(type: string, listener: (event: { detail?: Record<string, unknown> }) => void): void;
    };
    const host = params.get('host') ?? location.hostname;
    watchConnection(panel, factory.WebXPanelEvents as unknown as Record<string, string>, host);
    panel.initialize({ host, ipId: params.get('ipId') ?? '0x03' });
  }
  return choice === 'contract' ? new CrestronContractAdapter(CrComLib) : new PanelRouterAdapter(CrComLib);
}

/** Web XPanel connects only if this browser is signed in to the processor; say so on screen. */
const CONNECT_TIMEOUT_MS = 15000;

function watchConnection(
  panel: { addEventListener?(type: string, listener: (event: { detail?: Record<string, unknown> }) => void): void },
  events: Record<string, string>,
  host: string
): void {
  if (!panel.addEventListener) return;
  const notice = createConnectionNotice();
  let connected = false;
  const signIn = (event: { detail?: Record<string, unknown> }) => {
    connected = false;
    console.warn('Web XPanel needs a processor sign-in', event.detail);
    notice.show('sign-in', loginUrlFor(event.detail?.redirectTo, host));
  };
  for (const name of ['NOT_AUTHORIZED', 'AUTHENTICATION_REQUIRED', 'AUTHENTICATION_FAILED']) {
    if (events[name]) panel.addEventListener(events[name], signIn);
  }
  if (events.CONNECT_CIP) panel.addEventListener(events.CONNECT_CIP, () => { connected = true; notice.hide(); });
  if (events.DISCONNECT_CIP) panel.addEventListener(events.DISCONNECT_CIP, () => { connected = false; });
  setTimeout(() => { if (!connected && !notice.visible) notice.show('not-connected'); }, CONNECT_TIMEOUT_MS);
}

void start();
