import { css, html, nothing } from 'lit';
import { ZPage, shared, toggle } from '../base';
import { icon } from '../icons';
import { HOUSE_WEATHER, type WeatherState } from '../../model/types';
import { favoriteActions, readFavoriteKeys } from '../../model/favorites';
import { sunTimes, validCoordinates } from '../../model/sun';
import { humidityText, weatherOverview, windText } from '../../model/weather';

export class HomePage extends ZPage {
  static properties = {
    ...ZPage.properties, scenesOpen: { state: true }, favoritesOpen: { state: true }, editFavorites: { state: true }
  };
  scenesOpen = false;
  favoritesOpen = false;
  editFavorites = false;
  private favoriteKeys: string[] = [];
  private favoriteStorageKey = '';
  private timer?: number;

  static styles = [shared, css`
    .side { position: absolute; left: 0; top: 0; bottom: 0; width: 520px; background: rgba(6,8,11,0.62); }
    .bar { position: absolute; left: 40px; right: 40px; top: 24px; display: flex; justify-content: space-between; align-items: center; }
    .bar > div { display: flex; gap: 10px; align-items: center; }
    .sq { width: 44px; height: 44px; border-radius: 10px; display: flex; align-items: center; justify-content: center; border: 1.5px solid rgba(255,255,255,0.85); background: rgba(0,0,0,0.25); }
    .sq.solid { background: #fff; color: #0b0d10; border-color: #fff; }
    .greet { position: absolute; left: 56px; top: 104px; width: 420px; display: flex; flex-direction: column; gap: 28px; }
    .site { display: flex; flex-direction: column; gap: 18px; font-size: 20px; font-weight: 300; }
    .site .k { display: block; color: #a9afb6; }
    .site .v { display: block; color: #fff; font-weight: 400; overflow: hidden; display: -webkit-box; -webkit-box-orient: vertical; -webkit-line-clamp: 2; }
    .site .v.na { color: #a9afb6; font-style: italic; font-weight: 300; }
    .site .place { font-size: 24px; font-weight: 400; color: #fff; }
    .site .now { display: flex; flex-direction: column; gap: 4px; }
    .site .overview { color: #d7dbe0; font-weight: 300; font-style: italic; }
    .site .sun { display: grid; grid-template-columns: 130px 1fr; row-gap: 6px; }
    .site .sun .v { font-variant-numeric: tabular-nums; }
    .greet h1 { font-size: 56px; font-weight: 700; line-height: 1.05; }
    .date { display: flex; flex-direction: column; gap: 2px; font-size: 20px; }
    .date .day { font-size: 68px; font-weight: 300; line-height: 1.05; }
    .clock { position: absolute; left: 600px; top: 250px; display: flex; flex-direction: column; gap: 24px; }
    .clock .t { font-size: 96px; font-weight: 300; line-height: 1; letter-spacing: -1px; }
    .status { display: flex; flex-direction: column; gap: 6px; font-size: 16px; }
    .tab { position: absolute; right: 0; top: 220px; width: 44px; height: 150px; border: none; border-radius: 12px 0 0 12px; background: rgba(255,255,255,0.16); font-size: 12px; font-weight: 700; letter-spacing: 2px; writing-mode: vertical-rl; }
    .favorites-tab { top: 386px; }
    .favorites { gap: 16px; }
    .favorites header { display: flex; align-items: center; justify-content: space-between; gap: 16px; }
    .favorites h2 { font-size: 24px; }
    .favorite-list { display: flex; flex-direction: column; gap: 8px; overflow-y: auto; padding-bottom: 24px; }
    .favorite { min-height: 64px; display: flex; align-items: center; justify-content: space-between; gap: 16px; text-align: left; }
    .favorite span { display: block; }
    .favorite small { color: #a9afb6; }
    .favorite-edit { flex: 0 0 auto; }
    .scenes { position: absolute; right: 0; top: 0; bottom: 130px; width: 620px; background: rgba(6,8,11,0.94); border-left: 1px solid rgba(255,255,255,0.12); padding: 24px 48px 0 40px; display: flex; flex-direction: column; }
    .scenes .row { flex: 1; max-height: 104px; gap: 28px; border-color: rgba(255,255,255,0.14); }
    .scenes .tile { width: 64px; height: 64px; border-radius: 12px; }
    .scenes .n { flex: 1; font-size: 24px; font-weight: 700; }
    .x { width: 48px; height: 48px; margin-left: -12px; border: none; background: transparent; display: flex; align-items: center; justify-content: center; }
    .all { height: 64px; display: flex; align-items: center; gap: 8px; font-size: 14px; font-weight: 700; letter-spacing: 1px; color: #e6e8eb; }
  `];

  connectedCallback(): void {
    super.connectedCallback();
    const params = new URLSearchParams(location.search);
    this.favoriteStorageKey = JSON.stringify(['sytrohs-favorites', params.get('host') ?? location.hostname,
      params.get('ipId') ?? '0x03', params.get('adapter') ?? 'default']);
    try { this.favoriteKeys = readFavoriteKeys(window.localStorage.getItem(this.favoriteStorageKey)); }
    catch { /* This panel may block persistent browser storage. */ }
    this.timer = window.setInterval(() => this.requestUpdate(), 15000);
  }
  disconnectedCallback(): void {
    window.clearInterval(this.timer);
    super.disconnectedCallback();
  }

  render() {
    const now = new Date();
    const scenes = this.store.has('scenes') ? this.store.config.scenes.slice(0, 5) : [];
    const canShowFavorites = this.favoriteKeys.length > 0 || favoriteActions(this.store.config).length > 0;
    return html`
      <div class="side"></div>
      <div class="bar">
        <div><span class="sq solid">${icon('home', 22, 1.7)}</span></div>
        <div><a class="sq solid" href="#/settings" aria-label="Settings">${icon('dots', 22, 3)}</a></div>
      </div>
      <div class="greet">
        <h1>Welcome<br />Home</h1>
        <div class="date">
          <span>${now.toLocaleDateString([], { weekday: 'long' })}</span>
          <span class="day">${String(now.getDate()).padStart(2, '0')}</span>
          <span>${now.toLocaleDateString([], { month: 'long' })}</span>
        </div>
        ${this.siteInfo(now)}
      </div>
      <div class="clock">
        <span class="t">${now.toLocaleTimeString([], { hour: 'numeric', minute: '2-digit' })}</span>
        <div class="status">${this.statusLines()}</div>
      </div>
      ${scenes.length ? html`<button type="button" class="tab" aria-expanded=${this.scenesOpen ? 'true' : 'false'} @click=${() => { this.favoritesOpen = false; this.scenesOpen = true; }}>SCENES</button>` : nothing}
      ${canShowFavorites ? html`<button type="button" class="tab favorites-tab" aria-expanded=${this.favoritesOpen}
        @click=${() => { this.scenesOpen = false; this.favoritesOpen = true; }}>FAVORITES</button>` : nothing}
      ${this.favoritesOpen ? this.favoriteDrawer() : nothing}
      ${this.scenesOpen ? html`<section class="scenes" aria-label="Scenes">
        <button type="button" class="x" aria-label="Close scenes" @click=${() => (this.scenesOpen = false)}>${icon('chevronRight', 26, 1.8)}</button>
        ${scenes.map((s) => {
          const active = !!this.store.get('scene', s.id)?.active;
          return html`<div class="row"><span class="tile ${active ? 'on' : ''}">${icon(s.icon ?? 'scenes', 32, 1.4)}</span><span class="n">${s.name}</span>
            ${toggle(active, () => this.send({ kind: 'scene', id: s.id, action: 'run' }), `${s.name} ${active ? 'is on' : 'is off'}`)}</div>`;
        })}
        <a class="all" href="#/scenes">ALL SCENES ${icon('chevronRight', 18, 1.8)}</a>
      </section>` : nothing}
    `;
  }

  private saveFavoriteKeys(keys: string[]): void {
    this.favoriteKeys = keys;
    try { window.localStorage.setItem(this.favoriteStorageKey, JSON.stringify(keys)); }
    catch { /* The current session still retains its pins. */ }
    this.requestUpdate();
  }

  private favoriteDrawer() {
    const actions = favoriteActions(this.store.config);
    const available = new Map(actions.map((action) => [action.key, action]));
    const pinned = new Set(this.favoriteKeys);
    const visible = this.favoriteKeys.flatMap((key) => {
      const action = available.get(key);
      return action ? [action] : [];
    });
    const online = this.store.connection === 'online';
    return html`<section class="scenes favorites" aria-label="Favorites">
      <header>
        <button type="button" class="x" aria-label="Close favorites"
          @click=${() => { this.favoritesOpen = false; this.editFavorites = false; }}>${icon('chevronRight', 26, 1.8)}</button>
        <h2>Favorites</h2>
        <button type="button" class="btn outline favorite-edit"
          @click=${() => (this.editFavorites = !this.editFavorites)}>${this.editFavorites ? 'Done' : 'Edit'}</button>
      </header>
      <div class="favorite-list">
        ${this.editFavorites ? actions.map((action) => html`
          <button type="button" class="btn outline favorite" aria-pressed=${pinned.has(action.key)}
            @click=${() => this.saveFavoriteKeys(pinned.has(action.key)
              ? this.favoriteKeys.filter((key) => key !== action.key) : [...this.favoriteKeys, action.key])}>
            <span>${action.name}<small>${action.detail}</small></span>
            <span>${pinned.has(action.key) ? 'Remove' : 'Pin'}</span>
          </button>`) : visible.map((action) => html`
          <button type="button" class="btn outline favorite" ?disabled=${!online} @click=${() => {
            const current = favoriteActions(this.store.config).find((entry) => entry.key === action.key);
            if (this.store.connection === 'online' && current) this.send(current.command);
          }}><span>${action.name}<small>${action.detail}</small></span></button>`)}
        ${!this.editFavorites && !visible.length ? html`<span class="sub">No favorites available. Select Edit to pin an action.</span>` : nothing}
        ${this.editFavorites && online && visible.length < this.favoriteKeys.length ? html`
          <button type="button" class="btn outline" @click=${() => this.saveFavoriteKeys(this.favoriteKeys.filter((key) => available.has(key)))}>
            Remove unavailable favorites
          </button>` : nothing}
      </div>
    </section>`;
  }

  /** Place name, current weather with an overview, humidity, wind and today's sunrise/sunset. */
  private siteInfo(now: Date) {
    const location = this.store.config.location;
    const weather = this.store.get('weather', HOUSE_WEATHER);
    const value = (text: string | undefined) => text
      ? html`<span class="v">${text}</span>`
      : html`<span class="v na">Unavailable</span>`;
    let sunrise: string | undefined;
    let sunset: string | undefined;
    let daylight: boolean | undefined;
    if (location && validCoordinates(location.latitude, location.longitude)) {
      const sun = sunTimes(new Date(now.getFullYear(), now.getMonth(), now.getDate(), 12), location.latitude!, location.longitude!);
      const time = (d: Date) => d.toLocaleTimeString([], { hour: 'numeric', minute: '2-digit' });
      sunrise = sun.sunrise ? time(sun.sunrise) : sun.polar === 'day' ? 'Sun up all day' : sun.polar === 'night' ? 'No sunrise today' : undefined;
      sunset = sun.sunset ? time(sun.sunset) : sun.polar === 'day' ? 'No sunset today' : sun.polar === 'night' ? 'Sun down all day' : undefined;
      if (sun.sunrise && sun.sunset) daylight = now >= sun.sunrise && now < sun.sunset;
      else if (sun.polar) daylight = sun.polar === 'day';
    }
    const current = weatherText(weather);
    const overview = weatherOverview(weather, daylight);
    return html`<div class="site" aria-label="Location and weather">
      ${location?.name ? html`<span class="place">${location.name}</span>` : nothing}
      <div class="now">
        ${value(current)}
        ${overview ? html`<span class="overview">${overview}</span>` : nothing}
      </div>
      <div class="sun">
        <span class="k">Humidity</span>${value(humidityText(weather))}
        <span class="k">Wind</span>${value(windText(weather))}
        <span class="k">Sunrise</span>${value(sunrise)}
        <span class="k">Sunset</span>${value(sunset)}
      </div>
    </div>`;
  }

  /** Short house summary; adapters can enrich this later with door/lock states. */
  private statusLines() {
    const rooms = this.store.config.rooms;
    const lit = rooms.filter((r) => this.store.lightsOn(r) > 0).length;
    const playing = rooms.filter((r) => this.store.get('media', r.id)?.power).length;
    // Summaries only describe what the system has reported, never a default "off".
    const lightsKnown = rooms.some((r) => r.lights.some((l) => this.store.known('light', l.id)));
    const mediaKnown = rooms.some((r) => this.store.get('media', r.id)?.power !== undefined);
    return html`
      ${this.store.has('lighting') && lightsKnown ? html`<span>Lights are <em>on in ${lit} ${lit === 1 ? 'room' : 'rooms'}</em></span>` : nothing}
      ${this.store.has('media') && mediaKnown ? html`<span>Media is <em>${playing ? `playing in ${playing}` : 'off'}</em></span>` : nothing}
    `;
  }
}

/** "22°C · Partly cloudy", or whichever parts are reported. */
export function weatherText(w: WeatherState | undefined): string | undefined {
  if (!w) return undefined;
  const parts: string[] = [];
  if (typeof w.temperature === 'number' && Number.isFinite(w.temperature)) parts.push(`${Math.round(w.temperature)}°${w.unit ?? ''}`);
  if (w.condition) parts.push(w.condition);
  if (typeof w.low === 'number' && typeof w.high === 'number') parts.push(`H ${Math.round(w.high)}° L ${Math.round(w.low)}°`);
  return parts.length ? parts.join(' · ') : undefined;
}

customElements.define('z-home', HomePage);
