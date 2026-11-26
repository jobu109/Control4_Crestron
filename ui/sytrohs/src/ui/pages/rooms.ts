import { repeat } from 'lit/directives/repeat.js';
import { css, html, nothing } from 'lit';
import type { Room } from '../../model/types';
import { ZPage, iconButton, shared } from '../base';
import { icon, roomIcon } from '../icons';

/** Measured activity for a room row, including devices reported by the house catalog. */
export function roomStatus(page: ZPage, room: Room): { text: string; active: boolean } {
  const store = page.store;
  const parts: string[] = [];
  const media = store.get('media', room.id);
  const mediaPower = media?.power ?? room.mediaPower;
  const lit = store.lightsOn(room);
  const shades = room.shades.map((shade) => store.get('shade', shade.id)?.position);
  const open = shades.filter((position) => position !== undefined && position > 0).length;
  const runningFans = (room.fans ?? []).filter((fan) => fan.speed !== undefined && fan.speed > 0);
  if (mediaPower) parts.push(media?.title ?? 'Media on');
  if (lit) parts.push(`${lit} ${lit === 1 ? 'light' : 'lights'} on`);
  if (runningFans.length === 1) parts.push(`${runningFans[0].name} ${FAN_SPEEDS[runningFans[0].speed!]}`);
  else if (runningFans.length) parts.push(`${runningFans.length} fans on`);
  if (open) parts.push(`${open} ${open === 1 ? 'shade' : 'shades'} open`);
  const active = Boolean(mediaPower || lit || runningFans.length || open);
  const t = room.thermostatId ? store.get('thermostat', room.thermostatId) : undefined;
  if (t?.current !== undefined) parts.push(`${Math.round(t.current)}°`);
  if (!parts.length) {
    if (mediaPower === false) parts.push('Media off');
    if (room.lights.length && room.lights.every((light) => store.known('light', light.id))) parts.push('Lights off');
    if (room.fans?.length && room.fans.every((fan) => fan.speed === 0)) parts.push('Fans off');
    if (shades.length && shades.every((position) => position === 0)) parts.push('Shades closed');
  }
  return { text: parts.join(' · '), active };
}

export class RoomsPage extends ZPage {
  static styles = [shared, css`
    .content { display: grid; gap: 16px; }
    section { padding: 18px 22px; display: flex; flex-direction: column; overflow: hidden; }
    .head { height: 40px; }
    .floor { font-size: 15px; font-weight: 700; letter-spacing: 1.5px; text-transform: uppercase; }
    .list { overflow-y: auto; }
    a.row { height: 76px; }
  `];

  render() {
    const floors = this.store.sortedFloors();
    return html`
      <div class="toolbar"><span class="hint">Tap a room to open its controls</span></div>
      <div class="content" style="grid-template-columns: repeat(${Math.max(1, Math.min(floors.length, 4))}, minmax(0, 1fr))">
        ${floors.map((f) => {
          const rooms = this.store.roomsOnFloor(f.id);
          return html`<section class="panel" aria-label=${f.name}>
            <div class="head"><span class="floor">${f.name}</span><span class="sub">${rooms.length} rooms</span></div>
            <div class="list">
              ${rooms.map((r) => {
                const { text, active } = roomStatus(this, r);
                return html`<a class="row" href="#/room/${encodeURIComponent(r.id)}">
                  <span class="tile ${active ? 'on' : ''}">${icon(roomIcon(r.name, r.icon), 24, 1.5)}</span>
                  <span class="grow"><span class="name" style="display:block">${r.name}</span><span class="sub">${text}</span></span>
                  ${icon('chevronRight', 18, 1.8)}
                </a>`;
              })}
            </div>
          </section>`;
        })}
      </div>
    `;
  }
}

const FAN_SPEEDS = ['Off', 'Low', 'Medium', 'Med-High', 'High'] as const;

type Sheet = 'fans' | 'lighting' | 'shades' | 'relays' | 'buttons' | 'climate' | null;

export class RoomPage extends ZPage {
  static properties = { ...ZPage.properties, sheet: { state: true }, scenesOpen: { state: true } };
  sheet: Sheet = null;
  private scenesOpen = false;
  private roomRoute = "";
  private readonly shadeTargets = new Map<string, { position: number; timer: number }>();

  static styles = [shared, css`
    .side { position: absolute; left: 0; top: 0; bottom: 0; width: 270px; background: rgba(6,8,11,0.62); }
    .menu { position: absolute; left: 40px; top: 280px; display: flex; flex-direction: column; gap: 22px; }
    .menu button { height: 56px; padding: 0; border: none; background: transparent; display: flex; align-items: center; gap: 18px; font-size: 16px; font-weight: 700; letter-spacing: 1px; }
    .menu .tile { width: 56px; height: 56px; }
    .swipe { position: absolute; left: 310px; right: 40px; top: 108px; height: 64px; display: flex; justify-content: space-between; align-items: center; }
    .swipe button { height: 64px; padding: 0 12px; border: none; background: transparent; display: flex; align-items: center; gap: 10px; font-size: 13px; font-weight: 700; letter-spacing: 1.5px; }
    .swipe span { font-size: 22px; font-weight: 700; letter-spacing: 3px; color: var(--soft); }
    /* Anchored to the bottom so a two-line room name grows upward instead of over the buttons. */
    .hero { position: absolute; left: 330px; right: 60px; bottom: 336px; display: flex; justify-content: space-between; align-items: flex-end; gap: 32px; }
    .hero h1 { flex: 1; min-width: 0; font-size: 104px; font-weight: 300; line-height: 1.15; letter-spacing: -1px;
      display: -webkit-box; -webkit-box-orient: vertical; -webkit-line-clamp: 2; overflow: hidden; overflow-wrap: anywhere; }
    .hero h1.long { font-size: 80px; }
    .hero .st { flex-shrink: 0; max-width: 340px; display: flex; flex-direction: column; align-items: flex-end; gap: 6px; font-size: 24px; font-weight: 300; text-align: right; }
    .quick { position: absolute; left: 330px; right: 60px; top: 500px; display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 20px; }
    .quick .btn { height: 68px; font-size: 18px; letter-spacing: 1.5px; }
    .quick .rocker { grid-column: span 2; }
    .quick .rocker .btn { height: 58px; }
    .sheet { position: absolute; left: 290px; right: 40px; top: 100px; bottom: 64px; border-radius: 16px; background: rgba(6,8,11,0.96); border: 1px solid rgba(255,255,255,0.12); padding: 26px 40px 110px; overflow: hidden; }
    .sheet-heading { display: flex; min-height: 38px; align-items: center; justify-content: space-between; gap: 16px; }
    .sheet .scroll { height: calc(100% - 38px); overflow-y: auto; }
    .room-scenes-tab { display: inline-flex; align-items: center; gap: 8px; height: 36px; padding: 0 14px;
      white-space: nowrap; }
    .room-scenes { position: absolute; z-index: 1; right: 0; top: 0; bottom: 0; width: 255px;
      display: flex; flex-direction: column; gap: 18px; padding: 20px;
      background: #20242a; border-left: 1px solid rgba(255,255,255,0.16);
      box-shadow: -8px 0 25px rgba(0,0,0,0.45); }
    .room-scenes-header { display: flex; align-items: center; justify-content: space-between; }
    .room-scenes-list { overflow-y: auto; display: grid; grid-auto-rows: minmax(64px, auto);
      align-content: start; gap: 4px; padding: 7px; border: 1px solid #090909; border-radius: 3px;
      background: linear-gradient(90deg,#111,#181818 12%,#181818 88%,#101010);
      box-shadow: inset 2px 0 5px #ffffff0a,0 5px 14px #0009; }
    .room-scene { display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 4px;
      padding: 7px; border: 1px solid #101010; border-radius: 2px; background: linear-gradient(145deg,#303030,#282828 58%,#1d1d1d);
      color: #bfc2c3; box-shadow: inset 0 1px #ffffff12,0 2px 4px #0009; cursor: pointer; touch-action: manipulation;
      font: 650 16px/1.05 "Segoe UI", sans-serif; text-align: center; overflow-wrap: anywhere;
      transition: filter .12s, transform .12s, background .18s, box-shadow .18s; }
    .room-scene:enabled:active { filter: brightness(1.2); transform: scale(.975); color: #fff;
      border-color: #04aa8e; background: linear-gradient(145deg,#40534f,#314b46 60%,#1d2c29);
      box-shadow: inset 0 1px #ffffff1f,0 0 10px #04aa8e; }
    .room-scene:disabled { opacity: .45; cursor: default; }
    .room-scene-icon { display: grid; place-items: center; width: 34px; height: 34px; color: #525252;
      filter: drop-shadow(0 1px 1px #000); pointer-events: none; }
    .room-scene:enabled:active .room-scene-icon { color: #fff; }
    .room-scene-dots { display: grid; grid-template-columns: repeat(3,1fr); gap: 14%; width: 72%; height: 72%; }
    .room-scene-dots i { border-radius: 50%; background: currentColor; box-shadow: inset 0 1px #ffffff1f; }

    .load { padding: 16px 0; border-bottom: 1px solid rgba(255,255,255,0.12); display: flex; flex-direction: column; gap: 12px; }
    .load .top { display: flex; justify-content: space-between; align-items: center; font-size: 20px; }
    .sheet .load-grid { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); grid-auto-rows: 306px;
      align-content: start; gap: 18px; overflow-x: hidden; overflow-y: auto; padding: 12px 8px 16px; }
    .load-control { min-width: 0; height: 306px; display: grid; grid-template-rows: 52px 1fr 52px;
      padding: 8px; border: 1px solid #4b4c50; border-radius: 10px; background: linear-gradient(90deg,#111316,#35373a 12%,#202225 34%,#111316);
      box-shadow: inset 0 0 3px #777, 0 4px 9px #000; }
    .load-control .endpoint { display: flex; align-items: center; justify-content: center; gap: 8px; border: 1px solid #494c51;
      border-radius: 5px; background: #22252a; color: #fff; font-size: 13px; font-weight: 700; letter-spacing: 1px; }
    .load-control .endpoint.selected { border-color: var(--green); }
    .load-control button:enabled:active { background: #b7ddff; color: #0b0d10; }
    .load-control button:disabled { opacity: 0.45; cursor: default; }
    .load-control .face { position: relative; display: flex; flex-direction: column; align-items: center; justify-content: center;
      gap: 8px; padding: 6px; margin: 4px 0; border-radius: 4px; background: #15171a; box-shadow: inset 0 0 7px #000; text-align: center; }
    .load-control .face.metered { padding-right: 43px; }
    .load-control .load-name { font-size: 15px; line-height: 1.2; font-weight: 700; overflow-wrap: anywhere; display: -webkit-box;
      -webkit-box-orient: vertical; -webkit-line-clamp: 3; overflow: hidden; }
    .load-control .reading { color: var(--soft); font-size: 12px; font-weight: 600; font-variant-numeric: tabular-nums; }
    .load-control input.level { position: absolute; right: -47px; top: calc(50% - 22px); width: 136px; height: 44px; margin: 0;
      transform: rotate(-90deg); touch-action: none; }
    .load-control input.level::-webkit-slider-runnable-track { height: 8px; border-radius: 5px;
      background: linear-gradient(to right, #80cbff var(--pct, 0%), #33383e var(--pct, 0%)); }
    .load-control input.level::-webkit-slider-thumb { width: 6px; height: 20px; margin-top: -6px; border-radius: 3px;
      box-shadow: 0 0 6px #80cbff; }
    .load-control input.level::-moz-range-track { height: 8px; border-radius: 5px; background: #33383e; }
    .load-control input.level::-moz-range-progress { height: 8px; border-radius: 5px; background: #80cbff; }
    .load-control input.level::-moz-range-thumb { width: 6px; height: 20px; border-radius: 3px; }
    .wall-control { display: block; padding: 6px; border: 2px solid #0c0c0d; border-radius: 7px;
      background: linear-gradient(90deg,#151515,#393939 12%,#171717 88%,#080808);
      box-shadow: inset 0 0 4px #777, 0 4px 10px #000; }
    .wall-control .face { height: 100%; margin: 0; padding: 52px 16px; border: 1px solid #383838; border-radius: 3px;
      background: linear-gradient(100deg,#1a1a1a,#282828 32%,#242424 78%,#101010);
      box-shadow: inset 2px 0 5px #ffffff17,inset -4px 0 7px #050505; }
    .wall-control.on .face { border-color: #6896b2; box-shadow: inset -4px 0 7px #050505, 0 0 7px #80cbff40; }
    .wall-control .face.metered { padding-right: 38px; }
    .wall-control .load-name { font-size: 17px; text-shadow: 0 2px 4px #000; }
    .wall-control .reading { color: #80cbff; font-size: 16px; text-shadow: 0 2px 4px #000; }
    .wall-control .endpoint { position: absolute; left: 8px; right: 39px; height: 44px; padding: 0 6px;
      justify-content: flex-start; border-color: transparent; background: transparent; color: #dce7ef; }
    .wall-control .turn-on { top: 6px; }
    .wall-control .turn-off { bottom: 6px; }
    .wall-control .turn-off .dim-sun { display: inline-flex; opacity: 0.5; }
    .wall-control input.level { right: -81px; width: 204px; }
    .light-dimmer .face { padding-inline: 39px; }
    .light-caption { width: 100%; display: flex; flex-direction: column; align-items: center; gap: 8px; text-align: center; }
    .light-caption .load-name, .light-caption .reading { width: 100%; }
    .light-dimmer .endpoint { left: 24px; right: 24px; display: grid;
      grid-template-columns: minmax(0, 1fr) auto minmax(0, 1fr); }
    .light-dimmer .endpoint > :first-child { justify-self: end; }
    .light-dimmer .endpoint > :last-child { grid-column: 2; white-space: nowrap; }
    .wall-control input.level::-webkit-slider-runnable-track { height: 9px; box-shadow: inset 1px 0 3px #000; }
    .roller-shade .face { padding: 0; }
    .roller-shade .endpoint { left: 8%; right: 25%; height: 12%; padding: 0; justify-content: center;
      border-color: #52585d; background: #14171a; }
    .roller-shade .endpoint.selected { border-color: #85b6d3; }
    .roller-shade .open-shade { top: 4%; }
    .roller-shade .stop-shade { bottom: 19%; }
    .roller-shade .close-shade { bottom: 4%; }
    .roller-shade .stop-icon { width: 12px; height: 12px; border: 1.5px solid currentColor; border-radius: 2px; }
    .roller-shade .shade-window { position: absolute; top: 20%; left: 20%; right: 36%; height: 23%; overflow: hidden;
      border: 1px solid #becbd3; border-radius: 3px; background: linear-gradient(120deg,#324b59,#17252d); box-shadow: inset 0 0 5px #000; }
    .roller-shade .fabric { position: absolute; inset: 0 0 auto; background: repeating-linear-gradient(0deg,#777d80 0 2px,#a9adae 3px 4px);
      box-shadow: 0 1px 1px #000; transition: height .15s linear; }
    .roller-shade .roller { position: absolute; inset: 0 0 auto; height: 5px; background: #becbd3; }
    .roller-shade .shade-caption { position: absolute; top: 46%; left: 7%; right: 25%; height: 20%;
      display: flex; flex-direction: column; justify-content: center; gap: 4px; }
    .roller-shade .reading { font-size: 14px; }
    .roller-shade .shade-motion { position: absolute; inset: 0; background:
      repeating-linear-gradient(180deg, transparent 0 11px, #9bdcff66 13px 16px, transparent 18px 24px);
      background-size: 100% 24px; animation: shade-motion .75s linear infinite; }
    .roller-shade .shade-motion.opening { animation-direction: reverse; }
    .roller-shade.moving .shade-window { border-color: #9bdcff; }
    @keyframes shade-motion { to { background-position-y: 24px; } }
    @media (prefers-reduced-motion: reduce) { .roller-shade .shade-motion { animation: none; } }
    .fan-control .face { justify-content: flex-start; gap: 6px; padding: 10px; }
    .fan-control .fan-icon { display: flex; align-items: center; flex: 0 0 34px; }
    .fan-control .load-name { width: 100%; -webkit-line-clamp: 2; }
    .fan-control .reading { font-size: 14px; }
    .fan-speeds { display: grid; grid-template-rows: repeat(5, minmax(0, 1fr)); gap: 5px; flex: 1; width: 100%; }
    .fan-speed { position: relative; min-height: 0; border: 1px solid #52585d; border-radius: 5px;
      background: #14171a; font-size: 13px; font-weight: 650; touch-action: manipulation; }
    .fan-speed.selected { border-color: #80cbff; background: #253b4a; box-shadow: inset 0 0 7px #80cbff26; }
    .fan-speed .indicator { position: absolute; left: 8px; top: calc(50% - 3px); width: 6px; height: 6px;
      border-radius: 50%; background: #58636b; }
    .fan-speed.selected .indicator { background: #80cbff; box-shadow: 0 0 7px #80cbff; }
    .close { position: absolute; left: 50%; bottom: 20px; margin-left: -38px; }
    .empty { padding-top: 40px; font-size: 18px; color: var(--soft); }
  `];

  private get room(): Room | undefined {
    return this.store.room(decodeURIComponent(this.params[0] ?? '')) ?? this.store.config.rooms[0];
  }

  willUpdate(): void {
    const route = this.params.join('/');
    if (route !== this.roomRoute) {
      this.roomRoute = route;
      this.scenesOpen = false;
      this.sheet = this.params[1] === 'lighting' ? 'lighting' : null;
    }
    const room = this.room;
    if (room) this.store.focusRoom(room.id);
    for (const [id, target] of this.shadeTargets) {
      const shade = this.store.get('shade', id);
      if (this.store.connection !== 'online' || (this.store.known('shade', id) && shade?.position === target.position && !shade.moving))
        this.clearShadeTarget(id);
    }
  }

  private swipeStart: { x: number; y: number; id: number } | null = null;

  private onPointerDown = (e: PointerEvent): void => {
    // Sliders, buttons and open sheets keep their own gestures.
    const target = e.composedPath()[0] as Element | undefined;
    if (this.sheet || target?.closest?.('input, button, a, .sheet')) { this.swipeStart = null; return; }
    this.swipeStart = { x: e.clientX, y: e.clientY, id: e.pointerId };
    // Keep receiving the pointer when the finger leaves the page mid-swipe.
    try { this.setPointerCapture(e.pointerId); } catch { /* pointer already released */ }
  };

  private onPointerUp = (e: PointerEvent): void => {
    const start = this.swipeStart;
    this.swipeStart = null;
    if (!start || start.id !== e.pointerId || this.sheet) return;
    const scale = this.getBoundingClientRect().width / 1280 || 1;
    const dx = (e.clientX - start.x) / scale;
    const dy = (e.clientY - start.y) / scale;
    if (Math.abs(dx) >= 80 && Math.abs(dx) > Math.abs(dy) * 1.5) this.step(dx < 0 ? 1 : -1);
  };

  private onPointerCancel = (): void => { this.swipeStart = null; };

  connectedCallback(): void {
    super.connectedCallback();
    this.addEventListener('pointerdown', this.onPointerDown);
    this.addEventListener('pointerup', this.onPointerUp);
    this.addEventListener('pointercancel', this.onPointerCancel);
    this.style.touchAction = 'pan-y';
  }

  disconnectedCallback(): void {
    for (const id of this.shadeTargets.keys()) this.clearShadeTarget(id);
    this.removeEventListener('pointerdown', this.onPointerDown);
    this.removeEventListener('pointerup', this.onPointerUp);
    this.removeEventListener('pointercancel', this.onPointerCancel);
    super.disconnectedCallback();
  }

  private step(delta: number): void {
    const room = this.room;
    if (!room) return;
    const peers = this.store.roomsOnFloor(room.floorId);
    const next = peers[(peers.indexOf(room) + delta + peers.length) % peers.length];
    this.sheet = null;
    location.hash = `#/room/${encodeURIComponent(next.id)}`;
  }

  render() {
    const room = this.room;
    if (!room) return html`<div class="toolbar"><span class="hint">No rooms reported</span></div>`;
    const lit = this.store.lightsOn(room);
    const lightsKnown = room.lights.every((l) => this.store.known('light', l.id));
    // A shade counts as open when its reported position is above 0 (fully closed).
    const open = room.shades.filter((sh) => (this.store.get('shade', sh.id)?.position ?? 0) > 0).length;
    const shadesKnown = room.shades.every((sh) => this.store.known('shade', sh.id));
    const lightActions = room.lightActions ?? ['ambient', 'on', 'off'];
    const t = room.thermostatId ? this.store.get('thermostat', room.thermostatId) : undefined;
    const media = this.store.get('media', room.id);
    const loading = room.lazy && !room.lights.length && !room.shades.length;
    const menu: Array<[Exclude<Sheet, null>, string, string, boolean]> = [
      ['lighting', 'LIGHTING', 'light', room.lights.length > 0],
      ['fans', 'FANS', 'fan', Boolean(room.fans?.length)],
      ['shades', 'SHADES', 'shades', room.shades.length > 0],
      ['buttons', 'BUTTONS', 'power', Boolean(room.experienceButtons?.length)],
      ['relays', 'RELAYS', 'power', Boolean(room.relays?.length)],
      ['climate', 'CLIMATE', 'climate', !!room.thermostatId]
    ];
    return html`
      <div class="side"></div>
      <nav class="menu" aria-label="Room controls">
        ${room.videoSources.length || room.audioSources.length ? html`<button type="button" @click=${() => (location.hash = `#/media/${encodeURIComponent(room.id)}`)}><span class="tile">${icon('media', 28)}</span>MEDIA</button>` : nothing}
        ${menu.filter((m) => m[3]).map(([id, label, ic]) => html`<button type="button" @click=${() => (this.sheet = id)}>
          <span class="tile ${this.sheet === id ? 'on' : ''}">${icon(ic, 28)}</span>${label}</button>`)}
      </nav>
      <div class="swipe">
        <button type="button" @click=${() => this.step(-1)}>${icon('doubleLeft', 34, 1.4)}BACK<br />ROOM</button>
        <span>SWIPE</span>
        <button type="button" @click=${() => this.step(1)}>NEXT<br />ROOM${icon('doubleRight', 34, 1.4)}</button>
      </div>
      <div class="hero">
        <h1 class=${room.name.length > 12 ? 'long' : ''}>${room.name}</h1>
        <div class="st">
          ${room.videoSources.length || room.audioSources.length || media?.power !== undefined
            ? html`<span>Media is <em>${media?.power === undefined ? 'unknown' : media.power ? media.title ?? 'On' : 'Off'}</em></span>`
            : nothing}
          ${room.lights.length
            ? lit
              ? html`<span><em>${lit}</em> ${lit === 1 ? 'light is' : 'lights are'} on</span>`
              : html`<span>Lights are <em>${lightsKnown ? 'Off' : 'unknown'}</em></span>`
            : nothing}
          ${room.fans?.map((fan) => html`<span>${fan.name} is <em>${fan.speed === undefined ? 'unknown' : FAN_SPEEDS[fan.speed]}</em></span>`)}
          ${room.shades.length
            ? open
              ? html`<span><em>${open}</em> ${open === 1 ? 'shade is' : 'shades are'} open</span>`
              : html`<span>Shades are <em>${shadesKnown ? 'Closed' : 'unknown'}</em></span>`
            : nothing}
          ${t ? html`<span>Climate is <em>${t.power ? `${t.mode} ${t.setpoint}°` : 'Off'}</em></span>` : nothing}
          ${loading && this.store.connection === 'online' ? html`<span>Loading room…</span>` : nothing}
        </div>
      </div>
      ${room.lights.length ? html`<div class="quick" role="group" aria-label="Lighting scene" style="grid-template-columns: repeat(${lightActions.length}, minmax(0, 1fr))">
        ${lightActions.includes('ambient') ? html`<button type="button" class="btn outline" @click=${() => this.send({ kind: 'room-lights', id: room.id, action: 'ambient' })}>AMBIENT</button>` : nothing}
        ${lightActions.includes('on') && lightActions.includes('off') ? html`
          <div class="rocker" role="group" aria-label="Room lights on and off">
            <button type="button" class="btn ${lightsKnown && lit === room.lights.length ? 'sel' : ''}"
              @click=${() => this.send({ kind: 'room-lights', id: room.id, action: 'on' })}>ON</button>
            <button type="button" class="btn ${lightsKnown && !lit ? 'sel' : ''}"
              @click=${() => this.send({ kind: 'room-lights', id: room.id, action: 'off' })}>OFF</button>
          </div>` : lightActions.includes('on') ? html`<button type="button" class="btn outline"
            @click=${() => this.send({ kind: 'room-lights', id: room.id, action: 'on' })}>ON</button>` : lightActions.includes('off') ? html`
          <button type="button" class="btn ${lit || !lightsKnown ? 'outline' : 'sel'}"
            @click=${() => this.send({ kind: 'room-lights', id: room.id, action: 'off' })}>OFF</button>` : nothing}
      </div>` : nothing}
      ${this.sheet ? this.renderSheet(room) : nothing}
      <z-chrome .store=${this.store} heading=${this.store.floorName(room.floorId)} subheading=${room.name} back="rooms" current="rooms"></z-chrome>
    `;
  }

  private clearShadeTarget(id: string): void {
    window.clearTimeout(this.shadeTargets.get(id)?.timer);
    this.shadeTargets.delete(id);
  }

  private moveShade(id: string, action: 'open' | 'close' | 'stop' | 'position', value?: number): void {
    if (this.store.connection !== 'online') return;
    this.clearShadeTarget(id);
    if (action !== 'stop') {
      const position = action === 'open' ? 100 : action === 'close' ? 0 : value!;
      const measured = this.store.known('shade', id) ? this.store.get('shade', id) : undefined;
      if (measured?.position !== position || measured.moving) {
        const timer = window.setTimeout(() => {
          this.clearShadeTarget(id);
          this.requestUpdate();
        }, 120_000);
        this.shadeTargets.set(id, { position, timer });
      }
    }
    if (action === 'position' && value !== undefined) this.send({ kind: 'shade', id, action, value });
    else if (action !== 'position') this.send({ kind: 'shade', id, action });
    this.requestUpdate();
  }

  private loadLevel(value: number, label: string, disabled: boolean, commit: (value: number) => void) {
    return html`<input class="level" type="range" min="0" max="100" .value=${String(value)} ?disabled=${disabled}
      aria-label=${label} style=${'--pct:' + value + '%'}
      @input=${(event: Event) => {
        const input = event.currentTarget as HTMLInputElement;
        input.style.setProperty('--pct', input.value + '%');
      }}
      @change=${(event: Event) => commit(Number((event.currentTarget as HTMLInputElement).value))}>`;
  }

  private renderSheet(room: Room) {
    let body;
    const disabled = this.store.connection !== 'online';
    if (this.sheet === 'lighting') {
      body = repeat(room.lights, (light) => light.id, (light) => {
        const measured = this.store.known('light', light.id) ? this.store.get('light', light.id) : undefined;
        const level = measured?.level ?? 0;
        const dimmable = measured?.dimmable ?? light.dimmable;
        return html`<div class="load-control wall-control light-dimmer ${measured?.on ? 'on' : ''}" role="group" aria-label=${light.name}>
          <div class="face ${dimmable ? 'metered' : ''}">
            <button type="button" class="endpoint turn-on" aria-label=${light.name + ' on'} ?disabled=${disabled}
              @click=${() => this.send({ kind: 'light', id: light.id, action: 'on' })}>${icon('sun', 24)} <span>ON</span></button>
            <div class="light-caption">
              <span class="load-name">${light.name}</span>
              <span class="reading">${!measured ? 'Awaiting feedback' : dimmable ? level + '%' : measured.on ? 'On' : 'Off'}</span>
            </div>
            ${dimmable ? this.loadLevel(level, light.name + ' level', disabled,
              (value) => this.send({ kind: 'light', id: light.id, action: 'level', value })) : nothing}
            <button type="button" class="endpoint turn-off" aria-label=${light.name + ' off'} ?disabled=${disabled}
              @click=${() => this.send({ kind: 'light', id: light.id, action: 'off' })}><span class="dim-sun">${icon('sunDim', 24)}</span> <span>Off</span></button>
          </div>
        </div>`;
      });
    } else if (this.sheet === 'shades') {
      body = repeat(room.shades, (shade) => shade.id, (shade) => {
        const measured = this.store.known('shade', shade.id) ? this.store.get('shade', shade.id) : undefined;
        const position = measured?.position;
        const target = this.shadeTargets.get(shade.id);
        const moving = measured?.moving || (target && (position === undefined ? 'moving' : target.position > position ? 'opening' : 'closing'));
        const label = position === undefined ? 'Awaiting feedback' : position + '% open';
        const send = (action: 'open' | 'stop' | 'close') => this.moveShade(shade.id, action);
        return html`<div class="load-control wall-control roller-shade ${moving ? 'moving' : ''}" role="group" aria-label=${shade.name}>
          <div class="face">
            <button type="button" class="endpoint open-shade ${position === 100 ? 'selected' : ''}" aria-label=${'Open ' + shade.name}
              ?disabled=${disabled} @click=${() => send('open')}>${icon('chevronUp', 24)}</button>
            <span class="shade-window" aria-hidden="true">
              ${position === undefined ? nothing : html`<span class="fabric" style=${'height:' + (100 - position) + '%'}></span>`}
              ${moving ? html`<span class="shade-motion ${moving}"></span>` : nothing}
              <span class="roller"></span>
            </span>
            <span class="shade-caption">
              <span class="load-name">${shade.name}</span>
              <span class="reading">${moving ? 'Moving' : label}</span>
            </span>
            <button type="button" class="endpoint stop-shade" aria-label=${'Stop ' + shade.name}
              ?disabled=${disabled} @click=${() => send('stop')}><span class="stop-icon" aria-hidden="true"></span></button>
            <button type="button" class="endpoint close-shade ${position === 0 ? 'selected' : ''}" aria-label=${'Close ' + shade.name}
              ?disabled=${disabled} @click=${() => send('close')}>${icon('chevronDown', 24)}</button>
            ${this.loadLevel(position ?? 0, shade.name + ' openness', disabled,
              (value) => this.moveShade(shade.id, 'position', value))}
          </div>
        </div>`;
      });
    } else if (this.sheet === 'fans') {
      body = repeat(room.fans ?? [], (fan) => fan.id, (fan) => html`
        <div class="load-control wall-control fan-control ${fan.speed !== undefined && fan.speed > 0 ? 'on' : ''}">
          <div class="face">
            <span class="fan-icon">${icon('fan', 30, 1.5)}</span>
            <span class="load-name">${fan.name}</span>
            <span class="reading" aria-live="polite">${fan.speed === undefined ? 'Awaiting feedback' : FAN_SPEEDS[fan.speed]}</span>
            <div class="fan-speeds" role="group" aria-label=${fan.name + ' speed'}>
              ${FAN_SPEEDS.map((label, speed) => html`<button type="button"
                class="fan-speed ${fan.speed === speed ? 'selected' : ''}" aria-pressed=${fan.speed === speed} ?disabled=${disabled}
                @click=${() => this.send({ kind: 'fan-speed', id: fan.id, roomId: room.id, action: 'speed', value: speed })}>
                <span class="indicator" aria-hidden="true"></span>${label}
              </button>`)}
            </div>
          </div>
        </div>
      `);
    } else if (this.sheet === 'buttons') {
      body = room.experienceButtons?.map((button) => html`
        <div class="load">
          <button type="button" class="btn ${(button.kind === 'countdown' ? button.countdown?.running : button.on) ? 'sel' : 'outline'}" ?disabled=${this.store.connection !== 'online'}
            @click=${() => this.send({ kind: 'experience-button', id: button.id, roomId: room.id, action: 'select' })}>
            ${button.name} · ${button.kind === 'countdown'
              ? !button.countdown ? 'Unknown' : button.countdown.running
                ? `${button.countdown.remaining} min remaining` : 'Idle'
              : button.on === undefined ? 'Unknown' : button.on ? 'On' : 'Off'}
          </button>
        </div>
      `);
    } else if (this.sheet === 'relays') {
      body = room.relays?.map((relay) => html`
            <div class="load">
              <span class="top">${relay.name} · <em>${relay.closed === undefined ? 'Unknown' : relay.closed ? 'Closed' : 'Open'}</em></span>
              <div class="rocker" role="group" aria-label=${relay.name}>
                <button type="button" class="btn ${relay.closed === false ? 'sel' : 'outline'}" aria-pressed=${relay.closed === false} ?disabled=${this.store.connection !== 'online'}
                  @click=${() => this.send({ kind: 'relay', id: relay.id, roomId: room.id, action: 'open' })}>OPEN</button>
                <button type="button" class="btn ${relay.closed === true ? 'sel' : 'outline'}" aria-pressed=${relay.closed === true} ?disabled=${this.store.connection !== 'online'}
                  @click=${() => this.send({ kind: 'relay', id: relay.id, roomId: room.id, action: 'close' })}>CLOSE</button>
              </div>
            </div>
          `);
    } else {
      const id = room.thermostatId!;
      const t = this.store.get('thermostat', id);
      body = t ? html`<div class="load" style="flex-direction:row;align-items:center;justify-content:space-between;border:none">
        <span style="font-size:96px;font-weight:200">${t.setpoint}°</span>
        <span style="display:flex;gap:14px">
          ${iconButton('chevronDown', 'Lower setpoint', () => this.send({ kind: 'thermostat', id, action: 'setpoint', value: t.setpoint - 1 }))}
          ${iconButton('chevronUp', 'Raise setpoint', () => this.send({ kind: 'thermostat', id, action: 'setpoint', value: t.setpoint + 1 }))}
          ${iconButton('power', t.power ? 'Turn climate off' : 'Turn climate on', () => this.send({ kind: 'thermostat', id, action: 'power', value: !t.power }), `btn square ${t.power ? 'on' : ''}`)}
        </span>
      </div>` : html`<p class="empty">Waiting for climate state</p>`;
    }
    const scenes = this.sheet === 'lighting'
      ? this.store.config.scenes.filter((scene) => scene.roomIds?.includes(room.id))
      : [];
    const closeSheet = () => { this.sheet = null; this.scenesOpen = false; };
    return html`<div class="scrim" @click=${closeSheet}></div>
      <section class="sheet" aria-label="${room.name} ${this.sheet}">
        <header class="sheet-heading">
          <span class="label">${room.name} · ${this.sheet}</span>
          ${scenes.length ? html`<button type="button" class="btn neu-square room-scenes-tab" aria-expanded=${this.scenesOpen}
            @click=${() => (this.scenesOpen = !this.scenesOpen)}>${icon('scenes', 16)} Light Scenes</button>` : nothing}
        </header>
        <div class="scroll ${this.sheet === 'lighting' || this.sheet === 'shades' || this.sheet === 'fans' ? 'load-grid' : ''}">${body}</div>
        <button type="button" class="close neu-square primary" aria-label="Close" @click=${closeSheet}>${icon('close', 36, 1.3)}</button>

        ${scenes.length && this.scenesOpen ? html`<aside class="room-scenes" aria-label="Room light scenes">
          <div class="room-scenes-header"><span class="label">Light Scenes</span>
            ${iconButton('close', 'Close light scenes', () => (this.scenesOpen = false), 'btn square')}</div>
          <div class="room-scenes-list">${repeat(scenes, (scene) => scene.id, (scene, index) => html`
            <button type="button" class="room-scene" ?disabled=${this.store.connection !== 'online'}
              @click=${() => this.send({ kind: 'scene', id: scene.id, action: 'run' })}>
              <span class="room-scene-icon" aria-hidden="true">${index === 0
                ? html`<svg width="34" height="34" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><path d="M9 18v-1.5c0-2-3-3-3-6.5a6 6 0 0 1 12 0c0 3.5-3 4.5-3 6.5V18zM10 21h4"/></svg>`
                : html`<span class="room-scene-dots">${Array.from({ length: 9 }, () => html`<i></i>`)}</span>`}</span><span>${scene.name}</span>
            </button>`)}</div>
        </aside>` : nothing}
      </section>`;
  }
}

customElements.define('z-rooms', RoomsPage);
customElements.define('z-room', RoomPage);
