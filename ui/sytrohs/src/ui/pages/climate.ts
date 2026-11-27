import { css, html, nothing } from 'lit';
import { ZPage, iconButton, shared } from '../base';
import { icon } from '../icons';
import type { DirectorThermostat } from '../../model/types';

const systemModes: Record<string, { glyph: string; tone: string; description: string }> = {
  heat: { glyph: 'flame', tone: 'heat', description: 'Heating only' },
  cool: { glyph: 'snowflake', tone: 'cool', description: 'Cooling only' },
  auto: { glyph: 'opposingArrows', tone: '', description: 'Heats or cools as needed' },
  off: { glyph: 'power', tone: 'muted', description: 'No heating or cooling' },
  auxheat: { glyph: 'energy', tone: 'aux', description: 'Auxiliary heat only' }
};

const fanModes: typeof systemModes = {
  auto: { glyph: 'opposingArrows', tone: '', description: 'Runs as needed' },
  on: { glyph: 'fan', tone: '', description: 'Runs continuously' },
  off: { glyph: 'power', tone: 'muted', description: 'Fan off' }
};

function thermostatMode(mode: string | undefined, fan = false) {
  const key = (mode ?? '').toLowerCase().replace(/[ _-]/g, '');
  return (fan ? fanModes : systemModes)[key] ?? { glyph: fan ? 'fan' : 'opposingArrows', tone: '', description: '' };
}

export class ClimatePage extends ZPage {
  static properties = { ...ZPage.properties, thermostatId: { state: true }, modeView: { state: true }, drag: { state: true } };
  thermostatId = '';
  private modeView: 'hvac' | 'fan' | null = null;
  private drag?: { id: number; kind: 'heat' | 'cool'; pointer: number; start: number; y: number; pixels: number; value: number };

  static styles = [shared, css`
    .content { top: 108px; height: 620px; left: 40px; right: 40px; display: flex; gap: 20px; }
    .stat { flex: 1; padding: 28px; display: flex; align-items: center; gap: 28px; }
    .opts { width: 240px; display: flex; flex-direction: column; gap: 12px; }
    .opts h2 { font-size: 22px; font-weight: 700; margin-top: 8px; }
    .grid2 { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 10px; }
    .grid2 .btn { height: 60px; font-size: 16px; }
    .dial { width: 320px; height: 320px; position: relative; flex-shrink: 0; }
    .dial .mid { position: absolute; inset: 0; display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 4px; }
    .dial .sp { font-size: 100px; font-weight: 200; line-height: 1; }
    .side { display: flex; flex-direction: column; gap: 16px; }
    .side .btn { width: 76px; height: 76px; border-radius: 16px; }
    .pick { display: flex; gap: 8px; }
    .thermostat-stage { position: absolute; inset: 96px 40px 66px; display: grid; place-items: center; }
    .thermostat-select { position: absolute; top: 12px; left: 12px; max-width: 230px; display: grid; gap: 8px; }
    .thermostat-device {
      position: relative; width: 610px; height: 610px; padding: 27px; border-radius: 108px;
      background: linear-gradient(145deg, #343438, #0e0e10 37%, #060607 69%, #1c1c21);
      box-shadow: 0 28px 46px #000a, inset 0 2px 2px #ffffff36, inset 0 -3px 6px #000;
    }
    .thermostat-device::before {
      content: ''; position: absolute; top: 11px; left: calc(50% - 3px);
      width: 6px; height: 6px; border-radius: 50%; background: #09090b;
      box-shadow: inset 0 1px 1px #6668;
    }
    .thermostat-screen {
      position: relative; width: 100%; height: 100%; overflow: hidden; border-radius: 83px;
      color: #f7f7f9; background: radial-gradient(ellipse at 10% 0%, #202026, #080809 42%, #020203 80%);
      box-shadow: inset 0 0 0 1px #ffffff10;
    }
    .device-heading { position: absolute; top: 38px; left: 92px; right: 92px; text-align: center; }
    .device-heading h2 { font-size: 20px; font-weight: 600; }
    .device-heading span { color: #a6a6af; font-size: 14px; }
    .device-shortcuts { position: absolute; top: 126px; left: 33px; display: grid; gap: 16px; }
    .device-indicator {
      width: 69px; height: 69px; border-radius: 50%; display: grid; align-content: center;
      justify-items: center; gap: 3px; background: #1d1d23; box-shadow: inset 0 1px 0 #ffffff15;
    }
    .device-indicator small { color: #bdbdc5; font-size: 11px; }
    a.device-indicator:focus-visible { outline: 2px solid #fff; outline-offset: 4px; }
    .device-reading {
      position: absolute; inset: 138px 98px auto; text-align: center; display: grid; justify-items: center; gap: 11px;
    }
    .device-temperature { font-size: 154px; line-height: 1.08; font-weight: 200; letter-spacing: -7px; font-variant-numeric: tabular-nums; }
    .device-temperature small { font-size: 44px; letter-spacing: 0; vertical-align: top; line-height: 1.9; }
    .device-status { color: #dbdbe2; font-size: 18px; }
    .device-humidity { color: #a7a7b0; font-size: 15px; display: inline-flex; align-items: center; gap: 6px; }
    .temperature-rail { position: absolute; top: 112px; bottom: 96px; right: 30px; width: 56px; }
    .temperature-track {
      position: absolute; inset: 0 auto 0 26px; width: 4px; border-radius: 4px;
      background: linear-gradient(#5d3023, #37373d 50%, #24445c);
    }
    .temperature-tick { position: absolute; left: 23px; width: 10px; height: 1px; background: #767680; }
    .temperature-current {
      position: absolute; left: 17px; width: 22px; height: 3px; background: #fff;
      border-radius: 2px; box-shadow: 0 0 6px #fff8; transform: translateY(-50%);
    }
    .temperature-marker {
      position: absolute; left: 6px; width: 44px; height: 44px; display: grid; place-items: center;
      border-radius: 50%; transform: translateY(-50%); font-size: 15px; font-weight: 700;
      color: #101015; box-shadow: 0 3px 12px #000a;
    }
    .temperature-marker.heat { background: #ff8b54; }
    .temperature-marker.cool { background: #60bfff; }
    .device-setpoints {
      position: absolute; bottom: 34px; left: 100px; right: 100px;
      display: flex; flex-wrap: wrap; justify-content: center; gap: 8px; font-size: 13px;
    }
    .device-setpoints span { padding: 8px 13px; border-radius: 999px; background: #222229; }
    .device-setpoints .heat { color: #ffa47e; }
    .device-setpoints .cool { color: #84cdff; }
    button.device-indicator { border: 0; }
    .temperature-marker { z-index: 1; border: 0; touch-action: none; user-select: none; cursor: grab; }
    .temperature-marker.dragging, .temperature-marker:active { cursor: grabbing; z-index: 2; }
    .temperature-marker:disabled { cursor: default; }
    .temperature-marker:focus-visible, button.device-indicator:focus-visible { outline: 2px solid white; outline-offset: 3px; }
    .temperature-current { pointer-events: none; }
    .mode-screen { position: absolute; inset: 40px 54px 44px; overflow: auto; }
    .mode-heading { display: grid; grid-template-columns: 44px 1fr 44px; align-items: center; text-align: center; margin-bottom: 16px; }
    .mode-heading .btn { width: 44px; height: 44px; padding: 0; border-radius: 50%; }
    .mode-row { display: flex; align-items: center; gap: 14px; width: 100%; min-height: 56px;
      text-align: left; background: transparent; border: 0; border-bottom: 1px solid #292930; font-size: 17px; text-transform: capitalize; }
    .mode-row .mode-label { flex: 1; }
    .mode-label small { display: block; margin-top: 3px; color: #a7a7b0; font-size: 12px; text-transform: none; }
    .mode-glyph { width: 28px; display: grid; place-items: center; flex-shrink: 0; }
    .mode-glyph.heat { color: #ff8b54; }
    .mode-glyph.cool { color: #60bfff; }
    .mode-glyph.aux { color: #ff6161; }
    .mode-glyph.muted { color: #9a9aa3; }
    .mode-row strong { color: #67d994; }
    .mode-row:disabled { opacity: .5; }
    .climate-result { position: absolute; bottom: 12px; left: 92px; right: 92px; color: #bdbdc5; text-align: center; font-size: 12px; }
  `];

  render() {
    const director = this.store.config.directorClimate;
    if (director) {
      if (!director.available || !director.thermostats.length) return html`<div class="toolbar">
        <p class="hint" role="status">${director.available ? 'No thermostats reported' : 'Thermostat readings unavailable'}</p>
      </div>`;
      const selected = director.thermostats.find((item) => String(item.id) === this.thermostatId) ?? director.thermostats[0];
      return html`<div class="thermostat-stage">
        ${director.thermostats.length > 1 ? html`<div class="thermostat-select" role="group" aria-label="Thermostat">
          ${director.thermostats.map((item) => html`<button type="button" class="btn ${item.id === selected.id ? 'sel' : ''}"
            aria-pressed=${item.id === selected.id} @click=${() => { this.thermostatId = String(item.id); this.modeView = null; this.drag = undefined; }}>${item.name}</button>`)}
        </div>` : nothing}
        ${this.device(selected)}
      </div>`;
    }
    const list = this.store.config.thermostats;
    const t = list.find((x) => x.id === this.thermostatId) ?? list[0];
    if (!t) return html`<div class="toolbar"><span class="hint">No thermostats reported</span></div>`;
    const st = this.store.get('thermostat', t.id) ?? { power: false, mode: '', fanMode: '', setpoint: 0, current: 0 };
    const circ = 2 * Math.PI * 140;
    const arc = circ * 0.75;
    const fill = arc * Math.min(1, Math.max(0, (st.setpoint - 50) / 40));
    return html`
      ${list.length > 1 ? html`<div class="toolbar" style="top:104px;left:40px;justify-content:flex-start"><div class="pick">
        ${list.map((x) => html`<button type="button" class="btn ${x.id === t.id ? 'sel' : ''}" @click=${() => (this.thermostatId = x.id)}>${x.name}</button>`)}
      </div></div>` : nothing}
      <div class="content" style=${list.length > 1 ? 'top:168px;height:560px' : ''}>
        <section class="panel stat" aria-label="${t.name} thermostat">
          <div class="opts">
            ${t.fanModes.length ? html`<h2>Fan Speed</h2><div class="grid2">${t.fanModes.map((f) => html`<button type="button" class="btn ${f === st.fanMode ? 'sel' : ''}" @click=${() => this.send({ kind: 'thermostat', id: t.id, action: 'fan', value: f })}>${f}</button>`)}</div>` : nothing}
            <h2>Mode</h2>
            <div class="grid2">${t.modes.map((m) => html`<button type="button" class="btn ${m === st.mode ? 'sel' : ''}" @click=${() => this.send({ kind: 'thermostat', id: t.id, action: 'mode', value: m })}>${m}</button>`)}</div>
          </div>
          <div class="dial">
            <svg width="320" height="320" viewBox="0 0 320 320" role="img" aria-label="Target ${st.setpoint} degrees, room ${st.current} degrees">
              <defs><linearGradient id="ring" x1="0" y1="0" x2="1" y2="0"><stop offset="0" stop-color="#3BA7F0"></stop><stop offset="0.5" stop-color="#7B4DFF"></stop><stop offset="1" stop-color="#F0473C"></stop></linearGradient></defs>
              <circle cx="160" cy="160" r="140" fill="none" stroke="rgba(255,255,255,0.22)" stroke-width="16" stroke-linecap="round" stroke-dasharray="${arc} ${circ}" transform="rotate(135 160 160)"></circle>
              <circle cx="160" cy="160" r="140" fill="none" stroke="url(#ring)" stroke-width="16" stroke-linecap="round" stroke-dasharray="${fill} ${circ}" transform="rotate(135 160 160)"></circle>
            </svg>
            <div class="mid">
              <span class="sp">${st.setpoint}°</span>
              <span style="font-size:15px;letter-spacing:1px;text-align:center;line-height:1.2">TARGET<br />TEMPERATURE</span>
              <span style="width:160px;height:1px;background:rgba(255,255,255,0.5);margin:6px 0"></span>
              <span style="font-size:14px"><strong>CURRENT TEMP.</strong> ${st.current}°</span>
            </div>
          </div>
          <div class="side">
            ${iconButton('power', st.power ? 'Climate is on, tap to turn off' : 'Climate is off, tap to turn on', () => this.send({ kind: 'thermostat', id: t.id, action: 'power', value: !st.power }), `btn square ${st.power ? 'on' : ''}`)}
            <button type="button" class="btn square" aria-label="Raise setpoint" @click=${() => this.send({ kind: 'thermostat', id: t.id, action: 'setpoint', value: st.setpoint + 1 })}>+</button>
            <button type="button" class="btn square" aria-label="Lower setpoint" @click=${() => this.send({ kind: 'thermostat', id: t.id, action: 'setpoint', value: st.setpoint - 1 })}>−</button>
          </div>
        </section>
      </div>`;
  }
  private canControl(t: DirectorThermostat): boolean {
    return t.connected === true && this.store.connection === 'online' && this.store.config.climateResult !== 'pending';
  }

  private modes(t: DirectorThermostat, view: 'hvac' | 'fan') {
    const fan = view === 'fan';
    const title = fan ? 'Fan Mode' : 'System Mode';
    const selected = (fan ? t.FAN_MODE : t.HVAC_MODE)?.toLowerCase();
    const modes = ((fan ? t.FAN_MODES_LIST : t.HVAC_MODES_LIST) ?? '').split(',').map((mode) => mode.trim()).filter(Boolean);
    return html`<section class="mode-screen" aria-label=${title}>
      <header class="mode-heading">
        <button class="btn" aria-label="Back to thermostat" @click=${() => (this.modeView = null)}>${icon('chevronLeft', 20)}</button>
        <h2>${title}</h2>
        <button class="btn" aria-label="Thermostat home" @click=${() => (this.modeView = null)}>${icon('home', 20)}</button>
      </header>
      ${modes.map((mode) => {
        const appearance = thermostatMode(mode, fan);
        const active = mode.toLowerCase() === selected;
        return html`<button type="button" class="mode-row" ?disabled=${!this.canControl(t)}
          aria-pressed=${active} @click=${() => this.store.sendClimate(t.id, fan ? 'fan' : 'mode', mode)}>
          <span class="mode-glyph ${appearance.tone}">${icon(appearance.glyph, 22)}</span>
          <span class="mode-label">${mode}${appearance.description ? html`<small>${appearance.description}</small>` : nothing}</span>
          ${active ? html`<strong>${icon('check', 19)}</strong>` : nothing}
        </button>`;
      })}
    </section>`;
  }

  private marker(t: DirectorThermostat, kind: 'heat' | 'cool', value: number, top: number, span: number, enabled: boolean) {
    const send = (next: number) => {
      const setpoint = Math.max(45, Math.min(95, next));
      if (setpoint !== value && this.canControl(t)) this.store.sendClimate(t.id, kind + '_set', String(setpoint));
    };
    const dragging = this.drag?.id === t.id && this.drag.kind === kind;
    const read = dragging ? this.drag!.value : value;
    return html`<button type="button" role="slider" class="temperature-marker ${kind} ${dragging ? 'dragging' : ''}" style="top:${top}%"
      aria-label="${kind === 'heat' ? 'Heat' : 'Cool'} setpoint" aria-orientation="vertical"
      aria-valuemin="45" aria-valuemax="95" aria-valuenow=${read} ?disabled=${!enabled}
      @pointerdown=${(event: PointerEvent) => {
        if (!enabled || this.drag || event.button !== 0) return;
        const button = event.currentTarget as HTMLButtonElement;
        const rail = button.parentElement!.getBoundingClientRect();
        if (!rail.height) return;
        button.setPointerCapture(event.pointerId);
        this.drag = { id: t.id, kind, pointer: event.pointerId, start: value, y: event.clientY,
          pixels: rail.height * .84 / span, value };
      }}
      @pointermove=${(event: PointerEvent) => {
        const drag = this.drag;
        if (!drag || drag.pointer !== event.pointerId) return;
        this.drag = { ...drag, value: Math.max(45, Math.min(95, Math.round(drag.start + (drag.y - event.clientY) / drag.pixels))) };
      }}
      @pointerup=${(event: PointerEvent) => {
        const drag = this.drag;
        if (!drag || drag.pointer !== event.pointerId) return;
        this.drag = undefined;
        send(drag.value);
      }}
      @pointercancel=${() => (this.drag = undefined)}
      @lostpointercapture=${() => (this.drag = undefined)}
      @keydown=${(event: KeyboardEvent) => {
        const step = event.key === 'ArrowUp' || event.key === 'ArrowRight' ? 1
          : event.key === 'ArrowDown' || event.key === 'ArrowLeft' ? -1 : 0;
        if (!step) return;
        event.preventDefault();
        send(value + step);
      }}>${read}°</button>`;
  }

  private device(t: DirectorThermostat) {
    const scale = t.SCALE?.toLowerCase();
    const unit = scale === 'fahrenheit' ? 'F' : scale === 'celsius' ? 'C' : undefined;
    const online = t.connected !== false;
    const current = online && unit ? t.DISPLAY_TEMPERATURE : undefined;
    const mode = t.HVAC_MODE?.trim().toLowerCase().replace(/[ _-]/g, '');
    const showHeat = mode === 'heat' || mode === 'auxheat' || mode === 'auto' || mode === 'heatcool';
    const showCool = mode === 'cool' || mode === 'auto' || mode === 'heatcool';
    const heat = online && unit && showHeat ? t.DISPLAY_HEATSETPOINT : undefined;
    const cool = online && unit && showCool ? t.DISPLAY_COOLSETPOINT : undefined;
    const humidity = online ? t.HUMIDITY : undefined;
    const temperatures = [current, heat, cool].filter((v): v is number => typeof v === 'number' && Number.isFinite(v));
    const min = Math.min(unit === 'C' ? 12 : 45, ...temperatures);
    const max = Math.max(unit === 'C' ? 32 : 95, ...temperatures);
    const position = (temperature: number) => 92 - 84 * (temperature - min) / (max - min);
    const previewHeat = this.drag?.id === t.id && this.drag.kind === 'heat' ? this.drag.value : heat;
    const previewCool = this.drag?.id === t.id && this.drag.kind === 'cool' ? this.drag.value : cool;
    const heatPosition = typeof previewHeat === 'number' ? position(previewHeat) : undefined;
    const coolPosition = typeof previewCool === 'number' ? position(previewCool) : undefined;
    const closeMarkers = heatPosition !== undefined && coolPosition !== undefined && Math.abs(heatPosition - coolPosition) < 14;
    const markerMiddle = closeMarkers ? (heatPosition! + coolPosition!) / 2 : 0;
    const enabled = unit === 'F' && this.canControl(t);
    const result = this.store.config.climateResult;
    const label = (value?: number) => typeof value === 'number' && unit ? value + '°' + unit : '—';
    return html`<section class="thermostat-device" aria-label="${t.name} thermostat">
      <div class="thermostat-screen">
        ${this.modeView ? this.modes(t, this.modeView) : html`<header class="device-heading"><h2>${t.name}</h2><span>${online ? 'Room temperature' : 'Thermostat offline'}</span></header>
        <div class="device-shortcuts">
          <button class="device-indicator" aria-label="System mode" @click=${() => (this.modeView = 'hvac')}><span class="mode-glyph ${thermostatMode(t.HVAC_MODE).tone}">${icon(thermostatMode(t.HVAC_MODE).glyph, 23)}</span><small>${t.HVAC_MODE || 'Mode'}</small></button>
          <button class="device-indicator" aria-label="Fan mode" @click=${() => (this.modeView = 'fan')}><span class="mode-glyph ${thermostatMode(t.FAN_MODE, true).tone}">${icon(thermostatMode(t.FAN_MODE, true).glyph, 23)}</span><small>Fan</small></button>
        </div>
        <div class="device-reading">
          <div class="device-temperature" aria-label="${label(current)}">${current ?? '—'}${current !== undefined ? html`<small>°${unit}</small>` : nothing}</div>
          <p class="device-status">${online ? t.HVAC_STATE || 'HVAC status unavailable' : 'Readings unavailable'}</p>
          ${typeof humidity === 'number' ? html`<p class="device-humidity">${icon('comfort', 16)} ${humidity}% humidity</p>` : nothing}
        </div>
        ${temperatures.length ? html`<div class="temperature-rail" role="group" aria-label="Temperature setpoints">
          <span class="temperature-track"></span>
          ${Array.from({ length: 11 }, (_, i) => html`<i class="temperature-tick" style="top:${i * 10}%"></i>`)}
          ${heatPosition === undefined || heat === undefined ? nothing : this.marker(t, 'heat', heat, !this.drag && closeMarkers ? markerMiddle + 7 : heatPosition, max - min, enabled)}
          ${coolPosition === undefined || cool === undefined ? nothing : this.marker(t, 'cool', cool, !this.drag && closeMarkers ? markerMiddle - 7 : coolPosition, max - min, enabled)}
          ${typeof current === 'number' ? html`<span class="temperature-current" style="top:${position(current)}%"></span>` : nothing}
        </div>` : nothing}
        <div class="device-setpoints">
          ${showHeat ? html`<span class="heat">Heat ${label(previewHeat)}</span>` : nothing}
          ${showCool ? html`<span class="cool">Cool ${label(previewCool)}</span>` : nothing}
        </div>`}
        ${result ? html`<p class="climate-result" role="status">${result === 'pending' ? 'Sending…' : result === 'sent' ? 'Command accepted by Director' : 'Command could not be confirmed'}</p>` : nothing}
      </div>
    </section>`;
  }
}

customElements.define('z-climate', ClimatePage);
