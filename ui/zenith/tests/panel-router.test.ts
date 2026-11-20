import { beforeEach, describe, expect, it } from 'vitest';
import { encodeFrames } from '../src/adapters/wire';
import { HouseStore } from '../src/model/store';
import { PANEL_FLOOR, PanelRouterAdapter, ROUTER_JOINS as J, SR260_KEYS } from '../src/adapters/panel-router';
import type { CrComLibLike } from '../src/adapters/crestron';

// Simulates the Control4 - Panel Room Router joins. Frames use the same
// w:1 / base64 / 160-character envelope as Control4Gateway UiSerialFrames.cs.

/** Page request op -> stream kind, as handled by PanelRoomModule.Request. */
const PAGE_KIND: Record<string, string> = {
  device_page: 'd', audio_page: 'a', video_page: 'v', media_page: 'm', playing_page: 'n',
  rooms_page: 'rooms', scenes_page: 'scenes', audio_sessions_page: 'audio_sessions'
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
  store.attach(new PanelRouterAdapter(router));
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
    expect(router.ops()).toEqual(['rooms', 'scenes', 'audio_sessions']);
    expect(router.requests.map((r) => r.q)).toEqual([1, 2, 3]);
    router.doc(J.state, 0, 'rooms', ROOMS);
    expect(store.config.floors).toEqual([PANEL_FLOOR]);
    expect(store.config.rooms.map((r) => [r.id, r.name, r.lazy])).toEqual([['13', 'Living Room', true], ['212', 'Kitchen', true]]);
    expect(store.config.services).toEqual(['media']);
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
      actions: ['off', 'mute', 'unmute', 'play', 'pause', 'next']
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

  it('publishes scenes and runs them only with the current revision', () => {
    router.connect();
    router.doc(J.state, 0, 'rooms', ROOMS);
    router.doc(J.state, 0, 'scenes', { room_id: 0, revision: 'aaaa', actions: [{ slot: 1, kind: 'advanced_lighting', name: 'House Off' }, { slot: 2, kind: 'macros', name: 'House Off' }] });
    expect(store.config.services).toEqual(['media', 'scenes']);
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
    expect(store.get('zone', '13')).toEqual({ power: true, volume: 29, mute: false, actions: ['volume', 'volume-up', 'volume-down', 'mute', 'unmute'] });
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
    expect(router.requests.map(({ q: _q, ...r }) => r)).toEqual([
      { revision: 'rev1', room_id: 13, queue_id: 7, value: 34, op: 'audio_session_volume' },
      { revision: 'rev1', room_id: 212, queue_id: 7, value: 0, op: 'audio_session_mute' },
      { revision: 'rev1', room_id: 212, queue_id: 7, op: 'audio_session_remove' },
      { revision: 'rev1', room_id: 89, queue_id: 7, op: 'audio_session_add' }
    ]);
    router.clear();
    store.setView('home');
    expect(router.ops()).toEqual(['audio_sessions_close']);
  });

  it('marks zones unavailable when Whole House Audio is not available', () => {
    router.connect();
    router.doc(J.state, 0, 'rooms', ROOMS);
    router.doc(J.state, 0, 'audio_sessions', { room_id: 0, available: false });
    expect(store.config.audioZones).toEqual([]);
    expect(store.config.services).toEqual(['media']);
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
    expect(router.ops()).toEqual(['rooms', 'scenes', 'audio_sessions', 'room']);
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
