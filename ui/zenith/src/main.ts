import { HouseStore, type Adapter } from './model/store';
import { MockAdapter } from './adapters/mock';
import './ui/app';

declare const __DEFAULT_ADAPTER__: string;

// Picks the adapter: ?adapter=mock|crestron|contract overrides the build default.
//   crestron  -> existing Panel Room Router joins (serial 101-108, digital 7)
//   contract  -> draft ZenithUI house contract (docs/CONTRACT.md, not on the processor yet)
// Crestron Web XPanel also reads ?host=<processor>&ipId=<0x03>.
async function start(): Promise<void> {
  const params = new URLSearchParams(location.search);
  const choice = params.get('adapter') ?? __DEFAULT_ADAPTER__;
  const store = new HouseStore();
  store.attach(await createAdapter(choice, params));
  const app = document.createElement('zenith-app') as HTMLElement & { store: HouseStore };
  app.store = store;
  document.body.appendChild(app);
}

async function createAdapter(choice: string, params: URLSearchParams): Promise<Adapter> {
  if (choice !== 'crestron' && choice !== 'contract') return new MockAdapter();
  const [{ CrestronContractAdapter }, { PanelRouterAdapter }, CrComLib, xpanel] = await Promise.all([
    import('./adapters/crestron'),
    import('./adapters/panel-router'),
    import('@crestron/ch5-crcomlib/build_bundles/cjs/cr-com-lib.js'),
    import('@crestron/ch5-webxpanel')
  ]);
  (window as unknown as { CrComLib: unknown }).CrComLib = CrComLib;
  // On a TSW the container app provides the connection; in a browser start Web XPanel.
  const factory = xpanel.getWebXPanel(!xpanel.runsInContainerApp());
  if (factory.isActive) {
    const panel = (factory.WebXPanel as { default?: unknown }).default ?? factory.WebXPanel;
    (panel as { initialize(o: { host: string; ipId: string }): void }).initialize({
      host: params.get('host') ?? location.hostname,
      ipId: params.get('ipId') ?? '0x03'
    });
  }
  return choice === 'contract' ? new CrestronContractAdapter(CrComLib as never) : new PanelRouterAdapter(CrComLib as never);
}

void start();
