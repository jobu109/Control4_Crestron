import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { HouseStore } from '../src/model/store';
import { PanelRouterAdapter } from '../src/adapters/panel-router';
import { SimulatedRouter } from '../src/adapters/router-sim';

describe('simulated router preview', () => {
  beforeEach(() => vi.useFakeTimers());
  afterEach(() => vi.useRealTimers());

  it('drives the real adapter end to end', async () => {
    const store = new HouseStore();
    store.attach(new PanelRouterAdapter(new SimulatedRouter()));
    // Requests are spaced REQUEST_GAP_MS apart and documents are paged, so startup takes a moment.
    await vi.advanceTimersByTimeAsync(2000);
    expect(store.connection).toBe('online');
    expect(store.config.rooms.map((r) => r.name)).toEqual(['Family Room', 'Kitchen', 'Primary Suite', 'Office']);
    expect(store.config.services).toEqual(['lighting', 'shades', 'media', 'audio', 'scenes']);

    store.focusRoom('1');
    await vi.advanceTimersByTimeAsync(500);
    const room = store.room('1')!;
    expect(room.lights.length).toBe(3);
    expect(room.shades.length).toBe(2);
    expect(store.get('media', '1')?.title).toBe('Morning Light');

    store.send({ kind: 'light', id: '102', action: 'level', value: 55 });
    await vi.advanceTimersByTimeAsync(500);
    expect(store.get('light', '102')).toEqual({ on: true, level: 55 });

    store.send({ kind: 'media', id: '1', action: 'source', value: '1011' });
    await vi.advanceTimersByTimeAsync(500);
    expect(store.get('media', '1')?.keys).toContain('Select');

    store.send({ kind: 'media', id: '1', action: 'source', value: '1002' });
    await vi.advanceTimersByTimeAsync(1000);
    const catalog = store.get('media', '1')?.catalog;
    expect(catalog?.items.map((i) => i.name)).toEqual(['Indie Radio', 'Classic Rock Radio', 'Chill Radio']);
    // Browsing alone does not offer shuffle; it appears once Pandora is the playing source.
    expect(store.get('media', '1')?.shuffle).toBeUndefined();
    store.send({ kind: 'media', id: '1', action: 'item', value: catalog!.items[1].id });
    await vi.advanceTimersByTimeAsync(500);
    expect(store.get('media', '1')?.station).toBe('Classic Rock Radio');
    expect(store.get('media', '1')?.shuffle).toBe(false);
  });
});
