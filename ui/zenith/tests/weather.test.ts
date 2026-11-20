import { describe, expect, it } from 'vitest';
import { humidityText, weatherOverview, windText } from '../src/model/weather';

describe('Home page weather wording', () => {
  it('describes temperature and sky from the OpenWeather readings', () => {
    expect(weatherOverview({ temperature: 69.5, unit: 'F', clouds: 0, rain: 0 }, true)).toBe("It's currently mild and sunny.");
    expect(weatherOverview({ temperature: 69.5, unit: 'F', clouds: 0 }, false)).toBe("It's currently mild and clear.");
    expect(weatherOverview({ temperature: 40, unit: 'F', clouds: 95, rain: 1.2 })).toBe("It's currently cold and rainy.");
    expect(weatherOverview({ temperature: 28, unit: 'F', snow: 0.5, windSpeed: 25 })).toBe("It's currently freezing, snowy and windy.");
    expect(weatherOverview({ temperature: 31, unit: 'C', clouds: 45, windSpeed: 3 })).toBe("It's currently hot and partly cloudy.");
  });

  it('says nothing about readings that were not reported', () => {
    expect(weatherOverview(undefined)).toBeUndefined();
    expect(weatherOverview({ temperature: 20 })).toBeUndefined(); // scale unknown, no sky data
    expect(weatherOverview({ temperature: 20, clouds: 70 })).toBe("It's currently mostly cloudy.");
  });

  it('formats humidity and wind in the driver units', () => {
    expect(humidityText({ humidity: 54 })).toBe('54%');
    expect(humidityText({ humidity: 0 })).toBeUndefined();
    expect(windText({ windSpeed: 4, unit: 'F' })).toBe('4 mph');
    expect(windText({ windSpeed: 4, unit: 'C' })).toBe('14 km/h');
    expect(windText({ windSpeed: 4 })).toBeUndefined();
  });
});
