import type { Command, HouseConfig, Room, StateKind, StateMap } from './types';
import { stateKey } from './types';

export type ConnectionState = 'connecting' | 'online' | 'offline';

/** Anything that can feed the store: mock data, a Crestron contract or Home Assistant. */
export interface Adapter {
  readonly name: string;
  /** Start talking to the system. The adapter calls store.setConfig/setState as data arrives. */
  connect(store: HouseStore): void | Promise<void>;
  send(command: Command): void;
  /** The UI is showing this room. Adapters that load rooms on demand start loading it. */
  focusRoom?(roomId: string): void;
  /** The current page (route name). Lets an adapter open or close page-specific feeds. */
  setView?(view: string): void;
  sendSecurityKeypad?(partition: number, action: string, pin: string): void;
  dispose?(): void;
}

const EMPTY: HouseConfig = {
  name: '',
  services: [],
  floors: [],
  rooms: [],
  scenes: [],
  audioZones: [],
  thermostats: []
};

/**
 * Single source of truth for the UI. Components listen for `change` and
 * re-render; they never talk to an adapter except through `send`.
 */
export class HouseStore extends EventTarget {
  config: HouseConfig = EMPTY;
  connection: ConnectionState = 'connecting';
  private states = new Map<string, unknown>();
  private adapter?: Adapter;
  private pending = false;

  attach(adapter: Adapter): void {
    this.adapter?.dispose?.();
    this.adapter = adapter;
    void adapter.connect(this);
  }

  setConfig(config: HouseConfig): void {
    this.config = config;
    this.notify();
  }

  setConnection(connection: ConnectionState): void {
    if (this.connection === connection) return;
    this.connection = connection;
    this.notify();
  }

  setState<K extends StateKind>(kind: K, id: string, patch: Partial<StateMap[K]>): void {
    const key = stateKey(kind, id);
    const previous = (this.states.get(key) ?? {}) as StateMap[K];
    this.states.set(key, { ...previous, ...patch });
    this.notify();
  }

  get<K extends StateKind>(kind: K, id: string): StateMap[K] | undefined {
    return this.states.get(stateKey(kind, id)) as StateMap[K] | undefined;
  }

  sendSecurityKeypad(partition: number, action: string, pin: string): void {
    this.adapter?.sendSecurityKeypad?.(partition, action, pin);
  }

  send(command: Command): void {
    this.adapter?.send(command);
  }

  /** Called by pages that show one room. Repeated calls for the same room are cheap. */
  focusRoom(roomId: string): void {
    if (!roomId || roomId === this.focusedRoom) return;
    this.focusedRoom = roomId;
    this.adapter?.focusRoom?.(roomId);
  }

  setView(view: string): void {
    if (view === this.view) return;
    this.view = view;
    this.adapter?.setView?.(view);
  }

  /** Last room a page focused, so the adapter can restore it after a reconnect. */
  focusedRoom = '';
  view = '';

  /** True when the state is known (reported by the system), not defaulted. */
  known(kind: StateKind, id: string): boolean {
    return this.states.has(stateKey(kind, id));
  }

  /** Replace a whole state, so fields the system no longer reports become unknown. */
  putState<K extends StateKind>(kind: K, id: string, state: Partial<StateMap[K]>): void {
    this.states.set(stateKey(kind, id), { ...state });
    this.notify();
  }

  /** Remove a state so the UI shows it as unknown again (for example after a disconnect). */
  clearState(kind: StateKind, id: string): void {
    if (this.states.delete(stateKey(kind, id))) this.notify();
  }

  /** Replace one room in the config (devices or sources loaded on demand). */
  updateRoom(id: string, patch: Partial<Room>): void {
    this.config = { ...this.config, rooms: this.config.rooms.map((r) => (r.id === id ? { ...r, ...patch } : r)) };
    this.notify();
  }

  // ---- convenience lookups ----

  room(id: string): Room | undefined {
    return this.config.rooms.find((r) => r.id === id);
  }

  roomsOnFloor(floorId: string): Room[] {
    return this.config.rooms.filter((r) => r.floorId === floorId);
  }

  sortedFloors() {
    return [...this.config.floors].sort((a, b) => a.order - b.order);
  }

  floorName(floorId: string): string {
    return this.config.floors.find((f) => f.id === floorId)?.name ?? '';
  }

  lightsOn(room: Room): number {
    return room.lights.filter((l) => this.get('light', l.id)?.on).length;
  }

  has(service: string): boolean {
    return (this.config.services as string[]).includes(service);
  }

  /** Batch notifications so a burst of feedback causes one render. */
  private notify(): void {
    if (this.pending) return;
    this.pending = true;
    queueMicrotask(() => {
      this.pending = false;
      this.dispatchEvent(new Event('change'));
    });
  }
}
