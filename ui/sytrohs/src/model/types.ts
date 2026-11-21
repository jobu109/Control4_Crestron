// Shared house model. Every adapter (mock, Crestron contract, Home Assistant)
// produces a HouseConfig and keeps a map of live device states. The UI only
// ever reads this model and sends Commands, so it never knows which system
// is behind it.

export type ServiceId =
  | 'lighting'
  | 'shades'
  | 'climate'
  | 'comfort'
  | 'media'
  | 'audio'
  | 'scenes'
  | 'security'
  | 'cameras'
  | 'pool'
  | 'irrigation'
  | 'energy';

export const ALL_SERVICES: ServiceId[] = [
  'lighting', 'shades', 'climate', 'comfort', 'media', 'audio',
  'scenes', 'security', 'cameras', 'pool', 'irrigation', 'energy'
];

export interface Floor {
  id: string;
  name: string;
  /** Lower numbers are listed first (Basement before Main Level). */
  order: number;
}

export interface Light {
  id: string;
  name: string;
  dimmable: boolean;
}

export interface Shade {
  id: string;
  name: string;
  /** True when the shade reports a position, not just open/closed. */
  positionable: boolean;
}

export interface Source {
  id: string;
  name: string;
  kind: 'video' | 'audio';
  detail?: string;
}

export interface Room {
  id: string;
  name: string;
  floorId: string;
  icon?: string;
  mediaPower?: boolean;
  lights: Light[];
  shades: Shade[];
  experienceButtons?: Array<{ id: string; name: string; on?: boolean; kind?: 'countdown'; countdown?: CountdownState }>;
  fans?: Array<{ id: string; name: string; speed?: number }>;
  relays?: Array<{ id: string; name: string; closed?: boolean }>;
  videoSources: Source[];
  audioSources: Source[];
  thermostatId?: string;
  /**
   * True when devices and sources arrive only after the room is focused
   * (Crestron Panel Room Router). Until then the arrays are empty, not "none".
   */
  lazy?: boolean;
  /** Room lighting buttons the system supports. Omitted means all three. */
  lightActions?: RoomLightAction[];
}

export interface CountdownState {
  running: boolean;
  remaining: number;
}

export type RoomLightAction = 'ambient' | 'on' | 'off';

export interface Scene {
  id: string;
  name: string;
  icon?: string;
  description?: string;
}

export interface AudioZone {
  id: string;
  name: string;
  roomId?: string;
}

export interface Thermostat {
  id: string;
  name: string;
  roomId?: string;
  modes: string[];
  fanModes: string[];
}

export interface HouseConfig {
  name: string;
  /** Only these services get a dock tile and a page. */
  services: ServiceId[];
  floors: Floor[];
  rooms: Room[];
  scenes: Scene[];
  audioZones: AudioZone[];
  thermostats: Thermostat[];
  /** Optional wallpaper image URL per page id (home, rooms, lighting, ...). */
  wallpapers?: Record<string, string>;
  /** House location as the system reports it. Sunrise and sunset are calculated from the coordinates. */
  location?: SiteLocation;
}

export interface SiteLocation {
  /** Place name to show, for example "Heathcote, New South Wales". */
  name?: string;
  latitude?: number;
  longitude?: number;
}

/** State id used for the house's current weather. */
export const HOUSE_WEATHER = 'house';

// ---- Live state, keyed by `${kind}:${id}` (see stateKey) ----

export interface LightState { on: boolean; level: number }
export interface ShadeState { position: number; moving?: 'opening' | 'closing' }
export interface RoomMediaState {
  power: boolean;
  activeSourceId?: string;
  volume: number;
  mute: boolean;
  title?: string;
  artist?: string;
  album?: string;
  artUrl?: string;
  progress?: { position: number; duration: number };
  /** "Playing from" label (station or playlist) when the system reports one. */
  station?: string;
  /** The playback control the service currently offers, when it is reported. */
  playbackAction?: 'play' | 'pause';
  /** Media actions the system accepts for this room. Omitted means all. */
  actions?: MediaAction[];
  /** Saved stations or presets of the service source the user opened. */
  catalog?: MediaCatalog;
  /** Remote keys the room's video device accepts (SR-260 names such as Up, Select, Digit_1). */
  keys?: string[];
  /** Service shuffle, when the service reports it. */
  shuffle?: boolean;
}

export type MediaAction = 'off' | 'mute' | 'unmute' | 'play' | 'pause' | 'next' | 'previous' | 'volume-up' | 'volume-down';

export interface MediaCatalog {
  sourceId: string;
  items: Array<{ id: string; name: string }>;
  /** False while pages are still arriving or when the service cannot select items. */
  selectable: boolean;
  pending: boolean;
}

export type ZoneAction = 'on' | 'off' | 'mute' | 'unmute' | 'volume' | 'volume-up' | 'volume-down';

export interface ZoneState {
  power: boolean;
  sourceName?: string;
  volume: number;
  mute: boolean;
  nowPlaying?: string;
  /** Current track of the zone's session, as reported (any part may be missing). */
  track?: ZoneTrack;
  /** Zone actions available right now. Omitted means all. */
  actions?: ZoneAction[];
}
export interface ZoneTrack { title?: string; artist?: string; album?: string; station?: string; artwork?: string }
export interface SceneState { active: boolean }
export interface ThermostatState {
  power: boolean;
  mode: string;
  fanMode: string;
  setpoint: number;
  current: number;
  action?: 'heating' | 'cooling' | 'idle';
}

/** Current outdoor weather. Fields the system does not report stay absent. */
export interface WeatherState {
  temperature?: number;
  unit?: 'C' | 'F';
  condition?: string;
  /** Today's forecast range, when reported. */
  low?: number;
  high?: number;
  /** Relative humidity, percent. */
  humidity?: number;
  /** Wind speed in the driver's units: mph with °F, m/s with °C. */
  windSpeed?: number;
  /** Cloud cover, percent. */
  clouds?: number;
  /** Rain and snow over the last hour, mm. */
  rain?: number;
  snow?: number;
}

export type StateKind = 'light' | 'shade' | 'media' | 'zone' | 'scene' | 'thermostat' | 'weather';

export interface StateMap {
  light: LightState;
  shade: ShadeState;
  media: RoomMediaState;
  zone: ZoneState;
  scene: SceneState;
  thermostat: ThermostatState;
  weather: WeatherState;
}

export function stateKey(kind: StateKind, id: string): string {
  return `${kind}:${id}`;
}

// ---- Commands from the UI ----

export type Command =
  | { kind: 'fan-speed'; id: string; roomId: string; action: 'speed'; value: number }
  | { kind: 'experience-button'; id: string; roomId: string; action: 'select' }
  | { kind: 'zones-off' }
  | { kind: 'relay'; id: string; roomId: string; action: 'open' | 'close' }
  | { kind: 'light'; id: string; action: 'on' | 'off' | 'toggle' }
  | { kind: 'light'; id: string; action: 'level'; value: number }
  | { kind: 'room-lights'; id: string; action: 'ambient' | 'on' | 'off' }
  | { kind: 'shade'; id: string; action: 'open' | 'close' | 'stop' }
  | { kind: 'shade'; id: string; action: 'position'; value: number }
  | { kind: 'room-shades'; id: string; action: 'open' | 'close' | 'stop' }
  | { kind: 'media'; id: string; action: 'source'; value: string }
  | { kind: 'media'; id: string; action: 'volume'; value: number }
  | { kind: 'media'; id: string; action: 'item'; value: string }
  | { kind: 'media'; id: string; action: 'key'; value: string }
  | { kind: 'media'; id: string; action: 'shuffle'; value: boolean }
  | { kind: 'media'; id: string; action: MediaAction }
  | { kind: 'zone'; id: string; action: 'on' | 'off' | 'mute' | 'unmute' | 'volume-up' | 'volume-down' }
  | { kind: 'zone'; id: string; action: 'volume'; value: number }
  | { kind: 'scene'; id: string; action: 'run' }
  | { kind: 'thermostat'; id: string; action: 'setpoint'; value: number }
  | { kind: 'thermostat'; id: string; action: 'mode' | 'fan'; value: string }
  | { kind: 'thermostat'; id: string; action: 'power'; value: boolean };
