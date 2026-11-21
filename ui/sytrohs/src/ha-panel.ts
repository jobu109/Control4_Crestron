import { HouseStore } from './model/store';
import { HomeAssistantAdapter, type HassLike } from './adapters/home-assistant';
import './ui/app';

// Home Assistant custom panel. Home Assistant creates <sytrohs-panel> and sets
// `hass` on it every time any state changes.
class SytrohsPanel extends HTMLElement {
  private store = new HouseStore();
  private adapter = new HomeAssistantAdapter();
  private mounted = false;

  set hass(hass: HassLike) {
    if (!this.mounted) {
      this.mounted = true;
      this.store.attach(this.adapter);
      this.adapter.update(hass);
      this.style.display = 'block';
      this.style.height = '100%';
      const app = document.createElement('sytrohs-app') as HTMLElement & { store: HouseStore };
      app.store = this.store;
      this.appendChild(app);
      loadFont();
      return;
    }
    this.adapter.update(hass);
  }
}

function loadFont(): void {
  if (document.querySelector('link[data-sytrohs-font]')) return;
  const link = document.createElement('link');
  link.rel = 'stylesheet';
  link.dataset.sytrohsFont = '';
  link.href = 'https://fonts.googleapis.com/css2?family=Montserrat:ital,wght@0,200;0,300;0,400;0,500;0,700;1,400&display=swap';
  document.head.appendChild(link);
}

if (!customElements.get('sytrohs-panel')) customElements.define('sytrohs-panel', SytrohsPanel);
