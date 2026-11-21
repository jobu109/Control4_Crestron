import { lookupPlace } from '../model/places';
import type { Adapter, HouseStore } from '../model/store';
import type {
  AudioZone, Command, HouseConfig, Light, MediaAction, MediaCatalog, RoomMediaState, Room, Scene,
  ServiceId, Shade, SiteLocation, Source, WeatherState, ZoneAction, ZoneTrack
} from '../model/types';
import { HOUSE_WEATHER } from '../model/types';
import { validCoordinates } from '../model/sun';
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
interface Catalog { kind: string; valid: boolean; revision: string; items: Array<{ id: string; name: string }>; selection_supported: boolean; source_id?: number; shuffle?: boolean }
interface SessionRoom { id: number; name?: string; volume?: number; muted?: boolean }
interface AudioSession {
  queue_id: number; room_ids: number[]; owner_room_id?: number; source_item_id?: number; state?: string;
  now_playing?: Record<string, unknown>;
}
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
interface HouseCollectors {
  rooms: FrameCollector<Doc>; scenes: FrameCollector<Doc>; audio_sessions: FrameCollector<Doc>; site: FrameCollector<Doc>;
  house_devices: FrameCollector<Doc>;
}

const PAGE_OPS: Record<string, string> = {
  d: 'device_page', a: 'audio_page', v: 'video_page', m: 'media_page', n: 'playing_page',
  rooms: 'rooms_page', scenes: 'scenes_page', audio_sessions: 'audio_sessions_page', site: 'site_page',
  house_devices: 'house_devices_page'
};

/** Director room discovery can finish after Connected_FB; retry an empty whole-house catalog a few times. */
const HOUSE_RETRY_MS = 10000;
const HOUSE_RETRIES = 6;

/**
 * Requests share one serial join (Request$). The router's SIMPL+ CHANGE handler only sees the
 * latest value, so requests sent in the same instant can be lost; space them out.
 */
export const REQUEST_GAP_MS = 60;

/** `?debug=1` logs every router join in the browser console (Web XPanel: F12). */
const ROUTER_DEBUG = typeof location !== 'undefined' && /[?&]debug=1/.test(location.search);

/** Re-request a startup document (rooms, scenes, site) that has not arrived after this long. */
const INITIAL_RETRY_MS = 15000;
const INITIAL_RETRIES = 2;
const INITIAL_DOCS = ['rooms', 'scenes', 'site'] as const;

/** Location and weather are re-read this often; weather drivers update on a similar cadence. */
export const SITE_REFRESH_MS = 15 * 60 * 1000;

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
  private siteTimer: ReturnType<typeof setInterval> | undefined;
  /** Whole-house lights and shades of every scoped room: device id -> owning room and kind. */
  private houseDevices = new Map<number, Device & { room: number }>();
  private houseTries = 0;
  private houseTimer: ReturnType<typeof setTimeout> | undefined;
  private houseRequested = false;
  private outbox: Array<{ key: string; json: string }> = [];
  private outboxTimer: ReturnType<typeof setTimeout> | undefined;
  private received = new Set<string>();
  private initialTries = 0;
  private initialTimer: ReturnType<typeof setTimeout> | undefined;
  /** Last nearest-town lookup, so the 15-minute site refresh does not flicker the name. */
  private place: { lat: number; lon: number; label?: string } | undefined;

  constructor(
    private lib: CrComLibLike,
    private joins = ROUTER_JOINS,
    private requestGapMs = REQUEST_GAP_MS,
    private placeLookup: (lat: number, lon: number) => Promise<string | undefined> =
      (lat, lon) => lookupPlace(lat, lon).then((p) => p?.label)
  ) {}

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
    clearInterval(this.siteTimer);
    clearTimeout(this.houseTimer);
    clearTimeout(this.initialTimer);
    if (this.online) this.request('house_devices_close');
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
    this.relayUpdates.clear();
    this.experienceUpdates.clear();
    this.countdownUpdates.clear();
    this.fanUpdates.clear();
    this.received.clear();
    this.houseRequested = false;
    this.outbox = [];
    clearTimeout(this.outboxTimer);
    this.outboxTimer = undefined;
    clearTimeout(this.initialTimer);
    this.initialTries = 0;
    if (!online) {
      this.sessionsOpen = false;
      clearInterval(this.siteTimer);
      this.siteTimer = undefined;
      clearTimeout(this.houseTimer);
      this.leaveActive();
      for (const d of this.houseDevices.values()) store.clearState(d.shade ? 'shade' : 'light', String(d.id));
      this.houseDevices.clear();
      for (const zone of store.config.audioZones) store.clearState('zone', zone.id);
      store.clearState('weather', HOUSE_WEATHER);
      store.setConfig({ ...store.config, rooms: store.config.rooms.map((room) => ({ ...room, relays: undefined, experienceButtons: undefined, fans: undefined })) });
      store.setConnection('offline');
      return;
    }
    store.setConnection('online');
    this.request('rooms');
    this.request('scenes');
    this.request('site');
    this.houseTries = 0;
    // house_devices follows the room list (onRooms): it is the heaviest request and its level
    // updates share State$ with the startup documents.
    this.scheduleInitialCheck();
    clearInterval(this.siteTimer);
    this.siteTimer = setInterval(() => {
      this.house.site = new FrameCollector<Doc>(0, 'site');
      this.request('site');
    }, SITE_REFRESH_MS);
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
    if (packet.h === 4 && validId(packet.u) && (packet.v === null || validFanSpeed(packet.v))) {
      const id = String(packet.u);
      const speed = validFanSpeed(packet.v) ? packet.v : undefined;
      const store = this.store!;
      this.fanUpdates.set(id, speed);
      store.setConfig({ ...store.config, rooms: store.config.rooms.map((room) =>
        room.fans?.some((fan) => fan.id === id) ? { ...room, fans: room.fans.map((fan) =>
          fan.id === id ? { ...fan, speed } : fan) } : room) });
      return;
    }
    if (packet.h === 5 && validId(packet.u)) {
      const countdown = parseCountdownState(packet.v);
      if (packet.v !== null && !countdown) return;
      const id = String(packet.u);
      const store = this.store!;
      this.countdownUpdates.set(id, countdown);
      store.setConfig({ ...store.config, rooms: store.config.rooms.map((room) =>
        room.experienceButtons?.some((button) => button.id === id && button.kind === 'countdown')
          ? { ...room, experienceButtons: room.experienceButtons.map((button) =>
            button.id === id && button.kind === 'countdown' ? { ...button, countdown } : button) } : room) });
      return;
    }
    if (packet.h === 3 && validId(packet.u) && (typeof packet.v === 'boolean' || packet.v === null)) {
      const id = String(packet.u);
      const on = packet.v === null ? undefined : packet.v;
      const store = this.store!;
      this.experienceUpdates.set(id, on);
      store.setConfig({ ...store.config, rooms: store.config.rooms.map((room) =>
        room.experienceButtons?.some((button) => button.id === id) ? { ...room, experienceButtons: room.experienceButtons.map((button) =>
          button.id === id ? { ...button, on } : button) } : room) });
      return;
    }
    if (packet.h === 2 && validId(packet.u) && typeof packet.v === 'boolean') {
      const id = String(packet.u);
      const closed = packet.v;
      const store = this.store!;
      this.relayUpdates.set(id, closed);
      store.setConfig({ ...store.config, rooms: store.config.rooms.map((room) =>
        room.relays?.some((relay) => relay.id === id) ? { ...room, relays: room.relays.map((relay) =>
          relay.id === id ? { ...relay, closed } : relay) } : room) });
      return;
    }
    if (packet.h === 1 && Number.isInteger(packet.u)) {
      this.onHouseValue(packet.u as number, packet.v);
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
    this.received.add(frame.k);
    if (frame.k === 'rooms') this.onRooms(doc);
    else if (frame.k === 'scenes') this.onScenes(doc);
    else if (frame.k === 'audio_sessions') this.onSessions(doc);
    else if (frame.k === 'site') this.onSite(doc);
    else if (frame.k === 'house_devices') this.onHouseDevices(doc);
  }

  /** Catalog of every scoped room's lights and shades, with current levels. */
  private lastHouseDoc: Doc | null = null;
  private readonly relayUpdates = new Map<string, boolean>();
  private readonly fanUpdates = new Map<string, number | undefined>();
  private readonly countdownUpdates = new Map<string, ReturnType<typeof parseCountdownState>>();
  private readonly experienceUpdates = new Map<string, boolean | undefined>();

  private onHouseDevices(doc: Doc): void {
    const store = this.store!;
    this.lastHouseDoc = doc;
    const rooms = Array.isArray(doc.rooms) ? doc.rooms : [];
    const next = new Map<number, Device & { room: number }>();
    let config = store.config;
    for (const r of rooms) {
      if (!r || !validId(r.id) || !this.inScope(r.id) || !Array.isArray(r.devices)) continue;
      const lights: Light[] = [];
      const shades: Shade[] = [];
      for (const d of r.devices) {
        if (!d || !validId(d.id) || typeof d.name !== 'string' || typeof d.shade !== 'boolean' || next.has(d.id)) continue;
        next.set(d.id, { id: d.id, name: d.name, shade: d.shade, room: r.id });
        if (d.shade) shades.push({ id: String(d.id), name: d.name, positionable: true });
        else lights.push({ id: String(d.id), name: d.name, dimmable: true });
      }
      const relays = (Array.isArray(r.relays) ? r.relays : [])
        .filter((relay: Doc) => relay && validId(relay.id) && typeof relay.name === 'string')
        .map((relay: Doc) => ({ id: String(relay.id), name: relay.name,
          closed: this.relayUpdates.get(String(relay.id)) ?? (typeof relay.closed === 'boolean' ? relay.closed : undefined) }));
      const fans = (Array.isArray(r.fans) ? r.fans : [])
        .filter((fan: Doc) => fan && validId(fan.id) && typeof fan.name === 'string')
        .map((fan: Doc) => {
          const id = String(fan.id);
          const speed = this.fanUpdates.has(id) ? this.fanUpdates.get(id)
            : validFanSpeed(fan.speed) ? fan.speed : undefined;
          return { id, name: fan.name, speed };
        });
      const experienceButtons = (Array.isArray(r.experience_buttons) ? r.experience_buttons : [])
        .filter((button: Doc) => button && validId(button.id) && typeof button.name === 'string')
                .map((button: Doc) => {
          const id = String(button.id);
          if (button.kind === 'countdown') return { id, name: button.name, kind: 'countdown' as const,
            countdown: this.countdownUpdates.has(id) ? this.countdownUpdates.get(id) : parseCountdownState(button.countdown) };
          return { id, name: button.name, on: this.experienceUpdates.has(id) ? this.experienceUpdates.get(id)
            : typeof button.on === 'boolean' ? button.on : undefined };
        });
      config = { ...config, rooms: config.rooms.map((room) => (room.id === String(r.id)
        ? { ...room, lights, shades, relays, experienceButtons, fans } : room)) };
    }
    // Devices that disappeared from the catalog are no longer reported.
    for (const [id, d] of this.houseDevices) if (!next.has(id)) store.clearState(d.shade ? 'shade' : 'light', String(id));
    this.houseDevices = next;
    store.setConfig(this.withServices(config));
    for (const r of rooms) for (const d of (r && Array.isArray(r.devices) ? r.devices : [])) if (d && validId(d.id)) this.onHouseValue(d.id, d.value);
    clearTimeout(this.houseTimer);
    if ((next.size === 0 || doc.relay_ready === false) && this.houseTries < HOUSE_RETRIES) {
      this.houseTries++;
      this.houseTimer = setTimeout(() => {
        this.house.house_devices = new FrameCollector<Doc>(0, 'house_devices');
        this.request('house_devices');
      }, HOUSE_RETRY_MS);
    }
  }

  private onHouseValue(id: number, value: unknown): void {
    const device = this.houseDevices.get(id);
    if (!device) return;
    const store = this.store!;
    const key = String(id);
    if (!Number.isInteger(value) || (value as number) < 0 || (value as number) > 100) {
      store.clearState(device.shade ? 'shade' : 'light', key);
      return;
    }
    const level = value as number;
    if (device.shade) store.putState('shade', key, { position: level });
    else store.putState('light', key, { on: level > 0, level });
  }

  /** Location (coordinates, optional name) and weather from the gateway's site document. */
  private onSite(doc: Doc): void {
    const store = this.store!;
    const location: SiteLocation = {};
    if (typeof doc.name === 'string' && doc.name.trim()) location.name = doc.name.trim();
    if (validCoordinates(doc.latitude, doc.longitude)) {
      location.latitude = doc.latitude as number;
      location.longitude = doc.longitude as number;
    }
    // No name from the controller: use the nearest town to the project coordinates (offline table).
    if (!location.name && location.latitude !== undefined && location.longitude !== undefined) {
      const lat = location.latitude, lon = location.longitude;
      if (this.place && this.place.lat === lat && this.place.lon === lon) {
        if (this.place.label) location.name = this.place.label;
      } else {
        void this.placeLookup(lat, lon).then((label) => {
          this.place = { lat, lon, label };
          const current = this.store?.config.location;
          if (label && current && !current.name && current.latitude === lat && current.longitude === lon)
            this.store!.setConfig({ ...this.store!.config, location: { ...current, name: label } });
        });
      }
    }
    store.setConfig({ ...store.config, location: Object.keys(location).length ? location : undefined });
    const w = doc.weather as Doc | undefined;
    if (doc.weather_status === 'reporting' && w && typeof w.temperature === 'number' && Number.isFinite(w.temperature)) {
      const weather: WeatherState = { temperature: w.temperature };
      if (doc.temperature_unit === 'F' || doc.temperature_unit === 'C') weather.unit = doc.temperature_unit;
      if (typeof w.low === 'number' && typeof w.high === 'number' && w.low <= w.high) { weather.low = w.low; weather.high = w.high; }
      const num = (v: unknown) => (typeof v === 'number' && Number.isFinite(v) ? v : undefined);
      const extra = { humidity: num(w.humidity), windSpeed: num(w.wind_speed), clouds: num(w.clouds), rain: num(w.rain), snow: num(w.snow) };
      for (const [key, value] of Object.entries(extra)) if (value !== undefined) (weather as Record<string, number>)[key] = value;
      store.putState('weather', HOUSE_WEATHER, weather);
    } else {
      store.clearState('weather', HOUSE_WEATHER);
    }
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
    if (!this.houseRequested) {
      this.houseRequested = true;
      this.request('house_devices');
    }
    // The whole-house catalog may have arrived before the room list; apply it to the new rooms.
    if (this.lastHouseDoc) this.onHouseDevices(this.lastHouseDoc);
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
      source_id: Number.isInteger(doc.source_id) ? (doc.source_id as number) : undefined,
      shuffle: doc.pandora_shuffle === 'On' ? true : doc.pandora_shuffle === 'Off' ? false : undefined
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
    // Only keys the device advertises reach the gateway. `local_ui` keys
    // (Watch, Listen, List) are panel-side view switches that the gateway rejects.
    const keys = new Set<string>();
    if (p.wire === 'keys1') {
      const states = p.states;
      if (typeof states !== 'string' || states.length !== SR260_KEYS.length || !/^[alu]+$/.test(states)) return;
      SR260_KEYS.forEach((key, i) => { if (states[i] === 'a') keys.add(key); });
    } else if (Array.isArray(p.keys)) {
      for (const k of p.keys) if (k && typeof k.key === 'string' && k.state === 'advertised') keys.add(k.key);
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
    // Room_Off is the power button, not a remote key.
    state.keys = SR260_KEYS.filter((k) => k !== 'Room_Off' && room.keys.has(k));
    const catalog = this.catalogState(room);
    if (catalog) state.catalog = catalog;
    const shuffle = this.playingShuffle(room);
    if (shuffle !== undefined) state.shuffle = shuffle;
    this.store!.putState('media', String(room.id), state);
  }

  /**
   * Shuffle of the service that is actually playing: the catalog must be a valid
   * service catalog for the room's measured active audio source.
   */
  private playingShuffle(room: ActiveRoom): boolean | undefined {
    const c = room.catalog;
    const f = room.feedback;
    const audio = pick(room.listen, f.audioValid, f.audioIndex);
    if (!c?.valid || c.kind !== 'service_presets' || c.shuffle === undefined || c.source_id === undefined) return undefined;
    if (!audio || audio.id !== String(c.source_id)) return undefined;
    return c.shuffle;
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
    const state: { power: boolean; volume?: number; mute?: boolean; nowPlaying?: string; track?: ZoneTrack; actions: ZoneAction[] } = { power: !!session, actions };
    if (session) {
      const track = sessionTrack(session.now_playing);
      if (track) { state.track = track; if (track.title) state.nowPlaying = track.title; }
      if (Number.isInteger(r.volume)) { state.volume = r.volume; actions.push('volume', 'volume-up', 'volume-down'); }
      if (typeof r.muted === 'boolean') state.mute = r.muted;
      actions.push('mute', 'unmute');
      // Other members leave the queue; the host room (queue owner) can't be removed, so it turns off.
      if (session.owner_room_id === r.id || (session.owner_room_id !== undefined && this.inScope(session.owner_room_id))) actions.push('off');
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
    // Lighting and Shades overviews appear once the whole-house catalog reports devices.
    const services: ServiceId[] = [];
    if (this.houseDevices.size && config.rooms.some((r) => r.lights.length)) services.push('lighting');
    if (this.houseDevices.size && config.rooms.some((r) => r.shades.length)) services.push('shades');
    if (config.rooms.length) services.push('media');
    if (config.audioZones.length) services.push('audio');
    if (config.scenes.length) services.push('scenes');
    return { ...config, services };
  }

  // ---- outgoing ----

  send(c: Command): void {
    if (!this.online) return;
    switch (c.kind) {
      case 'zones-off':
        if (this.sessions?.sessions.some((session) => session.room_ids.length))
          this.request('audio_sessions_off', { revision: this.sessions.revision });
        return;
      case 'scene': return this.runScene(c.id);
      case 'zone': return this.zoneCommand(c.id, c.action, 'value' in c ? c.value : undefined);
    }
    switch (c.kind) {
      case 'fan-speed': {
        const room = this.store!.config.rooms.find((entry) => entry.id === c.roomId);
        if (validFanSpeed(c.value) && room?.fans?.some((fan) => fan.id === c.id))
          this.request('fan_speed', { room_id: Number(c.roomId), device_id: Number(c.id), value: c.value });
        return;
      }
      case 'experience-button': {
        const room = this.store!.config.rooms.find((entry) => entry.id === c.roomId);
        if (room?.experienceButtons?.some((button) => button.id === c.id))
          this.request('experience_select', { room_id: Number(c.roomId), device_id: Number(c.id) });
        return;
      }
      case 'relay': {
        const room = this.store!.config.rooms.find((entry) => entry.id === c.roomId);
        if (room?.relays?.some((relay) => relay.id === c.id))
          this.request('relay', { room_id: Number(c.roomId), device_id: Number(c.id), action: c.action });
        return;
      }
      case 'light': {
        const id = Number(c.id);
        let level: number;
        if (c.action === 'level') level = c.value;
        else if (c.action === 'toggle') level = this.store!.get('light', c.id)?.on ? 0 : 100;
        else level = c.action === 'on' ? 100 : 0;
        return this.device(id, false, 'level', clamp(level));
      }
      case 'room-lights':
        if (c.action === 'ambient') return;
        for (const d of this.roomDevices(Number(c.id))) if (!d.shade) this.device(d.id, false, 'level', c.action === 'on' ? 100 : 0);
        return;
      case 'shade': {
        const id = Number(c.id);
        if (c.action === 'position') return this.device(id, true, 'level', clamp(c.value));
        return this.device(id, true, c.action);
      }
      case 'room-shades':
        for (const d of this.roomDevices(Number(c.id))) if (d.shade) this.device(d.id, true, c.action);
        return;
      case 'media': {
        const room = this.active;
        if (!room || String(room.id) !== c.id) return;
        return this.mediaCommand(room, c);
      }
    }
  }

  /** Lights and shades of a scoped room: the open room's session list, else the whole-house catalog. */
  private roomDevices(roomId: number): Device[] {
    if (this.active?.id === roomId && this.active.devices.size) return [...this.active.devices.values()];
    return [...this.houseDevices.values()].filter((d) => d.room === roomId);
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
      case 'key':
        if (c.value !== 'Room_Off' && room.keys.has(c.value)) this.request('key', { ...r, value: c.value });
        return;
      case 'shuffle':
        if (this.playingShuffle(room) !== undefined) this.request('shuffle', { ...r, value: c.value ? 1 : 0 });
        return;
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
        return this.request(session.owner_room_id === roomId ? 'audio_session_room_off' : 'audio_session_remove', { ...base, queue_id: session.queue_id });
      case 'on': {
        const target = allowed.includes('on') ? this.joinableSession(doc) : undefined;
        if (target) this.request('audio_session_add', { ...base, queue_id: target.queue_id });
      }
    }
  }

  /**
   * Device command. The open room's devices use its session ("device"); devices in other
   * scoped rooms go through the whole-house catalog ("house_device"). Unknown ids are dropped.
   */
  private device(id: number, shade: boolean, action: string, value?: number): void {
    const room = this.active;
    const fields: Record<string, unknown> = { device_id: id, action };
    if (value !== undefined) fields.value = value;
    const sessionDevice = room?.devices.get(id);
    if (room && sessionDevice) {
      if (sessionDevice.shade === shade) this.request('device', { room_id: room.id, ...fields });
      return;
    }
    const houseDevice = this.houseDevices.get(id);
    if (houseDevice && houseDevice.shade === shade) this.request('house_device', { room_id: houseDevice.room, ...fields });
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
    // Devices in the whole-house catalog keep reporting, so only the others go stale.
    for (const d of room.devices.values()) if (!this.houseDevices.has(d.id)) store.clearState(d.shade ? 'shade' : 'light', String(d.id));
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
    // A queued request for the same target is replaced (slider drags send many levels).
    const { value: _value, ...target } = fields;
    const key = JSON.stringify({ ...target, op });
    const queued = this.outbox.findIndex((item) => item.key === key);
    if (queued >= 0) this.outbox[queued] = { key, json };
    else this.outbox.push({ key, json });
    this.flush();
  }

  private flush(): void {
    if (this.outboxTimer !== undefined) return;
    if (this.requestGapMs <= 0) {
      for (const item of this.outbox.splice(0)) {
        if (ROUTER_DEBUG) console.log('[router out]', item.json);
        this.lib.publishEvent('s', this.joins.request, item.json);
      }
      return;
    }
    const next = this.outbox.shift();
    if (!next) return;
    if (ROUTER_DEBUG) console.log('[router out]', next.json);
    this.lib.publishEvent('s', this.joins.request, next.json);
    this.outboxTimer = setTimeout(() => {
      this.outboxTimer = undefined;
      this.flush();
    }, REQUEST_GAP_MS);
  }

  /** Re-requests startup documents that never completed (a lost request or frame). */
  private scheduleInitialCheck(): void {
    clearTimeout(this.initialTimer);
    this.initialTimer = setTimeout(() => {
      if (!this.online) return;
      const missing = INITIAL_DOCS.filter((k) => !this.received.has(k));
      if (!missing.length || this.initialTries >= INITIAL_RETRIES) return;
      this.initialTries++;
      for (const k of missing) {
        this.house[k] = new FrameCollector<Doc>(0, k);
        this.request(k);
      }
      this.scheduleInitialCheck();
    }, INITIAL_RETRY_MS);
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
    const wrapped = (value: never) => {
      if (ROUTER_DEBUG) console.log('[router in]', type, signal, typeof value === 'string' ? (value as string).slice(0, 200) : value);
      try {
        cb(value);
      } catch (error) {
        // One bad packet must not stop the panel; report it for diagnosis.
        console.error('[router] failed to handle', type, signal, error);
      }
    };
    this.subs.push([type, signal, this.lib.subscribeState(type, signal, wrapped)]);
  }
}

/** Track text and an https artwork URL from the router's session now_playing; nothing else. */
function sessionTrack(raw: Record<string, unknown> | undefined): ZoneTrack | undefined {
  if (!raw || typeof raw !== 'object') return undefined;
  const track: ZoneTrack = {};
  for (const key of ['title', 'artist', 'album', 'station'] as const) {
    const v = raw[key];
    if (typeof v === 'string' && v.trim()) track[key] = v.trim().slice(0, 200);
  }
  if (typeof raw.artwork === 'string' && /^https:\/\/[^\s]+$/.test(raw.artwork) && raw.artwork.length <= 2048) track.artwork = raw.artwork;
  return Object.keys(track).length ? track : undefined;
}

function houseCollectors(): HouseCollectors {
  return {
    rooms: new FrameCollector<Doc>(0, 'rooms'),
    scenes: new FrameCollector<Doc>(0, 'scenes'),
    audio_sessions: new FrameCollector<Doc>(0, 'audio_sessions'),
    site: new FrameCollector<Doc>(0, 'site'),
    house_devices: new FrameCollector<Doc>(0, 'house_devices')
  };
}

function parseCountdownState(value: unknown) {
  if (!value || typeof value !== 'object') return;
  const { running, remaining } = value as { running?: unknown; remaining?: unknown };
  if (typeof running === 'boolean' && typeof remaining === 'number' &&
      Number.isSafeInteger(remaining) && remaining >= 0) return { running, remaining };
}

function validFanSpeed(value: unknown): value is number {
  return typeof value === 'number' && Number.isInteger(value) && value >= 0 && value <= 4;
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

/** Labels for the scene kinds the gateway reports (GatewayCore.DiscoverPanelActions). */
const SCENE_KIND_LABELS: Record<string, string> = { light: 'Lighting', macro: 'Macro' };

function humanize(kind: string): string {
  if (SCENE_KIND_LABELS[kind]) return SCENE_KIND_LABELS[kind];
  const text = kind.replace(/[_-]+/g, ' ').trim();
  return text ? text.charAt(0).toUpperCase() + text.slice(1) : '';
}
