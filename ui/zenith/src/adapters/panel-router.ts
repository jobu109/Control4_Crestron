import type { Adapter, HouseStore } from '../model/store';
import type {
  AudioZone, Command, HouseConfig, Light, MediaAction, MediaCatalog, RoomMediaState, Room, Scene,
  ServiceId, Shade, Source, ZoneAction
} from '../model/types';
import type { CrComLibLike } from './crestron';
import { FrameCollector, type WireFrame } from './wire';

// Talks to the hardware-verified Control4 - Panel Room Router (one per panel)
// through its existing joins. Nothing on the processor changes for this
// adapter; see docs/PANEL_ROUTER_ADAPTER.md for the protocol it uses.
//
// The router keeps one active room session per panel. Rooms load on demand:
// when a page focuses a room the adapter selects it, and that room's devices,
// sources, catalog, now playing and keys arrive. Other rooms show no live
// state until they are focused.

export const ROUTER_JOINS = {
  request: '101',
  state: '102',
  audio: '103',
  video: '104',
  catalog: '105',
  playing: '106',
  progress: '107',
  keys: '108',
  connected: '7'
} as const;

/** Key order of the compact `keys1` capability string (GatewayCore.Sr260Keys). */
export const SR260_KEYS = [
  'Room_Off', 'Watch', 'Control4', 'Listen', 'List', 'Custom_1', 'Custom_2', 'Custom_3',
  'Guide', 'Page_Up', 'Page_Down', 'Previous', 'Volume_Up', 'Volume_Down', 'Channel_Up', 'Channel_Down',
  'Up', 'Down', 'Left', 'Right', 'Select', 'Mute', 'Info', 'Menu', 'Back', 'DVR', 'Rewind', 'Fast_Forward',
  'Skip_Back', 'Play', 'Skip_Forward', 'Record', 'Pause', 'Stop', 'Red', 'Green', 'Yellow', 'Blue',
  'Digit_1', 'Digit_2', 'Digit_3', 'Digit_4', 'Digit_5', 'Digit_6', 'Digit_7', 'Digit_8', 'Digit_9', 'Star', 'Digit_0', 'Pound'
];

/** Room media actions that map to a remote key and therefore need an advertised capability. */
const KEY_ACTIONS: Partial<Record<MediaAction, string>> = {
  play: 'Play',
  pause: 'Pause',
  next: 'Skip_Forward',
  previous: 'Skip_Back',
  'volume-up': 'Volume_Up',
  'volume-down': 'Volume_Down'
};

/** The router has no floor data yet, so every scoped room is listed under one group. */
export const PANEL_FLOOR = { id: 'panel', name: 'Rooms', order: 1 };

const ZONE_STEP = 5;

interface Device { id: number; name: string; shade: boolean }
interface RoomFeedback { [field: string]: number }
interface NowPlaying { valid: boolean; title?: string; artist?: string; album?: string; artwork?: string; station?: string; playback_action?: string }
interface Catalog { kind: string; valid: boolean; revision: string; items: Array<{ id: string; name: string }>; selection_supported: boolean; source_id?: number }
interface SessionRoom { id: number; name?: string; volume?: number; muted?: boolean }
interface AudioSession { queue_id: number; room_ids: number[]; owner_room_id?: number; source_item_id?: number; state?: string }
interface AudioSessions { available: boolean; revision: string; rooms: SessionRoom[]; sessions: AudioSession[] }

/** Everything the router reports for the one selected room. */
class ActiveRoom {
  devices = new Map<number, Device>();
  feedback: RoomFeedback = {};
  listen: Source[] | null = null;
  watch: Source[] | null = null;
  keys = new Set<string>();
  nowPlaying: NowPlaying | null = null;
  progress: { position: number; duration: number } | null = null;
  catalog: Catalog | null = null;
  opened = '';
  readonly wire: Record<'d' | 'a' | 'v' | 'm' | 'n', FrameCollector<Record<string, unknown>>>;

  constructor(readonly id: number) {
    this.wire = {
      d: new FrameCollector(id, 'd'),
      a: new FrameCollector(id, 'a'),
      v: new FrameCollector(id, 'v'),
      m: new FrameCollector(id, 'm'),
      n: new FrameCollector(id, 'n')
    };
  }
}

type Doc = Record<string, unknown>;
interface HouseCollectors { rooms: FrameCollector<Doc>; scenes: FrameCollector<Doc>; audio_sessions: FrameCollector<Doc> }

const PAGE_OPS: Record<string, string> = {
  d: 'device_page', a: 'audio_page', v: 'video_page', m: 'media_page', n: 'playing_page',
  rooms: 'rooms_page', scenes: 'scenes_page', audio_sessions: 'audio_sessions_page'
};

export class PanelRouterAdapter implements Adapter {
  readonly name = 'panel-router';
  private store?: HouseStore;
  private subs: Array<['b' | 'n' | 's', string, string]> = [];
  private online = false;
  private sequence = 0;
  private wanted = '';
  private view = '';
  private active: ActiveRoom | null = null;
  private scope: Array<{ id: number; name: string }> = [];
  private house: HouseCollectors = houseCollectors();
  private sceneRevision = '';
  private scenes: Scene[] = [];
  private sessions: AudioSessions | null = null;
  private sessionsOpen = false;

  constructor(private lib: CrComLibLike, private joins = ROUTER_JOINS) {}

  connect(store: HouseStore): void {
    this.store = store;
    store.setConnection('connecting');
    this.sub('b', this.joins.connected, (value: boolean) => this.onConnected(Boolean(value)));
    this.sub('s', this.joins.state, (raw: string) => this.onState(raw));
    this.sub('s', this.joins.audio, (raw: string) => this.onRoomFrame(raw, 'a'));
    this.sub('s', this.joins.video, (raw: string) => this.onRoomFrame(raw, 'v'));
    this.sub('s', this.joins.catalog, (raw: string) => this.onRoomFrame(raw, 'm'));
    this.sub('s', this.joins.playing, (raw: string) => this.onRoomFrame(raw, 'n'));
    this.sub('s', this.joins.progress, (raw: string) => this.onProgress(raw));
    this.sub('s', this.joins.keys, (raw: string) => this.onKeys(raw));
    if (store.focusedRoom) this.wanted = store.focusedRoom;
    if (store.view) this.view = store.view;
  }

  dispose(): void {
    if (this.sessionsOpen) this.request('audio_sessions_close');
    for (const [type, signal, id] of this.subs) this.lib.unsubscribeState(type, signal, id);
    this.subs = [];
  }

  focusRoom(roomId: string): void {
    this.wanted = roomId;
    this.selectWanted();
  }

  setView(view: string): void {
    this.view = view;
    if (!this.online) return;
    if (view === 'audio') this.openSessions();
    else if (this.sessionsOpen) this.closeSessions();
  }

  // ---- incoming ----

  private onConnected(online: boolean): void {
    const store = this.store;
    if (!store || online === this.online) return;
    this.online = online;
    this.house = houseCollectors();
    if (!online) {
      this.sessionsOpen = false;
      this.leaveActive();
      for (const zone of store.config.audioZones) store.clearState('zone', zone.id);
      store.setConnection('offline');
      return;
    }
    store.setConnection('online');
    this.request('rooms');
    this.request('scenes');
    // Opening the session catalog also tells us whether Whole House Audio is
    // available; it is closed again once read unless the Audio page is open.
    this.openSessions();
    this.selectWanted();
  }

  private onState(raw: string): void {
    const packet = this.parse(raw);
    if (!packet || !this.online) return;
    if (packet.w === 1) {
      const frame = packet as unknown as WireFrame;
      if (frame.r === 0) this.onHouseFrame(frame);
      else if (this.active && frame.r === this.active.id && frame.k === 'd') this.onRoomFrame(frame, 'd');
      return;
    }
    const room = this.active;
    if (!room || packet.r !== room.id) return;
    if (Number.isInteger(packet.u)) {
      this.onDeviceValue(room, packet.u as number, packet.v);
      return;
    }
    let changed = false;
    for (const [key, value] of Object.entries(packet)) {
      if (key === 'r' || typeof value !== 'number') continue;
      if (room.feedback[key] !== value) { room.feedback[key] = value; changed = true; }
    }
    if (changed) this.publishMedia(room);
  }

  private onHouseFrame(frame: WireFrame): void {
    const collector = this.house[frame.k as keyof HouseCollectors];
    if (!collector) return;
    const doc = this.collect(collector, frame, PAGE_OPS[frame.k], false);
    if (!doc) return;
    if (frame.k === 'rooms') this.onRooms(doc);
    else if (frame.k === 'scenes') this.onScenes(doc);
    else if (frame.k === 'audio_sessions') this.onSessions(doc);
  }

  private onRoomFrame(raw: string | WireFrame, kind: 'd' | 'a' | 'v' | 'm' | 'n'): void {
    const room = this.active;
    if (!room || !this.online) return;
    const frame = typeof raw === 'string' ? (this.parse(raw) as unknown as WireFrame | null) : raw;
    if (!frame || frame.w !== 1 || frame.r !== room.id) return;
    const doc = this.collect(room.wire[kind], frame, PAGE_OPS[kind], true);
    if (!doc || this.active !== room) return;
    if (kind === 'd') this.onDevices(room, doc);
    else if (kind === 'a' || kind === 'v') this.onSources(room, kind, doc);
    else if (kind === 'm') this.onCatalog(room, doc);
    else this.onNowPlaying(room, doc);
  }

  private onRooms(doc: Record<string, unknown>): void {
    const store = this.store!;
    const seen = new Set<number>();
    const rooms = Array.isArray(doc.rooms) ? doc.rooms : [];
    this.scope = rooms
      .filter((r): r is { id: number; name: string } => !!r && validId(r.id) && typeof r.name === 'string' && r.name.trim() !== '')
      .filter((r) => !seen.has(r.id) && !!seen.add(r.id))
      .map((r) => ({ id: r.id, name: r.name.trim() }));
    const previous = new Map(store.config.rooms.map((r) => [r.id, r]));
    const config: HouseConfig = {
      ...store.config,
      floors: this.scope.length ? [PANEL_FLOOR] : [],
      rooms: this.scope.map(({ id, name }): Room => {
        const key = String(id);
        const old = previous.get(key);
        return old ? { ...old, name } : {
          id: key, name, floorId: PANEL_FLOOR.id, lights: [], shades: [], videoSources: [], audioSources: [],
          lazy: true, lightActions: ['on', 'off']
        };
      })
    };
    store.setConfig(this.withServices(config));
    this.selectWanted();
  }

  private onScenes(doc: Record<string, unknown>): void {
    if (typeof doc.revision !== 'string' || !Array.isArray(doc.actions)) return;
    const revision = doc.revision;
    this.sceneRevision = revision;
    this.scenes = doc.actions
      .filter((a): a is { slot: number; name: string; kind?: string } => !!a && Number.isInteger(a.slot) && a.slot > 0 && typeof a.name === 'string')
      .map((a) => ({ id: `${revision}:${a.slot}`, name: a.name, description: typeof a.kind === 'string' ? humanize(a.kind) : undefined }));
    const store = this.store!;
    store.setConfig(this.withServices({ ...store.config, scenes: this.scenes }));
  }

  private onSessions(doc: Record<string, unknown>): void {
    const store = this.store!;
    if (doc.available !== true || typeof doc.revision !== 'string') {
      this.sessions = null;
      for (const zone of store.config.audioZones) store.clearState('zone', zone.id);
      store.setConfig(this.withServices({ ...store.config, audioZones: [] }));
    } else {
      const sessions: AudioSessions = {
        available: true,
        revision: doc.revision,
        rooms: (Array.isArray(doc.rooms) ? doc.rooms : []).filter((r): r is SessionRoom => !!r && validId(r.id)),
        sessions: (Array.isArray(doc.sessions) ? doc.sessions : []).filter((s): s is AudioSession =>
          !!s && Number.isInteger(s.queue_id) && Array.isArray(s.room_ids))
      };
      this.sessions = sessions;
      const names = new Map(this.scope.map((r) => [r.id, r.name]));
      const zones: AudioZone[] = sessions.rooms.map((r) => ({
        id: String(r.id),
        name: (typeof r.name === 'string' && r.name) || names.get(r.id) || `Room ${r.id}`,
        roomId: String(r.id)
      }));
      store.setConfig(this.withServices({ ...store.config, audioZones: zones }));
      for (const r of sessions.rooms) store.putState('zone', String(r.id), this.zoneState(sessions, r));
    }
    // Probe complete: stop background refresh unless the Audio page is open.
    if (this.view !== 'audio' && this.sessionsOpen) this.closeSessions();
  }

  private onDevices(room: ActiveRoom, doc: Record<string, unknown>): void {
    if (doc.room_id !== room.id || !Array.isArray(doc.devices)) return;
    room.devices.clear();
    const lights: Light[] = [];
    const shades: Shade[] = [];
    for (const d of doc.devices) {
      if (!d || !validId(d.id) || typeof d.name !== 'string' || typeof d.shade !== 'boolean') continue;
      room.devices.set(d.id, { id: d.id, name: d.name, shade: d.shade });
      // The router only accepts level commands for lights, so every light gets a level slider.
      if (d.shade) shades.push({ id: String(d.id), name: d.name, positionable: true });
      else lights.push({ id: String(d.id), name: d.name, dimmable: true });
      this.onDeviceValue(room, d.id, d.value);
    }
    this.store!.updateRoom(String(room.id), { lights, shades });
  }

  private onDeviceValue(room: ActiveRoom, id: number, value: unknown): void {
    const device = room.devices.get(id);
    if (!device) return;
    const store = this.store!;
    const key = String(id);
    const kind = device.shade ? 'shade' : 'light';
    if (!Number.isInteger(value) || (value as number) < 0 || (value as number) > 100) {
      store.clearState(kind, key);
      return;
    }
    const level = value as number;
    if (device.shade) store.putState('shade', key, { position: level });
    else store.putState('light', key, { on: level > 0, level });
  }

  private onSources(room: ActiveRoom, kind: 'a' | 'v', doc: Record<string, unknown>): void {
    if (doc.room_id !== room.id) return;
    const video = kind === 'v';
    const items = doc.valid === true && Array.isArray(doc.items) ? doc.items : [];
    const sources: Source[] = items
      .filter((s): s is { id: string; name: string } => !!s && typeof s.id === 'string' && s.id !== '' && typeof s.name === 'string')
      .map((s) => ({ id: s.id, name: s.name, kind: video ? 'video' : 'audio' }));
    if (video) room.watch = sources; else room.listen = sources;
    const loaded = room.listen !== null && room.watch !== null;
    this.store!.updateRoom(String(room.id), video ? { videoSources: sources, lazy: !loaded } : { audioSources: sources, lazy: !loaded });
    this.publishMedia(room);
  }

  private onCatalog(room: ActiveRoom, doc: Record<string, unknown>): void {
    if (doc.room_id !== room.id || typeof doc.kind !== 'string') return;
    const items = Array.isArray(doc.items) ? doc.items.filter((i): i is { id: string; name: string } =>
      !!i && typeof i.id === 'string' && typeof i.name === 'string') : [];
    room.catalog = {
      kind: doc.kind,
      valid: doc.valid === true,
      revision: typeof doc.revision === 'string' ? doc.revision : '',
      items,
      selection_supported: doc.selection_supported === true,
      source_id: Number.isInteger(doc.source_id) ? (doc.source_id as number) : undefined
    };
    this.publishMedia(room);
  }

  private onNowPlaying(room: ActiveRoom, doc: Record<string, unknown>): void {
    if (doc.room_id !== room.id || typeof doc.valid !== 'boolean') return;
    room.nowPlaying = doc as unknown as NowPlaying;
    this.publishMedia(room);
  }

  private onProgress(raw: string): void {
    const room = this.active;
    const p = this.parse(raw);
    if (!room || !p || !this.online || p.room_id !== room.id) return;
    const position = p.position_seconds as number;
    const duration = p.duration_seconds as number;
    room.progress = p.valid === true && Number.isInteger(position) && Number.isInteger(duration) &&
      duration > 0 && duration <= 172800 && position >= 0 && position <= duration ? { position, duration } : null;
    this.publishMedia(room);
  }

  private onKeys(raw: string): void {
    const room = this.active;
    const p = this.parse(raw);
    if (!room || !p || !this.online || p.room_id !== room.id) return;
    const keys = new Set<string>();
    if (p.wire === 'keys1') {
      const states = p.states;
      if (typeof states !== 'string' || states.length !== SR260_KEYS.length || !/^[alu]+$/.test(states)) return;
      SR260_KEYS.forEach((key, i) => { if (states[i] !== 'u') keys.add(key); });
    } else if (Array.isArray(p.keys)) {
      for (const k of p.keys) if (k && typeof k.key === 'string' && (k.state === 'advertised' || k.state === 'local_ui')) keys.add(k.key);
    } else return;
    room.keys = keys;
    this.publishMedia(room);
  }

  // ---- derived state ----

  /** Rebuild the selected room's media state from what the router has reported. */
  private publishMedia(room: ActiveRoom): void {
    if (this.active !== room) return;
    const f = room.feedback;
    const state: Partial<RoomMediaState> = {};
    if (f.power !== undefined) state.power = f.power === 1;
    if (f.volumeValid === 1 && Number.isInteger(f.volume)) state.volume = f.volume;
    if (f.muteValid === 1 && f.muted !== undefined) state.mute = f.muted === 1;
    const video = pick(room.watch, f.videoValid, f.videoIndex);
    const audio = pick(room.listen, f.audioValid, f.audioIndex);
    if (video || audio) state.activeSourceId = (video ?? audio)!.id;
    const np = f.nowPlayingValid === 1 && room.nowPlaying?.valid ? room.nowPlaying : null;
    if (np) {
      if (np.title) state.title = np.title;
      if (np.artist) state.artist = np.artist;
      if (np.album) state.album = np.album;
      if (np.artwork) state.artUrl = np.artwork;
      if (np.station) state.station = np.station;
      if (np.playback_action === 'play' || np.playback_action === 'pause') state.playbackAction = np.playback_action;
    }
    if (room.progress) state.progress = room.progress;
    const actions: MediaAction[] = ['off', 'mute', 'unmute'];
    for (const [action, key] of Object.entries(KEY_ACTIONS)) if (room.keys.has(key!)) actions.push(action as MediaAction);
    state.actions = actions;
    const catalog = this.catalogState(room);
    if (catalog) state.catalog = catalog;
    this.store!.putState('media', String(room.id), state);
  }

  private catalogState(room: ActiveRoom): MediaCatalog | undefined {
    const c = room.catalog;
    if (!c || c.kind !== 'service_presets') return undefined;
    if (!c.valid) return room.opened ? { sourceId: room.opened, items: [], selectable: false, pending: true } : undefined;
    return {
      sourceId: c.source_id !== undefined ? String(c.source_id) : room.opened,
      items: c.items,
      selectable: c.selection_supported && room.feedback.selectReady === 1,
      pending: false
    };
  }

  private zoneState(doc: AudioSessions, r: SessionRoom) {
    const session = doc.sessions.find((s) => s.room_ids.includes(r.id));
    const actions: ZoneAction[] = [];
    const state: { power: boolean; volume?: number; mute?: boolean; actions: ZoneAction[] } = { power: !!session, actions };
    if (session) {
      if (Number.isInteger(r.volume)) { state.volume = r.volume; actions.push('volume', 'volume-up', 'volume-down'); }
      if (typeof r.muted === 'boolean') state.mute = r.muted;
      actions.push('mute', 'unmute');
      // The router refuses to remove the queue owner.
      if (session.owner_room_id !== undefined && session.owner_room_id !== r.id && this.inScope(session.owner_room_id)) actions.push('off');
    } else {
      const target = this.joinableSession(doc);
      if (target) actions.push('on');
    }
    return state;
  }

  /** "On" joins the only active session in scope. With none or several it is ambiguous, so it stays unavailable. */
  private joinableSession(doc: AudioSessions): AudioSession | undefined {
    const candidates = doc.sessions.filter((s) => s.owner_room_id !== undefined && this.inScope(s.owner_room_id));
    return candidates.length === 1 ? candidates[0] : undefined;
  }

  private withServices(config: HouseConfig): HouseConfig {
    // Lighting and Shades overview pages need whole-house device data, which the
    // router does not provide. Room pages still show their own lights and shades.
    const services: ServiceId[] = [];
    if (config.rooms.length) services.push('media');
    if (config.audioZones.length) services.push('audio');
    if (config.scenes.length) services.push('scenes');
    return { ...config, services };
  }

  // ---- outgoing ----

  send(c: Command): void {
    if (!this.online) return;
    switch (c.kind) {
      case 'scene': return this.runScene(c.id);
      case 'zone': return this.zoneCommand(c.id, c.action, 'value' in c ? c.value : undefined);
    }
    const room = this.active;
    if (!room) return;
    switch (c.kind) {
      case 'light': {
        const id = Number(c.id);
        const device = room.devices.get(id);
        if (!device || device.shade) return;
        let level: number;
        if (c.action === 'level') level = c.value;
        else if (c.action === 'toggle') level = this.store!.get('light', c.id)?.on ? 0 : 100;
        else level = c.action === 'on' ? 100 : 0;
        return this.device(room, id, 'level', clamp(level));
      }
      case 'room-lights':
        if (String(room.id) !== c.id || c.action === 'ambient') return;
        for (const d of room.devices.values()) if (!d.shade) this.device(room, d.id, 'level', c.action === 'on' ? 100 : 0);
        return;
      case 'shade': {
        const id = Number(c.id);
        if (!room.devices.get(id)?.shade) return;
        if (c.action === 'position') return this.device(room, id, 'level', clamp(c.value));
        return this.device(room, id, c.action);
      }
      case 'room-shades':
        if (String(room.id) !== c.id) return;
        for (const d of room.devices.values()) if (d.shade) this.device(room, d.id, c.action);
        return;
      case 'media':
        if (String(room.id) !== c.id) return;
        return this.mediaCommand(room, c);
    }
  }

  private mediaCommand(room: ActiveRoom, c: Extract<Command, { kind: 'media' }>): void {
    const r = { room_id: room.id };
    switch (c.action) {
      case 'source': {
        if (room.listen?.some((s) => s.id === c.value)) {
          room.opened = c.value;
          this.request('audio', { ...r, value: c.value });
        } else if (room.watch?.some((s) => s.id === c.value)) this.request('video', { ...r, value: c.value });
        return;
      }
      case 'item': {
        const catalog = room.catalog;
        if (!catalog?.valid || !catalog.selection_supported || room.feedback.selectReady !== 1 ||
            !catalog.items.some((i) => i.id === c.value)) return;
        this.request('item', { ...r, kind: catalog.kind, revision: catalog.revision, id: c.value });
        return;
      }
      case 'volume': return this.request('volume', { ...r, value: clamp(c.value) });
      case 'mute': return this.request('mute_on', r);
      case 'unmute': return this.request('mute_off', r);
      case 'off': return this.request('off', r);
      default: {
        const key = KEY_ACTIONS[c.action];
        if (key && room.keys.has(key)) this.request('key', { ...r, value: key });
      }
    }
  }

  private runScene(id: string): void {
    const [revision, slot] = id.split(':');
    if (!revision || revision !== this.sceneRevision || !this.scenes.some((s) => s.id === id)) return;
    this.request('scene_activate', { revision, slot: Number(slot) });
  }

  private zoneCommand(id: string, action: string, value: unknown): void {
    const doc = this.sessions;
    const roomId = Number(id);
    const r = doc?.rooms.find((x) => x.id === roomId);
    if (!doc || !r) return;
    const allowed = this.zoneState(doc, r).actions;
    const session = doc.sessions.find((s) => s.room_ids.includes(roomId));
    const base = { revision: doc.revision, room_id: roomId };
    switch (action) {
      case 'volume':
      case 'volume-up':
      case 'volume-down': {
        if (!session || !allowed.includes('volume')) return;
        const target = action === 'volume' ? Number(value) : (r.volume ?? 0) + (action === 'volume-up' ? ZONE_STEP : -ZONE_STEP);
        if (!Number.isFinite(target)) return;
        return this.request('audio_session_volume', { ...base, queue_id: session.queue_id, value: clamp(target) });
      }
      case 'mute':
      case 'unmute':
        if (!session) return;
        return this.request('audio_session_mute', { ...base, queue_id: session.queue_id, value: action === 'mute' ? 1 : 0 });
      case 'off':
        if (!session || !allowed.includes('off')) return;
        return this.request('audio_session_remove', { ...base, queue_id: session.queue_id });
      case 'on': {
        const target = allowed.includes('on') ? this.joinableSession(doc) : undefined;
        if (target) this.request('audio_session_add', { ...base, queue_id: target.queue_id });
      }
    }
  }

  private device(room: ActiveRoom, id: number, action: string, value?: number): void {
    this.request('device', value === undefined ? { room_id: room.id, device_id: id, action } : { room_id: room.id, device_id: id, action, value });
  }

  // ---- helpers ----

  private selectWanted(): void {
    if (!this.online || !this.wanted) return;
    const id = Number(this.wanted);
    if (!this.inScope(id) || this.active?.id === id) return;
    this.leaveActive();
    this.active = new ActiveRoom(id);
    this.request('room', { room_id: id });
  }

  /** Drop the previous room's live state: the router stops reporting it once another room is selected. */
  private leaveActive(): void {
    const room = this.active;
    const store = this.store;
    this.active = null;
    if (!room || !store) return;
    for (const d of room.devices.values()) store.clearState(d.shade ? 'shade' : 'light', String(d.id));
    store.clearState('media', String(room.id));
  }

  private openSessions(): void {
    this.sessionsOpen = true;
    this.house.audio_sessions = new FrameCollector(0, 'audio_sessions');
    this.request('audio_sessions');
  }

  private closeSessions(): void {
    this.sessionsOpen = false;
    this.request('audio_sessions_close');
  }

  private inScope(id: number): boolean {
    return this.scope.some((r) => r.id === id);
  }

  /** Feed one frame; ask for a missing page; return the document when complete and valid. */
  private collect(collector: FrameCollector<Record<string, unknown>>, frame: WireFrame, pageOp: string, scoped: boolean): Record<string, unknown> | null {
    try {
      const result = collector.accept(frame);
      if (result.status === 'partial') {
        if (result.requestPage) this.request(pageOp, scoped ? { room_id: frame.r, value: result.requestPage } : { value: result.requestPage });
        return null;
      }
      if (result.status !== 'complete') return null;
      const doc = result.document;
      if (!doc || typeof doc !== 'object' || doc.room_id !== frame.r || 'wire_error' in doc) return null;
      return doc;
    } catch (error) {
      console.warn('Panel Room Router frame rejected', error);
      return null;
    }
  }

  private request(op: string, fields: Record<string, unknown> = {}): void {
    if (!this.online) return;
    const json = JSON.stringify({ ...fields, op, q: ++this.sequence });
    if (json.length > 255) {
      console.warn('Panel Room Router request over 255 characters, dropped', op);
      return;
    }
    this.lib.publishEvent('s', this.joins.request, json);
  }

  private parse(raw: string): Record<string, unknown> | null {
    if (!raw) return null;
    try {
      const value = JSON.parse(raw);
      return value && typeof value === 'object' ? value : null;
    } catch {
      return null;
    }
  }

  private sub(type: 'b' | 'n' | 's', signal: string, cb: (value: never) => void): void {
    this.subs.push([type, signal, this.lib.subscribeState(type, signal, cb)]);
  }
}

function houseCollectors(): HouseCollectors {
  return {
    rooms: new FrameCollector<Doc>(0, 'rooms'),
    scenes: new FrameCollector<Doc>(0, 'scenes'),
    audio_sessions: new FrameCollector<Doc>(0, 'audio_sessions')
  };
}

function validId(id: unknown): id is number {
  return Number.isInteger(id) && (id as number) > 0 && (id as number) <= 65535;
}

function pick(list: Source[] | null, valid: number | undefined, index: number | undefined): Source | undefined {
  if (!list || valid !== 1 || !index || index < 1) return undefined;
  return list[index - 1];
}

function clamp(value: number): number {
  return Math.max(0, Math.min(100, Math.round(value)));
}

function humanize(kind: string): string {
  const text = kind.replace(/[_-]+/g, ' ').trim();
  return text ? text.charAt(0).toUpperCase() + text.slice(1) : '';
}
