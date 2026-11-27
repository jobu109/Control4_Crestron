import { LitElement, css, html, nothing } from 'lit';
import type { HouseStore } from '../model/store';
import type { ServiceId } from '../model/types';
import { icon } from './icons';
import { squareButtonStyles } from './base';

interface Tile { route: string; name: string; icon: string; service?: ServiceId }

const TILES: Tile[] = [
  { route: 'home', name: 'Home', icon: 'home' },
  { route: 'rooms', name: 'Rooms', icon: 'rooms' },
  { route: 'lighting', name: 'Lighting', icon: 'light', service: 'lighting' },
  { route: 'shades', name: 'Shades', icon: 'shades', service: 'shades' },
  { route: 'comfort', name: 'Comfort', icon: 'comfort' },
  { route: 'media', name: 'Media', icon: 'media', service: 'media' },
  { route: 'audio', name: 'Sessions', icon: 'music', service: 'audio' },
  { route: 'scenes', name: 'Scenes', icon: 'scenes', service: 'scenes' },
  { route: 'security', name: 'Security', icon: 'security' },
  { route: 'pool', name: 'Pool', icon: 'pool', service: 'pool' },
  { route: 'irrigation', name: 'Irrigation', icon: 'irrigation', service: 'irrigation' }
];

/** Dock tiles for the services this house actually has. */
export function dockTiles(store: HouseStore): Tile[] {
  return TILES.filter((tile) => {
    if (tile.route === 'security') {
      const status = store.config.securityStatus;
      return Boolean(status?.partition || status?.multiple);
    }
    if (tile.route === 'comfort') {
      const climate = store.config.directorClimate;
      return Boolean((climate?.available && climate.thermostats.length) || store.has('climate'));
    }
    return !tile.service || store.has(tile.service);
  });
}

/**
 * Top bar, MENU pull-up and dock. On Home the dock is always visible;
 * on every other page it slides up from the MENU tab.
 */
export class ZChrome extends LitElement {
  static properties = {
    store: { attribute: false },
    variant: {},
    heading: {},
    subheading: {},
    back: {},
    current: {},
    open: { state: true }
  };

  store!: HouseStore;
  variant: 'home' | 'page' = 'page';
  heading = '';
  subheading = '';
  back = '';
  current = '';
  open = false;

  static styles = css`
    :host { position: absolute; inset: 0; pointer-events: none; color: #fff; font-family: Montserrat, system-ui, sans-serif; }
    * { box-sizing: border-box; }
    a, button { pointer-events: auto; color: inherit; text-decoration: none; font-family: inherit; cursor: pointer; }
    .bar { position: absolute; left: 40px; right: 40px; top: 20px; height: 64px; display: flex; justify-content: space-between; align-items: center; }
    .left, .right { display: flex; align-items: center; gap: 12px; }
    .sq { width: 44px; height: 44px; border-radius: 10px; display: flex; align-items: center; justify-content: center; border: 1.5px solid rgba(255,255,255,0.85); background: rgba(0,0,0,0.25); }
    .sq.solid { background: #fff; color: #0b0d10; border-color: #fff; }
    .time { font-size: 22px; font-weight: 300; }
    .title { position: absolute; left: 300px; right: 300px; top: 22px; display: flex; flex-direction: column; align-items: center; gap: 2px; text-transform: uppercase; letter-spacing: 0.5px; }
    .title a, .title span:first-child { font-size: 22px; font-weight: 700; pointer-events: auto; }
    .title .sub { font-size: 20px; font-weight: 300; color: #e6e8eb; }
    .menu { position: absolute; left: 530px; bottom: 0; width: 220px; height: 48px; border: none; border-radius: 16px 16px 0 0; background: rgba(255,255,255,0.18); font-size: 15px; font-weight: 700; letter-spacing: 2px; }
    .scrim { position: absolute; inset: 0; border: none; background: rgba(0,0,0,0.55); pointer-events: auto; }
    nav { --square-accent: #fff; --square-glow: #ffffff55; position: absolute; left: 50%; transform: translateX(-50%); width: max-content;
      max-width: calc(100% - 32px); padding: 14px 40px 12px; display: flex; justify-content: center; gap: 12px;
      pointer-events: auto; border-radius: 999px; background: #2c3038;
      box-shadow: inset 4px 4px 8px #1a1c21, inset -4px -4px 8px #3e444f; }
    nav.home { bottom: 22px; }
    nav.sheet { bottom: 0; }
    nav a { width: 72px; display: flex; flex-direction: column; align-items: center; gap: 6px; }
    nav .tile { width: 60px; height: 60px; display: flex; align-items: center; justify-content: center;
      border: 1px solid #3e444f; background: #2c3038; color: #c4cbd4;
      box-shadow: 5px 5px 10px #1a1c21, -4px -4px 9px #3e444f;
      transition: box-shadow .18s, color .18s, transform .08s; }
    nav a[aria-current='page'] .tile, nav a:active .tile {
      color: #fff; border-color: #a4acb7;
      box-shadow: inset 4px 4px 8px #1a1c21, inset -4px -4px 8px #3e444f, 0 0 6px #ffffff55; }
    nav a:active .tile { transform: scale(.96); }
    nav .name { font-size: 11px; font-weight: 500; letter-spacing: 0.3px; color: #e6e8eb; }
    nav a[aria-current='page'] .name { color: #fff; }
    nav a:focus-visible { outline: 2px solid #fff; outline-offset: 3px; }
    ${squareButtonStyles}
    .offline { position: absolute; left: 50%; top: 92px; transform: translateX(-50%); padding: 6px 14px; border-radius: 8px; background: rgba(240,71,60,0.9); font-size: 13px; font-weight: 700; letter-spacing: 1px; pointer-events: auto; }
  `;

  private onChange = () => this.requestUpdate();
  connectedCallback(): void { super.connectedCallback(); this.store?.addEventListener('change', this.onChange); }
  disconnectedCallback(): void { this.store?.removeEventListener('change', this.onChange); super.disconnectedCallback(); }

  private dock(cls: string) {
    return html`<nav class=${cls} aria-label="Main">
      ${dockTiles(this.store).map((t) => html`<a href="#/${t.route}" aria-current=${t.route === this.current ? 'page' : 'false'} @click=${() => (this.open = false)}>
        <span class="tile">${icon(t.icon, 28)}</span><span class="name">${t.name}</span></a>`)}
    </nav>`;
  }

  render() {
    const offline = this.store?.connection === 'offline'
      ? html`<div class="offline" role="status">NOT CONNECTED</div>` : nothing;
    if (this.variant === 'home') return html`${offline}${this.dock('home')}`;
    const time = new Date().toLocaleTimeString([], { hour: 'numeric', minute: '2-digit' });
    return html`
      <div class="bar">
        <div class="left"><a class="sq" href="#/home" aria-label="Home">${icon('home', 22, 1.7)}</a><span class="time">${time}</span></div>
        <div class="right">
          <a class="sq solid" href="#/settings" aria-label="Settings">${icon('dots', 22, 3)}</a>
        </div>
      </div>
      <div class="title">
        ${this.back
          ? html`<a href="#/${this.back}">‹ ${this.heading}</a>`
          : html`<span>${this.heading}</span>`}
        <span class="sub">${this.subheading}</span>
      </div>
      ${offline}
      <button type="button" class="menu" aria-expanded=${this.open ? 'true' : 'false'} @click=${() => (this.open = true)}>MENU</button>
      ${this.open ? html`<button type="button" class="scrim" aria-label="Close menu" @click=${() => (this.open = false)}></button>${this.dock('sheet')}` : nothing}
    `;
  }
}

customElements.define('z-chrome', ZChrome);
