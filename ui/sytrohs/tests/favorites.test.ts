import { describe, expect, it } from 'vitest';
import { favoriteActions, readFavoriteKeys } from '../src/model/favorites';
import type { HouseConfig } from '../src/model/types';

const house: HouseConfig = {
  name: 'House', services: [], floors: [], audioZones: [], thermostats: [],
  scenes: [
    { id: 'old:1', name: 'House Off', description: 'Light' },
    { id: 'old:2', name: 'House Off', description: 'Macro' }
  ],
  rooms: [{
    id: '17', name: 'Den', floorId: 'main', lights: [], shades: [], videoSources: [], audioSources: [],
    experienceButtons: [{ id: '621', name: 'Find Remote' }]
  }]
};

describe('Home favorites', () => {
  it('distinguishes scene and macro names and resolves a pin against the current revision', () => {
    const [lighting, macro] = favoriteActions(house);
    expect(lighting.key).not.toBe(macro.key);
    const updated = { ...house, scenes: [{ ...house.scenes[1], id: 'new:7' }] };
    expect(favoriteActions(updated).find((item) => item.key === macro.key)?.command)
      .toEqual({ kind: 'scene', id: 'new:7', action: 'run' });
    expect(favoriteActions(updated).some((item) => item.key === lighting.key)).toBe(false);
  });

  it('does not resolve an ambiguous action or a button outside the current panel scope', () => {
    const button = favoriteActions(house).find((item) => item.name === 'Find Remote')!;
    expect(button.command).toEqual({ kind: 'experience-button', roomId: '17', id: '621', action: 'select' });
    expect(favoriteActions({ ...house, rooms: [] }).some((item) => item.key === button.key)).toBe(false);
    const duplicate = { ...house.scenes[0], id: 'old:3' };
    expect(favoriteActions({ ...house, scenes: [...house.scenes, duplicate] }).map((item) => item.detail))
      .toEqual(['Macro', 'Den']);
  });

  it('loads only unique string identities and handles malformed saved data', () => {
    expect(readFavoriteKeys('["a","a",4,null,"b"]')).toEqual(['a', 'b']);
    expect(readFavoriteKeys('broken')).toEqual([]);
    expect(readFavoriteKeys(null)).toEqual([]);
  });
});
