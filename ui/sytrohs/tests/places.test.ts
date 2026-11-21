import { describe, expect, it } from 'vitest';
import { lookupPlace, nearestPlace, parsePlaces } from '../src/model/places';

const SAMPLE = ['# test', '@MO', '@New South Wales', 'Alpha\t0\t4012\t-9512', 'Beta\t0\t4025\t-9505', 'Heathcote\t1\t-3409\t15101', ''].join('\n');

describe('offline nearest-town lookup', () => {
  it('parses the table and picks the nearest town with its region', () => {
    const t = parsePlaces(SAMPLE);
    expect(t.names).toEqual(['Alpha', 'Beta', 'Heathcote']);
    expect(nearestPlace(t, 40.121, -95.119)?.label).toBe('Alpha, MO');
    expect(nearestPlace(t, -34.1, 151.0)?.label).toBe('Heathcote, New South Wales');
  });

  it('gives no name when the nearest town is too far away', () => {
    const t = parsePlaces(SAMPLE);
    expect(nearestPlace(t, 0, 0)).toBeUndefined();
    expect(nearestPlace(t, Number.NaN, 0)).toBeUndefined();
  });

  it('names coordinates from the bundled GeoNames table', async () => {
    expect((await lookupPlace(39.1, -94.58))?.label).toBe('Kansas City, MO');
  });
});
