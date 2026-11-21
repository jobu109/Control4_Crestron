import { describe, expect, it } from 'vitest';
import { sunTimes, validCoordinates } from '../src/model/sun';
import { weatherText } from '../src/ui/pages/home';

// Reference times from published almanac tables (rounded to the minute).
// Each case passes local noon as a UTC instant so the test does not depend on the machine time zone.
const minutes = (a: Date, b: string) => Math.abs(a.getTime() - Date.parse(b)) / 60000;

describe('sunrise and sunset', () => {
  it.each([
    ['Sydney, winter solstice', -33.8688, 151.2093, '2026-06-21T02:00:00Z', '2026-06-20T21:00:00Z', '2026-06-21T06:54:00Z'],
    ['London, summer solstice', 51.5074, -0.1278, '2026-06-21T11:00:00Z', '2026-06-21T03:43:00Z', '2026-06-21T20:21:00Z'],
    // 7:22 AM / 7:30 PM CDT: solar noon 13:26 CDT, equinox day length about 12 h 08 min at 39° N.
    ['Kansas City, equinox', 39.0997, -94.5786, '2026-03-20T17:00:00Z', '2026-03-20T12:22:00Z', '2026-03-21T00:30:00Z']
  ])('%s', (_name, lat, lon, noon, rise, set) => {
    const sun = sunTimes(new Date(noon), lat, lon);
    expect(minutes(sun.sunrise!, rise)).toBeLessThanOrEqual(4);
    expect(minutes(sun.sunset!, set)).toBeLessThanOrEqual(4);
  });

  it('reports polar night and midnight sun instead of times', () => {
    expect(sunTimes(new Date('2026-12-21T11:00:00Z'), 69.65, 18.96)).toEqual({ polar: 'night' });
    expect(sunTimes(new Date('2026-06-21T11:00:00Z'), 69.65, 18.96)).toEqual({ polar: 'day' });
  });

  it('validates coordinates', () => {
    expect(validCoordinates(-33.9, 151)).toBe(true);
    expect(validCoordinates(91, 0)).toBe(false);
    expect(validCoordinates(undefined, 10)).toBe(false);
    expect(validCoordinates(Number.NaN, 10)).toBe(false);
  });
});

describe('weather text', () => {
  it('shows only the parts that are reported', () => {
    expect(weatherText(undefined)).toBeUndefined();
    expect(weatherText({})).toBeUndefined();
    expect(weatherText({ temperature: 21.6, unit: 'C', condition: 'Partly cloudy' })).toBe('22°C · Partly cloudy');
    expect(weatherText({ condition: 'Rain' })).toBe('Rain');
    expect(weatherText({ temperature: 70 })).toBe('70°');
  });
});
