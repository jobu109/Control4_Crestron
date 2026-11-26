import { css, html, nothing } from 'lit';
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
class SecurityPage extends ZPage {
  static properties = { ...ZPage.properties, pin: { state: true }, target: { state: true } };
  private pin = '';
  private target?: 'home' | 'away' | 'disarmed';

  private submit(partition: number, action: 'Stay' | 'Away' | 'Disarm'): void {
    this.target = action === 'Stay' ? 'home' : action === 'Away' ? 'away' : 'disarmed';
    const code = this.pin;
    this.pin = '';
    this.store.sendSecurityKeypad(partition, action, code);
  }

  static styles = [shared, css`
    .status { padding: 32px; display: grid; gap: 24px; min-width: 0; }
    dl { display: grid; grid-template-columns: 1fr auto; gap: 16px; margin: 0; }
    dd { margin: 0; font-weight: 700; }
    .content { display: grid; grid-template-columns: minmax(0, 1fr) minmax(0, 1fr); gap: 24px; align-items: start; }
    .zones { max-height: 100%; overflow: auto; }
    .keypad { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 8px; margin-top: 12px; }
    .keypad .code {
      grid-column: 1 / -1; min-height: 44px; display: grid; place-items: center;
      margin-bottom: 6px; padding: 6px 14px; border: 1px solid rgba(225, 238, 255, .24);
      border-radius: 12px; background: linear-gradient(160deg, rgba(229, 241, 255, .16), rgba(229, 241, 255, .04));
      box-shadow: inset 0 1px 0 rgba(255, 255, 255, .12), 0 4px 12px rgba(0, 0, 0, .2);
      color: #eaf2fc; letter-spacing: 5px;
    }
    .keypad .code.empty { color: #c1cad6; letter-spacing: 1px; }
    .zones .open { color: #ff777d; }
    .keypad .btn { height: 37px; padding: 0 4px; }
    .status { max-height: 100%; overflow: auto; }
  `];

  render() {
    const status = this.store.config.securityStatus;
    const result = this.store.config.securityKeypadResult;
    const pending = result === 'pending';
    const mode = status?.mode === 'disarmed' ? 'Disarmed' : status?.mode === 'home' ? 'Armed home'
      : status?.mode === 'away' ? 'Armed away' : 'Unknown';
    return html`<div class="content"><section class="panel status" role="status">
      ${this.store.connection !== 'online' ? html`<p>Security status unavailable while disconnected.</p>`
        : !status ? html`<p>Checking security status…</p>`
        : !status.partition ? html`<p>${status.multiple ? 'Multiple active partitions reported.' : 'Active partition status unavailable.'}</p>`
        : html`<h2>Security System</h2><dl>
          <dt>Arming state</dt><dd>${mode}</dd>
          <dt>Alarm flag</dt><dd>${status.alarm === true ? 'Set' : status.alarm === false ? 'Clear' : 'Unknown'}</dd>
          <dt>Open zones</dt><dd>${status.open ?? 'Unknown'}</dd>
        </dl>
        ${globalThis.crypto?.subtle ? html`<div class="keypad" role="group" aria-label="Security keypad">
          <div class="code ${this.pin ? '' : 'empty'}" aria-label="${this.pin.length} digits entered">${this.pin ? '•'.repeat(this.pin.length) : 'User code'}</div>
          ${['1','2','3','4','5','6','7','8','9','Clear','0','Delete'].map((key) => html`
            <button type="button" class="btn neu-square" ?disabled=${pending} @click=${() => {
              if (key === 'Clear') this.pin = '';
              else if (key === 'Delete') this.pin = this.pin.slice(0, -1);
              else if (this.pin.length < 10) this.pin += key;
            }}>${key}</button>`)}
          ${(['Stay','Away','Disarm'] as const).map((action) => html`
            <button type="button" class="btn neu-square" ?disabled=${pending || (action === 'Disarm' && !this.pin)}
              @click=${() => this.submit(status.partition!, action)}>${action === 'Disarm' ? action : 'Arm ' + action}</button>`)}
        </div>` : html`<p>Keypad requires a secure browser context.</p>`}
        ${this.target && result ? html`<p>${result === 'confirmed' || (result === 'sent' && status.mode === this.target)
          ? 'Partition state confirmed.' : result === 'sent' ? 'Request sent. Waiting for partition confirmation.'
          : pending ? 'Sending request…' : 'Security request could not be confirmed.'}</p>` : nothing}
        ` }
    </section>
    ${this.store.connection === 'online' && this.store.config.securityZones?.length ? html`
      <section class="panel status zones" aria-label="Security zones">
        <h2>Zones</h2><dl>${this.store.config.securityZones.map((zone) => html`
          <dt>${zone.id} · ${zone.name}</dt><dd class=${zone.open === true ? 'open' : ''}>${zone.open === true ? 'Open' : zone.open === false ? 'Closed' : 'Unknown'}</dd>
        `)}</dl>
      </section>` : nothing}
    </div>`;
  }
}

customElements.define('z-pending', PendingPage);
customElements.define('z-security', SecurityPage);
