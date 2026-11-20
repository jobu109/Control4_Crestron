import type { CrComLibLike } from './crestron';
import { ROUTER_JOINS as J, SR260_KEYS } from './panel-router';
import { encodeFrames } from './wire';

// Development preview: a simulated Control4 - Panel Room Router behind the
// CrComLib interface. `?adapter=sim` runs the real PanelRouterAdapter against
// it, so pages can be exercised with router-shaped data and no processor.
// The sample house is generic and is not any real installation.

interface SimDevice { id: number; name: string; shade: boolean; value: number | null }
interface SimSource { id: string; name: string; presets?: string[]; shuffle?: boolean }
interface SimRoom {
  id: number; name: string; devices: SimDevice[]; listen: SimSource[]; watch: SimSource[];
  volume: number; muted: boolean; power: boolean; audio: number; video: number;
  title: string; artist: string; station: string; playing: boolean; position: number; duration: number;
  opened: string;
}

const NAV_KEYS = ['Up', 'Down', 'Left', 'Right', 'Select', 'Back', 'Menu', 'Info', 'Guide', 'Channel_Up', 'Channel_Down',
  'Page_Up', 'Page_Down', 'Previous', 'Play', 'Pause', 'Rewind', 'Fast_Forward', 'Skip_Back', 'Skip_Forward', 'Stop',
  'Record', 'DVR', 'Red', 'Green', 'Yellow', 'Blue', 'Star', 'Pound', 'Volume_Up', 'Volume_Down',
  'Digit_0', 'Digit_1', 'Digit_2', 'Digit_3', 'Digit_4', 'Digit_5', 'Digit_6', 'Digit_7', 'Digit_8', 'Digit_9'];
const MUSIC_KEYS = ['Play', 'Pause', 'Skip_Forward', 'Skip_Back', 'Volume_Up', 'Volume_Down'];
const TRACKS = [['Morning Light', 'The Harbor Lines'], ['Slow Tide', 'North Coast'], ['Paper Planes', 'Lumen Six'], ['Open Road', 'Delta Sun']];
const PAGE_KIND: Record<string, string> = {
  device_page: 'd', audio_page: 'a', video_page: 'v', media_page: 'm', playing_page: 'n',
  rooms_page: 'rooms', scenes_page: 'scenes', audio_sessions_page: 'audio_sessions'
};

function room(id: number, name: string, devices: Array<[string, boolean, number | null]>): SimRoom {
  return {
    id, name, volume: 25, muted: false, power: false, audio: 0, video: 0,
    title: '', artist: '', station: '', playing: false, position: 0, duration: 0, opened: '',
    devices: devices.map(([n, shade, value], i) => ({ id: id * 100 + i + 1, name: n, shade, value })),
    listen: [
      { id: String(id * 1000 + 1), name: 'Spotify', presets: ['Daily Mix 1', 'Focus Flow', 'Dinner Jazz', 'Weekend Hits', 'Acoustic Morning'] },
      { id: String(id * 1000 + 2), name: 'Pandora', presets: ['Indie Radio', 'Classic Rock Radio', 'Chill Radio'], shuffle: false },
      { id: String(id * 1000 + 3), name: 'Turntable' }
    ],
    watch: [{ id: String(id * 1000 + 11), name: 'Apple TV' }, { id: String(id * 1000 + 12), name: 'Cable Box' }]
  };
}

export class SimulatedRouter implements CrComLibLike {
  private subs = new Map<string, Map<string, (value: never) => void>>();
  private next = 0;
  private served = new Map<string, { join: string; frames: string[] }>();
  private active: SimRoom | null = null;
  private sessionRevision = 1;
  private session = { queue: 7, owner: 1, members: new Set([1, 2]) };
  private rooms: SimRoom[] = [
    room(1, 'Family Room', [['Cans', false, 60], ['Lamps', false, 0], ['Accent', false, 35], ['Window Shade', true, 100], ['Patio Shade', true, 40]]),
    room(2, 'Kitchen', [['Pendants', false, 80], ['Under Cabinet', false, 100], ['Island', false, 0]]),
    room(3, 'Primary Suite', [['Ceiling', false, 0], ['Reading', false, 20], ['Blackout', true, 0]]),
    room(4, 'Office', [['Desk', false, 75]])
  ];

  constructor() {
    this.rooms[0].power = this.rooms[1].power = true;
    this.rooms[0].audio = 1;
    this.rooms[0].playing = true;
    this.rooms[0].title = TRACKS[0][0];
    this.rooms[0].artist = TRACKS[0][1];
    this.rooms[0].duration = 214;
    setTimeout(() => this.emit('b', J.connected, true), 50);
    setInterval(() => this.tick(), 1000);
  }

  subscribeState(type: string, signal: string, cb: (value: never) => void): string {
    const key = `${type}:${signal}`;
    const id = String(++this.next);
    if (!this.subs.has(key)) this.subs.set(key, new Map());
    this.subs.get(key)!.set(id, cb);
    return id;
  }

  unsubscribeState(type: string, signal: string, id: string): void {
    this.subs.get(`${type}:${signal}`)?.delete(id);
  }

  publishEvent(_type: string, signal: string, value: boolean | number | string): void {
    if (signal !== J.request) return;
    let r: Record<string, unknown>;
    try { r = JSON.parse(String(value)); } catch { return; }
    setTimeout(() => this.handle(r), 30);
  }

  private handle(r: Record<string, unknown>): void {
    const op = String(r.op);
    const page = this.served.get(PAGE_KIND[op]);
    if (page) {
      const n = Math.max(1, Math.min(Number(r.value) || 1, page.frames.length));
      this.emit('s', page.join, page.frames[n - 1]);
      return;
    }
    switch (op) {
      case 'rooms': return this.doc(J.state, 0, 'rooms', { room_id: 0, rooms: this.rooms.map(({ id, name }) => ({ id, name })) });
      case 'scenes': return this.doc(J.state, 0, 'scenes', { room_id: 0, revision: 'sim0000000000001', actions: [
        { slot: 1, kind: 'advanced_lighting', name: 'Good Morning' }, { slot: 2, kind: 'advanced_lighting', name: 'Movie Time' },
        { slot: 3, kind: 'macros', name: 'All Off' }, { slot: 4, kind: 'macros', name: 'Away' }] });
      case 'scene_activate':
        if (r.slot === 3) for (const rm of this.rooms) for (const d of rm.devices) if (!d.shade) this.setDevice(rm, d, 0);
        return;
      case 'audio_sessions': return this.publishSessions();
      case 'audio_sessions_close': return;
      case 'audio_session_volume': case 'audio_session_mute': case 'audio_session_add': case 'audio_session_remove':
        return this.sessionCommand(op, r);
      case 'room': return this.select(Number(r.room_id));
    }
    const rm = this.active;
    if (!rm || r.room_id !== rm.id) return;
    switch (op) {
      case 'device': {
        const d = rm.devices.find((x) => x.id === r.device_id);
        if (!d) return;
        if (r.action === 'level') this.setDevice(rm, d, Number(r.value));
        else if (r.action === 'open' || r.action === 'close') this.setDevice(rm, d, r.action === 'open' ? 100 : 0);
        return;
      }
      case 'volume': rm.volume = Number(r.value); return this.state(rm);
      case 'mute_on': case 'mute_off': rm.muted = op === 'mute_on'; return this.state(rm);
      case 'off':
        Object.assign(rm, { power: false, audio: 0, video: 0, playing: false, title: '', opened: '' });
        this.state(rm); this.keys(rm); this.catalog(rm); return this.media(rm);
      case 'video': {
        const i = rm.watch.findIndex((s) => s.id === r.value);
        if (i < 0) return;
        Object.assign(rm, { power: true, video: i + 1, audio: 0, title: rm.watch[i].name, artist: '', station: '', duration: 0, playing: true });
        this.state(rm); this.keys(rm); return this.media(rm);
      }
      case 'audio': {
        const i = rm.listen.findIndex((s) => s.id === r.value);
        if (i < 0) return;
        const source = rm.listen[i];
        rm.opened = source.id;
        if (!source.presets) {
          Object.assign(rm, { power: true, audio: i + 1, video: 0, title: source.name, artist: '', station: '', duration: 0, playing: true });
          this.state(rm); this.keys(rm); this.catalog(rm); return this.media(rm);
        }
        this.doc(J.catalog, rm.id, 'm', { room_id: rm.id, kind: 'service_presets', valid: false, revision: '', items: [], selection_supported: false });
        setTimeout(() => this.catalog(rm), 400);
        return;
      }
      case 'item': {
        const source = rm.listen.find((s) => s.id === rm.opened);
        const preset = source?.presets?.[Number(r.id) % 100 - 1];
        if (!source || !preset) return;
        const track = TRACKS[Math.floor(Math.random() * TRACKS.length)];
        Object.assign(rm, { power: true, audio: rm.listen.indexOf(source) + 1, video: 0, station: preset, title: track[0], artist: track[1], playing: true, position: 0, duration: 180 + Math.floor(Math.random() * 90) });
        this.state(rm); this.keys(rm); return this.media(rm);
      }
      case 'key': {
        if (r.value === 'Play' || r.value === 'Pause') rm.playing = r.value === 'Play';
        else if (r.value === 'Volume_Up' || r.value === 'Volume_Down') { rm.volume = Math.max(0, Math.min(100, rm.volume + (r.value === 'Volume_Up' ? 2 : -2))); this.state(rm); }
        else if ((r.value === 'Skip_Forward' || r.value === 'Skip_Back') && rm.audio) {
          const track = TRACKS[(TRACKS.findIndex((t) => t[0] === rm.title) + (r.value === 'Skip_Forward' ? 1 : TRACKS.length - 1)) % TRACKS.length];
          Object.assign(rm, { title: track[0], artist: track[1], position: 0 });
        } else return;
        return this.media(rm);
      }
      case 'shuffle': {
        const source = rm.listen.find((s) => s.id === rm.opened);
        if (source?.shuffle === undefined) return;
        source.shuffle = r.value === 1;
        return this.catalog(rm);
      }
    }
  }

  private select(id: number): void {
    const rm = this.rooms.find((x) => x.id === id);
    if (!rm) return;
    this.active = rm;
    this.doc(J.state, rm.id, 'd', { room_id: rm.id, devices: rm.devices.map(({ id: did, name, shade, value }) => ({ id: did, name, shade, value })) });
    for (const [join, kind, mode, list] of [[J.audio, 'a', 'listen', rm.listen], [J.video, 'v', 'watch', rm.watch]] as const)
      this.doc(join, rm.id, kind, { room_id: rm.id, mode, valid: true, page: 1, pages: 1, total: list.length, items: list.map(({ id: sid, name }) => ({ id: sid, name })) });
    this.state(rm);
    this.keys(rm);
    this.doc(J.catalog, rm.id, 'm', { room_id: rm.id, kind: '', valid: false, revision: '', items: [], selection_supported: false });
    this.media(rm);
  }

  private setDevice(rm: SimRoom, d: SimDevice, value: number): void {
    d.value = Math.max(0, Math.min(100, Math.round(value)));
    if (this.active === rm) setTimeout(() => this.plain(J.state, { r: rm.id, u: d.id, v: d.value }), 150);
  }

  private state(rm: SimRoom): void {
    this.plain(J.state, { r: rm.id, volume: rm.volume, volumeValid: 1, muted: rm.muted ? 1 : 0, muteValid: 1, power: rm.power ? 1 : 0,
      sourceValid: 1, audioIndex: rm.audio, audioValid: 1, videoIndex: rm.video, videoValid: 1, selectReady: 1, nowPlayingValid: rm.power ? 1 : 0 });
  }

  private keys(rm: SimRoom): void {
    const available = new Set(rm.video ? NAV_KEYS : rm.audio ? MUSIC_KEYS : []);
    this.plain(J.keys, { wire: 'keys1', room_id: rm.id, states: SR260_KEYS.map((k) => (available.has(k) ? 'a' : 'u')).join('') });
  }

  private catalog(rm: SimRoom): void {
    const source = rm.listen.find((s) => s.id === rm.opened);
    if (!source?.presets) {
      this.doc(J.catalog, rm.id, 'm', { room_id: rm.id, kind: '', valid: false, revision: '', items: [], selection_supported: false });
      return;
    }
    const doc: Record<string, unknown> = { room_id: rm.id, kind: 'service_presets', scope: 'room', valid: true, revision: `rev${source.id}`,
      page: 1, pages: 1, total: source.presets.length, partial: false, selection_supported: true, source_id: Number(source.id),
      items: source.presets.map((name, i) => ({ id: String(Number(source.id) * 100 + i + 1), name })) };
    if (source.shuffle !== undefined) doc.pandora_shuffle = source.shuffle ? 'On' : 'Off';
    this.doc(J.catalog, rm.id, 'm', doc);
  }

  private media(rm: SimRoom): void {
    this.doc(J.playing, rm.id, 'n', rm.power
      ? { room_id: rm.id, valid: true, title: rm.title, artist: rm.artist, album: '', artwork: '', station: rm.station, playback_action: rm.playing ? 'pause' : 'play' }
      : { room_id: rm.id, valid: false });
    this.progress(rm);
  }

  private progress(rm: SimRoom): void {
    const valid = rm.power && rm.audio > 0 && rm.duration > 0;
    this.plain(J.progress, valid
      ? { room_id: rm.id, valid: true, source_id: Number(rm.listen[rm.audio - 1].id), track_revision: '0123456789abcdef', position_seconds: rm.position, duration_seconds: rm.duration }
      : { room_id: rm.id, valid: false });
  }

  private tick(): void {
    const rm = this.active;
    if (!rm || !rm.playing || !rm.duration) return;
    rm.position = (rm.position + 1) % rm.duration;
    this.progress(rm);
  }

  private publishSessions(): void {
    const members = [...this.session.members];
    this.doc(J.state, 0, 'audio_sessions', {
      room_id: 0, available: true, revision: `simsess${this.sessionRevision}`,
      rooms: this.rooms.map((rm) => (this.session.members.has(rm.id) ? { id: rm.id, name: rm.name, volume: rm.volume, muted: rm.muted } : { id: rm.id, name: rm.name })),
      sessions: members.length ? [{ queue_id: this.session.queue, room_ids: members, owner_room_id: this.session.owner, state: 'play' }] : []
    });
  }

  private sessionCommand(op: string, r: Record<string, unknown>): void {
    if (r.revision !== `simsess${this.sessionRevision}` || r.queue_id !== this.session.queue) return;
    const rm = this.rooms.find((x) => x.id === r.room_id);
    if (!rm) return;
    if (op === 'audio_session_volume') rm.volume = Number(r.value);
    else if (op === 'audio_session_mute') rm.muted = r.value === 1;
    else if (op === 'audio_session_add') this.session.members.add(rm.id);
    else if (rm.id !== this.session.owner) this.session.members.delete(rm.id);
    this.sessionRevision++;
    if (this.active === rm) this.state(rm);
    this.publishSessions();
  }

  /** Publish page 1 of a framed document; the adapter requests the rest. */
  private doc(join: string, roomId: number, kind: string, document: unknown): void {
    const epoch = Array.from({ length: 16 }, () => Math.floor(Math.random() * 16).toString(16)).join('');
    const frames = encodeFrames(roomId, kind, epoch, document);
    this.served.set(kind, { join, frames });
    this.emit('s', join, frames[0]);
  }

  private plain(join: string, packet: unknown): void {
    this.emit('s', join, JSON.stringify(packet));
  }

  private emit(type: string, signal: string, value: unknown): void {
    for (const cb of this.subs.get(`${type}:${signal}`)?.values() ?? []) cb(value as never);
  }
}
