import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { encodeFrames } from '../src/adapters/wire';
import { HouseStore } from '../src/model/store';
import { PANEL_FLOOR, PanelRouterAdapter, REQUEST_GAP_MS, ROUTER_JOINS as J, SR260_KEYS } from '../src/adapters/panel-router';
import type { CrComLibLike } from '../src/adapters/crestron';

// Simulates the Control4 - Panel Room Router joins. Frames use the same
// w:1 / base64 / 160-character envelope as Control4Gateway UiSerialFrames.cs.

/** Page request op -> stream kind, as handled by PanelRoomModule.Request. */
const PAGE_KIND: Record<string, string> = {
  device_page: 'd', audio_page: 'a', video_page: 'v', media_page: 'm', playing_page: 'n',
  rooms_page: 'rooms', scenes_page: 'scenes', audio_sessions_page: 'audio_sessions', video_sessions_page: 'video_sessions', site_page: 'site', outdoor_page: 'outdoor',
  house_devices_page: 'house_devices'
};

class FakeRouter implements CrComLibLike {
  subs = new Map<string, (v: never) => void>();
  requests: Array<Record<string, unknown>> = [];
  raw: string[] = [];
  private epoch = 0;
  /** Latest frames per stream kind, served on page requests like UiSerialFrames.Read. */
  private served = new Map<string, { join: string; frames: string[] }>();
  subscribeState(type: string, signal: string, cb: (v: never) => void) { this.subs.set(`${type}:${signal}`, cb); return signal; }
  unsubscribeState(type: string, signal: string) { this.subs.delete(`${type}:${signal}`); }
  publishEvent(type: string, signal: string, value: unknown) {
    expect(type).toBe('s');
    expect(signal).toBe(J.request);
    this.raw.push(String(value));
    const request = JSON.parse(String(value));
    this.requests.push(request);
    const stream = this.served.get(PAGE_KIND[request.op]);
    if (stream && Number.isInteger(request.value)) {
      const page = Math.max(1, Math.min(request.value, stream.frames.length));
      this.emit('s', stream.join, stream.frames[page - 1]);
    }
  }
  emit(type: string, signal: string, value: unknown) { this.subs.get(`${type}:${signal}`)?.(value as never); }
  connect(online = true) { this.emit('b', J.connected, online); }
  /** Publish page 1 of a framed document, as the router does; the client requests the rest. */
  doc(join: string, room: number, kind: string, document: unknown, send = true): string[] {
    const epoch = (++this.epoch).toString(16).padStart(16, '0');
    const frames = encodeFrames(room, kind, epoch, document);
    this.served.set(kind, { join, frames });
    if (send) this.emit('s', join, frames[0]);
    return frames;
  }
  plain(join: string, packet: unknown) { this.emit('s', join, JSON.stringify(packet)); }
  ops() { return this.requests.map((r) => r.op).filter((op) => !String(op).endsWith('_page')); }
  last(op: string) { return [...this.requests].reverse().find((r) => r.op === op); }
  clear() { this.requests = []; }
}

const tick = () => new Promise((r) => setTimeout(r, 0));
const ROOMS = { room_id: 0, rooms: [{ id: 13, name: 'Living Room' }, { id: 212, name: 'Kitchen' }] };
const DEVICES = {
  room_id: 13,
  devices: [
    { id: 437, name: 'Main Hall', shade: false, value: 40 },
    { id: 438, name: 'Counter', shade: false, value: null },
    { id: 226, name: 'Middle Shade', shade: true, value: 100 }
  ]
};
const LISTEN = { room_id: 13, mode: 'listen', valid: true, page: 1, pages: 1, total: 2, items: [{ id: '65', name: "Michael's Spotify" }, { id: '1648', name: 'Pandora' }] };
const WATCH = { room_id: 13, mode: 'watch', valid: true, page: 1, pages: 1, total: 1, items: [{ id: '542', name: 'SHIELD TV' }] };

function keys(available: string[]) {
  return { wire: 'keys1', room_id: 13, states: SR260_KEYS.map((k) => (available.includes(k) ? 'a' : 'u')).join('') };
}

let router: FakeRouter;
let store: HouseStore;

function start() {
  router = new FakeRouter();
  store = new HouseStore();
  store.attach(new PanelRouterAdapter(router, undefined, 0));
}

/** Connect, publish the room list and open Living Room with its devices and sources. */
function openLivingRoom() {
  router.connect();
  router.doc(J.state, 0, 'rooms', ROOMS);
  store.focusRoom('13');
  router.doc(J.state, 13, 'd', DEVICES);
  router.doc(J.audio, 13, 'a', LISTEN);
  router.doc(J.video, 13, 'v', WATCH);
}

beforeEach(start);

describe('panel router adapter', () => {
  it('asks for the house catalogs on connect and builds lazy rooms', () => {
    expect(store.connection).toBe('connecting');
    router.connect();
    expect(store.connection).toBe('online');
    expect(router.ops()).toEqual(['rooms', 'scenes', 'site', 'outdoor', 'audio_sessions']);
    expect(router.requests.map((r) => r.q)).toEqual([1, 2, 3, 4, 5]);
    router.doc(J.state, 0, 'rooms', ROOMS);
    // The heavy whole-house request waits for the room list.
    expect(router.ops().slice(5)).toEqual(['house_devices']);
    expect(store.has('pool')).toBe(false);
    expect(store.has('irrigation')).toBe(false);
    router.doc(J.state, 0, 'outdoor', {
      room_id: 0, pool: [{ id: 2312, name: 'Pool', PUMPMODE: 'Off' }],
      irrigation: [{ id: 2315, name: 'Irrigation Program', STEP: '1' }]
    });
    expect(store.has('pool')).toBe(true);
    expect(store.has('irrigation')).toBe(true);
    expect(store.config.outdoor?.pool[0].PUMPMODE).toBe('Off');
    expect(store.config.floors).toEqual([PANEL_FLOOR]);
    expect(store.config.rooms.map((r) => [r.id, r.name, r.lazy])).toEqual([['13', 'Living Room', true], ['212', 'Kitchen', true]]);
    expect(store.config.services).toEqual(['media', 'audio', 'pool', 'irrigation']);
    expect(store.roomsOnFloor(PANEL_FLOOR.id)).toHaveLength(2);
  });

  it('selects a focused room once it is in the panel scope', () => {
    store.focusRoom('212');
    router.connect();
    expect(router.ops()).not.toContain('room');
    router.doc(J.state, 0, 'rooms', ROOMS);
    expect(router.last('room')).toMatchObject({ op: 'room', room_id: 212 });
    store.focusRoom('999');
    expect(router.requests.filter((r) => r.op === 'room')).toHaveLength(1);
  });

  it('keeps legacy dimmers visible and updates explicit switch capability without a level change', () => {
    openLivingRoom();
    expect(store.room('13')?.lights.find((light) => light.id === '437')?.dimmable).toBe(true);
    router.plain(J.state, { r: 13, u: 437, v: 40, dim: false });
    expect(store.get('light', '437')).toEqual({ on: true, level: 40, dimmable: false });
    router.plain(J.state, { r: 13, u: 437, v: 0 });
    expect(store.get('light', '437')?.dimmable).toBe(false);
    router.plain(J.state, { r: 13, u: 437, v: 0, dim: true });
    expect(store.get('light', '437')?.dimmable).toBe(true);
  });

  it('loads devices with measured levels and sends device commands', () => {
    openLivingRoom();
    const room = store.room('13')!;
    expect(room.lights.map((l) => l.id)).toEqual(['437', '438']);
    expect(room.shades).toEqual([{ id: '226', name: 'Middle Shade', positionable: true }]);
    expect(room.lazy).toBe(false);
    expect(store.get('light', '437')).toEqual({ on: true, level: 40 });
    expect(store.known('light', '438')).toBe(false);
    expect(store.get('shade', '226')).toEqual({ position: 100 });

    router.plain(J.state, { r: 13, u: 438, v: 0 });
    expect(store.get('light', '438')).toEqual({ on: false, level: 0 });
    router.plain(J.state, { r: 212, u: 437, v: 90 });
    expect(store.get('light', '437')?.level).toBe(40);

    router.clear();
    store.send({ kind: 'light', id: '437', action: 'level', value: 75 });
    store.send({ kind: 'light', id: '437', action: 'toggle' });
    store.send({ kind: 'shade', id: '226', action: 'stop' });
    store.send({ kind: 'shade', id: '226', action: 'position', value: 30 });
    store.send({ kind: 'light', id: '226', action: 'on' });
    store.send({ kind: 'light', id: '9999', action: 'on' });
    expect(router.requests.map(({ q: _q, ...r }) => r)).toEqual([
      { room_id: 13, device_id: 437, action: 'level', value: 75, op: 'device' },
      { room_id: 13, device_id: 437, action: 'level', value: 0, op: 'device' },
      { room_id: 13, device_id: 226, action: 'stop', op: 'device' },
      { room_id: 13, device_id: 226, action: 'level', value: 30, op: 'device' }
    ]);

    router.clear();
    store.send({ kind: 'room-lights', id: '13', action: 'off' });
    store.send({ kind: 'room-lights', id: '13', action: 'ambient' });
    store.send({ kind: 'room-shades', id: '13', action: 'open' });
    expect(router.requests.map((r) => [r.device_id, r.action, r.value])).toEqual([[437, 'level', 0], [438, 'level', 0], [226, 'open', undefined]]);
  });

  it('requests a missing page with the room id and completes the document', () => {
    router.connect();
    router.doc(J.state, 0, 'rooms', ROOMS);
    store.focusRoom('13');
    const many = { room_id: 13, devices: Array.from({ length: 30 }, (_, i) => ({ id: 500 + i, name: `Light ${i}`, shade: false, value: i })) };
    const frames = router.doc(J.state, 13, 'd', many, false);
    expect(frames.length).toBeGreaterThan(2);
    router.clear();
    // A later page arrives first (as after a panel reconnect): the adapter asks
    // for page 1, then each following page until the document is complete.
    router.emit('s', J.state, frames[1]);
    const pages = router.requests.map((r) => [r.op, r.room_id, r.value]);
    expect(pages[0]).toEqual(['device_page', 13, 1]);
    expect(pages).toContainEqual(['device_page', 13, frames.length]);
    expect(store.room('13')!.lights).toHaveLength(30);
    expect(store.get('light', '529')).toEqual({ on: true, level: 29 });
  });

  it('builds media state from feedback, sources, keys and now playing', () => {
    openLivingRoom();
    router.plain(J.state, { r: 13, volume: 31, volumeValid: 1, muted: 0, muteValid: 1, power: 1 });
    router.plain(J.state, { r: 13, audioIndex: 2, audioValid: 1, videoIndex: 0, videoValid: 1, nowPlayingValid: 1 });
    router.plain(J.keys, keys(['Play', 'Pause', 'Skip_Forward']));
    router.doc(J.playing, 13, 'n', { room_id: 13, valid: true, title: 'Amish Paradise', artist: 'Weird Al', album: 'Bad Hair Day', artwork: 'https://example.invalid/a.jpg', station: 'Weird Al Radio', playback_action: 'pause' });
    router.plain(J.progress, { room_id: 13, valid: true, source_id: 1648, track_revision: 'abcdef0123456789', position_seconds: 12, duration_seconds: 235 });
    expect(store.get('media', '13')).toEqual({
      power: true, volume: 31, mute: false, activeSourceId: '1648',
      title: 'Amish Paradise', artist: 'Weird Al', album: 'Bad Hair Day', artUrl: 'https://example.invalid/a.jpg',
      station: 'Weird Al Radio', playbackAction: 'pause', progress: { position: 12, duration: 235 },
      actions: ['off', 'mute', 'unmute', 'play', 'pause', 'next'],
      keys: ['Play', 'Skip_Forward', 'Pause']
    });

    router.plain(J.state, { r: 13, volumeValid: 0, nowPlayingValid: 0 });
    const st = store.get('media', '13')!;
    expect(st.volume).toBeUndefined();
    expect(st.title).toBeUndefined();

    router.clear();
    store.send({ kind: 'media', id: '13', action: 'next' });
    store.send({ kind: 'media', id: '13', action: 'previous' });
    store.send({ kind: 'media', id: '13', action: 'volume', value: 120 });
    store.send({ kind: 'media', id: '13', action: 'mute' });
    store.send({ kind: 'media', id: '13', action: 'off' });
    store.send({ kind: 'media', id: '13', action: 'source', value: '542' });
    store.send({ kind: 'media', id: '13', action: 'source', value: '77' });
    store.send({ kind: 'media', id: '212', action: 'off' });
    expect(router.requests.map(({ q: _q, ...r }) => r)).toEqual([
      { room_id: 13, value: 'Skip_Forward', op: 'key' },
      { room_id: 13, value: 100, op: 'volume' },
      { room_id: 13, op: 'mute_on' },
      { room_id: 13, op: 'off' },
      { room_id: 13, value: '542', op: 'video' }
    ]);
  });

  it('opens a service, shows its presets and selects one with the catalog revision', () => {
    openLivingRoom();
    router.clear();
    store.send({ kind: 'media', id: '13', action: 'source', value: '65' });
    expect(router.last('audio')).toMatchObject({ room_id: 13, value: '65' });
    router.doc(J.catalog, 13, 'm', { room_id: 13, kind: 'service_presets', valid: false, revision: '', items: [], selection_supported: false });
    expect(store.get('media', '13')?.catalog).toEqual({ sourceId: '65', items: [], selectable: false, pending: true });

    const catalog = { room_id: 13, kind: 'service_presets', scope: 'room', valid: true, revision: 'r1', page: 1, pages: 1, total: 2, partial: false,
      items: [{ id: '171', name: 'HEAVY' }, { id: '172', name: 'Chill' }], selection_supported: true, source_id: 65 };
    router.doc(J.catalog, 13, 'm', catalog);
    expect(store.get('media', '13')?.catalog).toMatchObject({ sourceId: '65', selectable: false, pending: false });
    store.send({ kind: 'media', id: '13', action: 'item', value: '171' });
    expect(router.last('item')).toBeUndefined();

    router.plain(J.state, { r: 13, selectReady: 1 });
    expect(store.get('media', '13')?.catalog?.selectable).toBe(true);
    store.send({ kind: 'media', id: '13', action: 'item', value: '999' });
    store.send({ kind: 'media', id: '13', action: 'item', value: '171' });
    expect(router.requests.filter((r) => r.op === 'item')).toEqual([
      { room_id: 13, kind: 'service_presets', revision: 'r1', id: '171', op: 'item', q: expect.any(Number) }
    ]);

    router.doc(J.catalog, 13, 'm', { room_id: 13, kind: '', valid: false, revision: '', items: [], selection_supported: false });
    expect(store.get('media', '13')?.catalog).toBeUndefined();
  });

  it('sends only advertised remote keys and never Room_Off as a key', () => {
    openLivingRoom();
    router.plain(J.keys, keys(['Room_Off', 'Up', 'Select', 'Digit_5', 'Menu']));
    expect(store.get('media', '13')?.keys).toEqual(['Up', 'Select', 'Menu', 'Digit_5']);
    router.clear();
    for (const k of ['Up', 'Digit_5', 'Down', 'Room_Off', 'Guide']) store.send({ kind: 'media', id: '13', action: 'key', value: k });
    expect(router.requests.map((r) => r.value)).toEqual(['Up', 'Digit_5']);
  });

  it('treats local_ui keys as panel-side and never sends them', () => {
    openLivingRoom();
    // GatewayRoomTransport always marks Watch, Listen and List as local_ui ('l').
    const states = SR260_KEYS.map((k) => (['Watch', 'Listen', 'List'].includes(k) ? 'l' : k === 'Up' ? 'a' : 'u')).join('');
    router.plain(J.keys, { wire: 'keys1', room_id: 13, states });
    expect(store.get('media', '13')?.keys).toEqual(['Up']);
    router.clear();
    for (const k of ['Watch', 'Listen', 'List']) store.send({ kind: 'media', id: '13', action: 'key', value: k });
    expect(router.requests).toEqual([]);
    router.plain(J.keys, { room_id: 13, keys: [{ key: 'List', state: 'local_ui' }, { key: 'Menu', state: 'advertised' }] });
    expect(store.get('media', '13')?.keys).toEqual(['Menu']);
  });

  it('reports shuffle only for the service that is actually playing', () => {
    openLivingRoom();
    const pandora = { room_id: 13, kind: 'service_presets', valid: true, revision: 'p1', items: [{ id: '5', name: 'Radio' }],
      selection_supported: true, source_id: 1648, pandora_shuffle: 'Off' };
    router.clear();
    store.send({ kind: 'media', id: '13', action: 'shuffle', value: true });
    // Browsing Pandora while nothing is playing: no shuffle.
    router.doc(J.catalog, 13, 'm', pandora);
    expect(store.get('media', '13')?.shuffle).toBeUndefined();
    store.send({ kind: 'media', id: '13', action: 'shuffle', value: true });
    expect(router.last('shuffle')).toBeUndefined();
    // Pandora (index 2) is the measured active source.
    router.plain(J.state, { r: 13, audioIndex: 2, audioValid: 1 });
    expect(store.get('media', '13')?.shuffle).toBe(false);
    store.send({ kind: 'media', id: '13', action: 'shuffle', value: true });
    expect(router.last('shuffle')).toMatchObject({ room_id: 13, value: 1 });
    // Spotify becomes active: Pandora's catalog no longer applies.
    router.plain(J.state, { r: 13, audioIndex: 1 });
    expect(store.get('media', '13')?.shuffle).toBeUndefined();
    router.plain(J.state, { r: 13, audioIndex: 2 });
    router.doc(J.catalog, 13, 'm', { ...pandora, kind: 'other_kind', revision: 'p2' });
    expect(store.get('media', '13')?.shuffle).toBeUndefined();
  });

  it('retains scene room assignments when scenes arrive ahead of rooms', () => {
    router.connect();
    router.doc(J.state, 0, 'scenes', {
      room_id: 0, revision: 'aaaa', actions: [
        { slot: 1, kind: 'light', name: 'Living Room Evening', room_ids: [13, 'invalid'] },
        { slot: 2, kind: 'macro', name: 'House Off', room_ids: [13] }
      ]
    });
    router.doc(J.state, 0, 'rooms', ROOMS);
    expect(store.config.scenes[0].roomIds).toEqual(['13']);
    expect(store.config.scenes[1].roomIds).toBeUndefined();
  });
  it('lists video sessions and sends only a listed matching Watch destination', () => {
    router.connect();
    router.doc(J.state, 0, 'rooms', ROOMS);
    router.clear();
    store.send({ kind: 'video-sessions' });
    router.doc(J.state, 0, 'video_sessions', { room_id: 0, sessions: [
      { room: 13, room_name: 'Living Room', source: 1713, source_name: 'Apple TV', targets: [{ id: 212, name: 'Kitchen' }] }
    ] });
    store.send({ kind: 'video-session-watch', room: 13, source: 1713, target: 89 });
    store.send({ kind: 'video-session-watch', room: 13, source: 1713, target: 212 });
    expect(router.requests.filter((request) => request.op !== 'video_sessions_page')).toEqual([
      { op: 'video_sessions', q: expect.any(Number) },
      { op: 'video_session_watch', room_id: 13, source: 1713, target: 212, q: expect.any(Number) }
    ]);
  });
  it('publishes scenes and runs them only with the current revision', () => {
    router.connect();
    router.doc(J.state, 0, 'rooms', ROOMS);
    router.doc(J.state, 0, 'scenes', { room_id: 0, revision: 'aaaa', actions: [{ slot: 1, kind: 'advanced_lighting', name: 'House Off' }, { slot: 2, kind: 'macros', name: 'House Off' }] });
    expect(store.config.services).toEqual(['media', 'audio', 'scenes']);
    expect(store.config.scenes).toEqual([
      { id: 'aaaa:1', name: 'House Off', description: 'Advanced lighting' },
      { id: 'aaaa:2', name: 'House Off', description: 'Macros' }
    ]);
    router.clear();
    store.send({ kind: 'scene', id: 'aaaa:2', action: 'run' });
    router.doc(J.state, 0, 'scenes', { room_id: 0, revision: 'bbbb', actions: [{ slot: 1, kind: 'macros', name: 'Main Level Off' }] });
    store.send({ kind: 'scene', id: 'aaaa:1', action: 'run' });
    expect(router.requests).toEqual([{ revision: 'aaaa', slot: 2, op: 'scene_activate', q: expect.any(Number) }]);
  });

  it('shows the session track on every member zone and keeps only https artwork', () => {
    router.connect();
    router.doc(J.state, 0, 'rooms', { room_id: 0, rooms: [{ id: 13, name: 'Living Room' }, { id: 212, name: 'Kitchen' }] });
    router.doc(J.state, 0, 'audio_sessions', {
      room_id: 0, available: true, revision: 'rev1',
      rooms: [{ id: 13, volume: 20, muted: false }, { id: 212, volume: 25, muted: false }],
      sessions: [{ queue_id: 7, room_ids: [13, 212], owner_room_id: 13, now_playing: {
        title: 'Song A', artist: 'Artist B', album: 'Album C', artwork: 'https://art.example/c.jpg' } }]
    });
    const track = { title: 'Song A', artist: 'Artist B', album: 'Album C', artwork: 'https://art.example/c.jpg' };
    expect(store.get('zone', '13')?.track).toEqual(track);
    expect(store.get('zone', '212')?.nowPlaying).toBe('Song A');

    router.doc(J.state, 0, 'audio_sessions', {
      room_id: 0, available: true, revision: 'rev1',
      rooms: [{ id: 13, volume: 20, muted: true }],
      sessions: [{ queue_id: 7, room_ids: [13], owner_room_id: 13, now_playing: { title: 'Song D', artwork: 'http://insecure.example/x.jpg' } }]
    });
    expect(store.get('zone', '13')?.track).toEqual({ title: 'Song D' });
    expect(store.get('zone', '13')?.mute).toBe(true);
  });

  it('turns audio sessions into zones with only the actions the router accepts', () => {
    router.connect();
    router.doc(J.state, 0, 'rooms', { room_id: 0, rooms: [{ id: 13, name: 'Living Room' }, { id: 212, name: 'Kitchen' }, { id: 89, name: 'Master Bed' }] });
    router.clear();
    router.doc(J.state, 0, 'audio_sessions', {
      room_id: 0, available: true, revision: 'rev1',
      rooms: [{ id: 13, name: 'Living Room', volume: 29, muted: false }, { id: 212, name: 'Kitchen', volume: 30, muted: true }, { id: 89, name: 'Master Bed' }],
      sessions: [{ queue_id: 7, room_ids: [13, 212], owner_room_id: 13, state: 'play' }]
    });
    // Not on the Audio page, so the probe closes the background refresh.
    expect(router.ops()).toEqual(['audio_sessions_close']);
    expect(store.config.services).toEqual(['media', 'audio']);
    expect(store.config.audioZones.map((z) => z.id)).toEqual(['13', '212', '89']);
    expect(store.get('zone', '13')).toEqual({ power: true, volume: 29, mute: false, actions: ['volume', 'volume-up', 'volume-down', 'mute', 'unmute', 'off'] });
    expect(store.get('zone', '212')?.actions).toContain('off');
    expect(store.get('zone', '89')).toEqual({ power: false, actions: ['on'] });

    store.setView('audio');
    expect(router.last('audio_sessions')).toBeDefined();
    router.clear();
    store.send({ kind: 'zone', id: '13', action: 'volume-up' });
    store.send({ kind: 'zone', id: '212', action: 'unmute' });
    store.send({ kind: 'zone', id: '13', action: 'off' });
    store.send({ kind: 'zone', id: '212', action: 'off' });
    store.send({ kind: 'zone', id: '89', action: 'on' });
    store.send({ kind: 'zones-off' });
    expect(router.requests.map(({ q: _q, ...r }) => r)).toEqual([
      { revision: 'rev1', room_id: 13, queue_id: 7, value: 34, op: 'audio_session_volume' },
      { revision: 'rev1', room_id: 212, queue_id: 7, value: 0, op: 'audio_session_mute' },
      { revision: 'rev1', room_id: 13, queue_id: 7, op: 'audio_session_room_off' },
      { revision: 'rev1', room_id: 212, queue_id: 7, op: 'audio_session_remove' },
      { revision: 'rev1', room_id: 89, queue_id: 7, op: 'audio_session_add' },
      { revision: 'rev1', op: 'audio_sessions_off' }
    ]);
    router.clear();
    store.setView('home');
    expect(router.ops()).toEqual(['climate', 'security_watch', 'security', 'audio_sessions_close']);
  });

  it('updates only scoped room summary power without selecting the room', () => {
    router.connect();
    router.doc(J.state, 0, 'rooms', { room_id: 0, rooms: [{ id: 81, name: 'Den' }, { id: 82, name: 'Office' }] });
    router.clear();
    router.emit('s', J.state, JSON.stringify({ h: 6, u: 82, v: true }));
    router.emit('s', J.state, JSON.stringify({ h: 6, u: 83, v: true }));
    expect(store.room('82')?.mediaPower).toBe(true);
    expect(store.room('81')?.mediaPower).toBeUndefined();
    expect(store.room('83')).toBeUndefined();
    expect(store.get('media', '82')).toBeUndefined();
    expect(router.ops()).toEqual([]);
    router.emit('s', J.state, JSON.stringify({ h: 6, u: 82, v: false }));
    expect(store.room('82')?.mediaPower).toBe(false);
    router.emit('s', J.state, JSON.stringify({ h: 6, u: 82, v: null }));
    expect(store.room('82')?.mediaPower).toBeUndefined();
  });

  it('groups scoped rooms by Director floor and honors Composer room order', () => {
    router.connect();
    router.doc(J.state, 0, 'rooms', { room_id: 0, rooms: [
      { id: 81, name: 'Upper Den', floor_id: 12, floor_name: 'Upper', floor_order: 2, room_order: 3 },
      { id: 82, name: 'Living', floor_id: 11, floor_name: 'Main', floor_order: 1, room_order: 4 },
      { id: 83, name: 'Entry', floor_id: 11, floor_name: 'Main', floor_order: 1, room_order: 1 },
      { id: 84, name: 'Unknown Floor', floor_id: 13 }
    ] });
    expect(store.sortedFloors().filter((floor) => floor.id !== 'panel').map((floor) => floor.name)).toEqual(['Main', 'Upper']);
    expect(store.roomsOnFloor('11').map((room) => room.name)).toEqual(['Entry', 'Living']);
    expect(store.roomsOnFloor('12').map((room) => room.name)).toEqual(['Upper Den']);
    expect(store.roomsOnFloor('panel').map((room) => room.name)).toEqual(['Unknown Floor']);
  });

  it('selects only Experience Buttons from the reported room catalog', () => {
    router.connect();
    router.doc(J.state, 0, 'rooms', { room_id: 0, rooms: [{ id: 81, name: 'Den' }] });
    router.doc(J.state, 0, 'house_devices', { room_id: 0, rooms: [
      { id: 81, devices: [], experience_buttons: [{ id: 321, name: 'Find Remote', on: false }] },
      { id: 82, devices: [], experience_buttons: [{ id: 322, name: 'Other Remote' }] }
    ] });
    expect(store.room('81')?.experienceButtons).toEqual([{ id: '321', name: 'Find Remote', on: false }]);
    router.emit('s', J.state, JSON.stringify({ h: 3, u: 321, v: true }));
    expect(store.room('81')?.experienceButtons?.[0]?.on).toBe(true);
    router.emit('s', J.state, JSON.stringify({ h: 3, u: 321, v: null }));
    expect(store.room('81')?.experienceButtons?.[0]?.on).toBeUndefined();
    router.clear();
    store.send({ kind: 'experience-button', roomId: '82', id: '322', action: 'select' });
    store.send({ kind: 'experience-button', roomId: '81', id: '321', action: 'select' });
    expect(router.ops()).toEqual(['experience_select']);
    expect(router.last('experience_select')).toMatchObject({ room_id: 81, device_id: 321 });
  });

  it('shows the Countdown snapshot and applies event-driven tick and expiry state', () => {
    router.connect();
    router.doc(J.state, 0, 'rooms', { room_id: 0, rooms: [{ id: 81, name: 'Den' }] });
    router.doc(J.state, 0, 'house_devices', { room_id: 0, rooms: [
      { id: 81, devices: [], experience_buttons: [
        { id: 321, name: 'Lights Countdown', kind: 'countdown', countdown: { running: true, remaining: 10 } }
      ] }
    ] });
    const reading = () => store.room('81')?.experienceButtons?.[0]?.countdown;
    expect(reading()).toEqual({ running: true, remaining: 10 });
    router.clear();
    const press = { kind: 'experience-button', roomId: '81', id: '321', action: 'select' } as const;
    store.send(press);
    store.send(press);
    expect(router.ops()).toEqual(['experience_select', 'experience_select']);
    expect(router.last('experience_select')).toMatchObject({ room_id: 81, device_id: 321 });
    expect(reading()).toEqual({ running: true, remaining: 10 });
    router.emit('s', J.state, JSON.stringify({ h: 5, u: 321, v: { running: true, remaining: 8 } }));
    expect(reading()).toEqual({ running: true, remaining: 8 });
    router.emit('s', J.state, JSON.stringify({ h: 5, u: 321, v: { running: true, remaining: -1 } }));
    expect(reading()).toEqual({ running: true, remaining: 8 });
    router.emit('s', J.state, JSON.stringify({ h: 5, u: 321, v: { running: false, remaining: 0 } }));
    expect(reading()).toEqual({ running: false, remaining: 0 });
  });

  it('shows measured fan speed and sends only scoped five-speed requests', () => {
    router.connect();
    router.doc(J.state, 0, 'rooms', { room_id: 0, rooms: [{ id: 81, name: 'Den' }] });
    router.doc(J.state, 0, 'house_devices', { room_id: 0, rooms: [
      { id: 81, devices: [], fans: [{ id: 321, name: 'Den Fan', speed: 0 }] }
    ] });
    expect(store.room('81')?.fans).toEqual([{ id: '321', name: 'Den Fan', speed: 0 }]);
    router.clear();
    store.send({ kind: 'fan-speed', roomId: '81', id: '321', action: 'speed', value: 6 });
    store.send({ kind: 'fan-speed', roomId: '81', id: '999', action: 'speed', value: 3 });
    store.send({ kind: 'fan-speed', roomId: '81', id: '321', action: 'speed', value: 3 });
    expect(router.ops()).toEqual(['fan_speed']);
    expect(router.last('fan_speed')).toMatchObject({ room_id: 81, device_id: 321, value: 3 });
    expect(store.room('81')?.fans?.[0]?.speed).toBe(0);
    router.emit('s', J.state, JSON.stringify({ h: 4, u: 321, v: 3 }));
    expect(store.room('81')?.fans?.[0]?.speed).toBe(3);
    router.emit('s', J.state, JSON.stringify({ h: 4, u: 321, v: null }));
    expect(store.room('81')?.fans?.[0]?.speed).toBeUndefined();
  });

  it('loads whole-house lights and shades and controls rooms that are not open', () => {
    router.connect();
    // Catalog first, room list second: the adapter must apply it once the rooms exist.
    router.doc(J.state, 0, 'house_devices', { room_id: 0, rooms: [
      { id: 13, devices: [{ id: 437, name: 'Main Hall', shade: false, value: 40 }, { id: 226, name: 'Middle Shade', shade: true, value: 100 }] },
      { id: 212, devices: [{ id: 602, name: 'Island', shade: false, value: null }] },
      { id: 999, devices: [{ id: 5, name: 'Out of scope', shade: false, value: 1 }] }
    ] });
    router.doc(J.state, 0, 'rooms', ROOMS);
    expect(store.room('212')!.lights.map((l) => l.id)).toEqual(['602']);
    expect(store.room('13')!.shades.map((s) => s.id)).toEqual(['226']);
    expect(store.config.services).toEqual(['lighting', 'shades', 'media', 'audio']);
    expect(store.get('light', '437')).toEqual({ on: true, level: 40 });
    expect(store.known('light', '602')).toBe(false);
    expect(store.known('light', '5')).toBe(false);

    router.plain(J.state, { h: 1, u: 602, v: 70 });
    expect(store.get('light', '602')).toEqual({ on: true, level: 70 });

    router.clear();
    store.send({ kind: 'room-lights', id: '212', action: 'off' });
    store.send({ kind: 'shade', id: '226', action: 'open' });
    store.send({ kind: 'light', id: '5', action: 'on' });
    expect(router.requests.map(({ q: _q, ...r }) => r)).toEqual([
      { room_id: 212, device_id: 602, action: 'level', value: 0, op: 'house_device' },
      { room_id: 13, device_id: 226, action: 'open', op: 'house_device' }
    ]);

    // Opening a room uses its session; leaving it keeps whole-house state.
    store.focusRoom('13');
    router.doc(J.state, 13, 'd', DEVICES);
    router.clear();
    store.send({ kind: 'light', id: '437', action: 'off' });
    expect(router.last('device')).toMatchObject({ room_id: 13, device_id: 437, value: 0 });
    store.focusRoom('212');
    expect(store.get('shade', '226')).toEqual({ position: 100 });
  });

  it('publishes location and reported weather from the site document', () => {
    router.connect();
    router.doc(J.state, 0, 'site', { room_id: 0, name: 'Sample City, ST', latitude: 40, longitude: -95, temperature_unit: 'F',
      weather_status: 'reporting', weather: { temperature: 68, low: 55, high: 72 } });
    expect(store.config.location).toEqual({ name: 'Sample City, ST', latitude: 40, longitude: -95 });
    expect(store.get('weather', 'house')).toEqual({ temperature: 68, unit: 'F', low: 55, high: 72 });

    // A driver that stops reporting clears weather but keeps the location.
    router.doc(J.state, 0, 'site', { room_id: 0, latitude: 40, longitude: -95, weather_status: 'not_reporting' });
    expect(store.known('weather', 'house')).toBe(false);
    expect(store.config.location).toEqual({ latitude: 40, longitude: -95 });

    router.doc(J.state, 0, 'site', { room_id: 0, latitude: 400, longitude: -95, weather_status: 'unavailable' });
    expect(store.config.location).toBeUndefined();

    router.connect(false);
    expect(store.known('weather', 'house')).toBe(false);
  });

  it('marks zones unavailable when Whole House Audio is not available', () => {
    router.connect();
    router.doc(J.state, 0, 'rooms', ROOMS);
    router.doc(J.state, 0, 'audio_sessions', { room_id: 0, available: false });
    expect(store.config.audioZones).toEqual([]);
    expect(store.config.services).toEqual(['media', 'audio']);
  });

  it('clears the previous room and ignores its late frames after a room switch', () => {
    openLivingRoom();
    router.plain(J.state, { r: 13, power: 1 });
    store.focusRoom('212');
    expect(router.last('room')).toMatchObject({ room_id: 212 });
    expect(store.known('light', '437')).toBe(false);
    expect(store.known('media', '13')).toBe(false);
    router.plain(J.state, { r: 13, u: 437, v: 80 });
    router.plain(J.state, { r: 13, power: 1 });
    expect(store.known('light', '437')).toBe(false);
    expect(store.known('media', '13')).toBe(false);
    router.clear();
    store.send({ kind: 'light', id: '437', action: 'on' });
    expect(router.requests).toEqual([]);
  });

  it('goes offline, clears live state and re-selects the room on reconnect', async () => {
    openLivingRoom();
    router.connect(false);
    expect(store.connection).toBe('offline');
    expect(store.known('light', '437')).toBe(false);
    router.clear();
    store.send({ kind: 'light', id: '437', action: 'on' });
    expect(router.requests).toEqual([]);
    router.connect(true);
    expect(router.ops()).toEqual(['rooms', 'scenes', 'site', 'outdoor', 'audio_sessions', 'room']);
    expect(router.last('room')).toMatchObject({ room_id: 13 });
    router.doc(J.state, 13, 'd', DEVICES);
    expect(store.get('light', '437')?.level).toBe(40);
    await tick();
  });

  it('keeps every request within the 255-character serial limit', () => {
    openLivingRoom();
    for (let i = 0; i < 50; i++) store.send({ kind: 'light', id: '437', action: 'level', value: i });
    expect(router.raw.every((r) => r.length <= 255)).toBe(true);
    expect(router.requests[router.requests.length - 1].q).toBe(router.requests.length);
  });

  it('rejects malformed frames and documents for another room', () => {
    openLivingRoom();
    const before = store.room('13')!.lights.length;
    router.emit('s', J.state, '{"w":1,"r":13,"k":"d","e":"zz","p":1,"n":1,"d":"!!"}');
    router.emit('s', J.state, 'not json');
    router.doc(J.state, 13, 'd', { room_id: 212, devices: [] });
    expect(store.room('13')!.lights).toHaveLength(before);
  });
});

// Request$ is one SIMPL+ STRING_INPUT: a busy CHANGE handler only sees the latest value.
describe('panel router request pacing', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    router = new FakeRouter();
    store = new HouseStore();
    store.attach(new PanelRouterAdapter(router));
  });
  afterEach(() => vi.useRealTimers());

  it('spaces requests on the shared serial join', async () => {
    router.connect();
    expect(router.ops()).toEqual(['rooms']);
    await vi.advanceTimersByTimeAsync(REQUEST_GAP_MS);
    expect(router.ops()).toEqual(['rooms', 'scenes']);
    await vi.advanceTimersByTimeAsync(REQUEST_GAP_MS * 3);
    expect(router.ops()).toEqual(['rooms', 'scenes', 'site', 'outdoor', 'audio_sessions']);
  });

  it('keeps only the latest queued level for the same device', async () => {
    router.connect();
    router.doc(J.state, 0, 'rooms', ROOMS);
    store.focusRoom('13');
    router.doc(J.state, 13, 'd', DEVICES);
    await vi.advanceTimersByTimeAsync(REQUEST_GAP_MS * 20);
    router.clear();
    for (const value of [10, 20, 30, 40]) store.send({ kind: 'light', id: '437', action: 'level', value });
    await vi.advanceTimersByTimeAsync(REQUEST_GAP_MS * 10);
    const levels = router.requests.filter((r) => r.op === 'device').map((r) => r.value);
    expect(levels).toEqual([10, 40]);
  });

  it('re-requests startup documents that never arrived', async () => {
    router.connect();
    await vi.advanceTimersByTimeAsync(REQUEST_GAP_MS * 10);
    router.doc(J.state, 0, 'rooms', ROOMS);
    await vi.advanceTimersByTimeAsync(REQUEST_GAP_MS * 10);
    router.clear();
    await vi.advanceTimersByTimeAsync(15000);
    // rooms arrived; scenes and site were lost and are asked for again.
    expect(router.ops()).toEqual(['scenes', 'site', 'outdoor']);
    router.clear();
    await vi.advanceTimersByTimeAsync(15000 * 5);
    expect(router.ops().filter((op) => op === 'scenes')).toHaveLength(1); // two retries in total
  });
});

describe('panel router location name', () => {
  it('names the location from the coordinates when the controller has no name, without flicker on refresh', async () => {
    router = new FakeRouter();
    store = new HouseStore();
    const looked: Array<[number, number]> = [];
    store.attach(new PanelRouterAdapter(router, undefined, 0, async (lat, lon) => { looked.push([lat, lon]); return 'Sample Town, ST'; }));
    router.connect();
    router.doc(J.state, 0, 'site', { room_id: 0, latitude: 40.25, longitude: -95.5, weather_status: 'unavailable' });
    await tick();
    expect(store.config.location).toEqual({ name: 'Sample Town, ST', latitude: 40.25, longitude: -95.5 });
    router.doc(J.state, 0, 'site', { room_id: 0, latitude: 40.25, longitude: -95.5, weather_status: 'unavailable' });
    expect(store.config.location?.name).toBe('Sample Town, ST');
    expect(looked).toHaveLength(1);
  });

  it('prefers a name reported by the controller', async () => {
    router = new FakeRouter();
    store = new HouseStore();
    let calls = 0;
    store.attach(new PanelRouterAdapter(router, undefined, 0, async () => { calls++; return 'Lookup'; }));
    router.connect();
    router.doc(J.state, 0, 'site', { room_id: 0, name: 'Driver City', latitude: 40.25, longitude: -95.5, weather_status: 'unavailable' });
    await tick();
    expect(store.config.location?.name).toBe('Driver City');
    expect(calls).toBe(0);
  });
});
