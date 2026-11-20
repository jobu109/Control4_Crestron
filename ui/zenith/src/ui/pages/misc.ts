import { css, html } from 'lit';
import { ALL_SERVICES } from '../../model/types';
import { ZPage, shared } from '../base';
import { icon } from '../icons';

export class ScenesPage extends ZPage {
  static properties = { ...ZPage.properties, filter: { state: true }, sent: { state: true } };
  /** Scene description (kind) to show, or '' for all. */
  filter = '';
  /** Scene just tapped; shown as "Sent" briefly because most systems report no scene state. */
  sent = '';
  private sentTimer?: number;

  static styles = [shared, css`
    /* Fixed row height so long scene lists scroll instead of squeezing. */
    .content { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); grid-auto-rows: 92px; align-content: start; gap: 12px; overflow-y: auto; padding-right: 4px; }
    .scene { position: relative; border-radius: 14px; padding: 0 16px; display: flex; align-items: center; gap: 14px; text-align: left; background: var(--panel); border: 1px solid var(--line); min-width: 0; }
    .scene.active { border: 2px solid #fff; background: rgba(8,10,13,0.8); }
    .scene.sent { border-color: var(--green); }
    .scene .tile { width: 52px; height: 52px; border-radius: 12px; }
    .scene .text { min-width: 0; display: flex; flex-direction: column; gap: 2px; }
    .scene .name { font-size: 17px; line-height: 1.2; overflow: hidden; display: -webkit-box; -webkit-box-orient: vertical; -webkit-line-clamp: 2; }
    .badge { position: absolute; top: 8px; right: 10px; height: 22px; padding: 0 8px; border-radius: 11px; background: var(--green); color: #0b0d10; font-size: 10px; font-weight: 700; letter-spacing: 1px; display: inline-flex; align-items: center; }
    .filters { display: flex; gap: 6px; }
    .filters .btn { height: 40px; padding: 0 16px; font-size: 12px; }
  `];

  disconnectedCallback(): void {
    window.clearTimeout(this.sentTimer);
    super.disconnectedCallback();
  }

  private run(id: string): void {
    this.send({ kind: 'scene', id, action: 'run' });
    this.sent = id;
    window.clearTimeout(this.sentTimer);
    this.sentTimer = window.setTimeout(() => (this.sent = ''), 1500);
  }

  render() {
    const scenes = this.store.config.scenes;
    const kinds = [...new Set(scenes.map((s) => s.description).filter((d): d is string => !!d))];
    const filter = kinds.includes(this.filter) ? this.filter : '';
    const shown = filter ? scenes.filter((s) => s.description === filter) : scenes;
    return html`
      <div class="toolbar">
        <span class="hint">Tap a scene to run it · ${shown.length} ${shown.length === 1 ? 'scene' : 'scenes'}</span>
        ${kinds.length > 1 ? html`<span class="filters" role="tablist" aria-label="Scene type">
          ${['', ...kinds].map((k) => html`<button type="button" role="tab" class="btn ${k === filter ? 'sel' : ''}" aria-selected=${k === filter ? 'true' : 'false'}
            @click=${() => (this.filter = k)}>${k ? k.toUpperCase() : 'ALL'}</button>`)}
        </span>` : ''}
      </div>
      <section class="content" aria-label="Scenes">
        ${shown.map((s) => {
          const active = !!this.store.get('scene', s.id)?.active;
          const sent = this.sent === s.id;
          return html`<button type="button" class="scene ${active ? 'active' : ''} ${sent ? 'sent' : ''}" @click=${() => this.run(s.id)}>
            <span class="tile ${active ? 'on' : ''}">${icon(s.icon ?? 'scenes', 26, 1.4)}</span>
            <span class="text"><span class="name">${s.name}</span><span class="sub">${sent ? 'Sent' : s.description ?? ''}</span></span>
            ${active ? html`<span class="badge">ACTIVE</span>` : ''}
          </button>`;
        })}
      </section>`;
  }
}

/** Settings: shows the services this house reports and the connection. */
export class SettingsPage extends ZPage {
  static styles = [shared, css`
    .content { display: flex; gap: 20px; }
    section { flex: 1; padding: 22px 30px; display: flex; flex-direction: column; }
    .grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); column-gap: 30px; }
    .grid .row { height: 64px; justify-content: space-between; font-size: 16px; }
    .pill { height: 30px; padding: 0 12px; border-radius: 15px; font-size: 12px; font-weight: 700; letter-spacing: 1px; display: inline-flex; align-items: center; background: rgba(255,255,255,0.16); }
    .pill.on { background: var(--green); color: #0b0d10; }
    dl { display: grid; grid-template-columns: 180px 1fr; row-gap: 14px; font-size: 16px; margin: 18px 0 0; }
    dt { color: var(--soft); }
    dd { margin: 0; }
  `];

  render() {
    const c = this.store.config;
    return html`
      <div class="toolbar"><span class="hint">This panel</span></div>
      <div class="content">
        <section class="panel" aria-label="Services">
          <div class="head label"><span>Services in this house</span><span>Hidden services have no dock tile</span></div>
          <div class="grid">
            ${ALL_SERVICES.map((s) => html`<div class="row"><span style="text-transform:capitalize">${s}</span><span class="pill ${this.store.has(s) ? 'on' : ''}">${this.store.has(s) ? 'SHOWN' : 'HIDDEN'}</span></div>`)}
          </div>
        </section>
        <section class="panel" aria-label="About" style="flex:0.8">
          <div class="head label"><span>About</span></div>
          <dl>
            <dt>Connection</dt><dd>${this.store.connection}</dd>
            <dt>House</dt><dd>${c.name || '—'}</dd>
            <dt>Floors</dt><dd>${c.floors.length}</dd>
            <dt>Rooms</dt><dd>${c.rooms.length}</dd>
            <dt>Audio zones</dt><dd>${c.audioZones.length}</dd>
            <dt>UI version</dt><dd>0.1.0</dd>
          </dl>
        </section>
      </div>`;
  }
}

/** Placeholder for designed pages whose data model is not built yet. */
export class PendingPage extends ZPage {
  static styles = [shared, css`
    .content { display: flex; align-items: center; justify-content: center; }
    p { max-width: 640px; text-align: center; font-size: 20px; line-height: 1.5; color: #e6e8eb; padding: 40px; }
  `];

  render() {
    return html`<div class="content"><p class="panel">This page is designed but not wired yet. It will appear once the program or Home Assistant reports this service.</p></div>`;
  }
}

customElements.define('z-scenes', ScenesPage);
customElements.define('z-settings', SettingsPage);
customElements.define('z-pending', PendingPage);
