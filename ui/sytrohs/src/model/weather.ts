// Home page weather wording from the OpenWeather readings the gateway publishes. The driver
// reports numbers only (temperature, humidity, wind, cloud cover, rain, snow), so the overview is
// derived from those; nothing is said about a reading that was not reported.
import type { WeatherState } from './types';

const finite = (v: unknown): v is number => typeof v === 'number' && Number.isFinite(v);

/** Temperature in °F, or undefined when the scale is not known. */
function fahrenheit(w: WeatherState): number | undefined {
  if (!finite(w.temperature) || !w.unit) return undefined;
  return w.unit === 'F' ? w.temperature : w.temperature * 9 / 5 + 32;
}

/** Wind in mph, or undefined when the scale is not known (OpenWeather: mph imperial, m/s metric). */
function windMph(w: WeatherState): number | undefined {
  if (!finite(w.windSpeed) || !w.unit) return undefined;
  return w.unit === 'F' ? w.windSpeed : w.windSpeed * 2.23694;
}

function feel(f: number): string {
  if (f <= 32) return 'freezing';
  if (f < 45) return 'cold';
  if (f < 60) return 'cool';
  if (f < 72) return 'mild';
  if (f < 85) return 'warm';
  return 'hot';
}

function sky(w: WeatherState, daylight: boolean | undefined): string | undefined {
  if (finite(w.snow) && w.snow > 0) return 'snowy';
  if (finite(w.rain) && w.rain > 0) return 'rainy';
  if (!finite(w.clouds)) return undefined;
  if (w.clouds <= 20) return daylight === false ? 'clear' : 'sunny';
  if (w.clouds <= 60) return 'partly cloudy';
  if (w.clouds <= 85) return 'mostly cloudy';
  return 'overcast';
}

/** "It's currently mild and sunny." from whichever readings are reported. */
export function weatherOverview(w: WeatherState | undefined, daylight?: boolean): string | undefined {
  if (!w) return undefined;
  const words: string[] = [];
  const f = fahrenheit(w);
  if (f !== undefined) words.push(feel(f));
  const s = sky(w, daylight);
  if (s) words.push(s);
  const mph = windMph(w);
  if (mph !== undefined && mph >= 20) words.push('windy');
  if (!words.length) return undefined;
  const list = words.length === 1 ? words[0] : `${words.slice(0, -1).join(', ')} and ${words[words.length - 1]}`;
  return `It's currently ${list}.`;
}

export function humidityText(w: WeatherState | undefined): string | undefined {
  return w && finite(w.humidity) && w.humidity > 0 && w.humidity <= 100 ? `${Math.round(w.humidity)}%` : undefined;
}

/** "4 mph" (imperial) or "14 km/h" (metric); left out when the scale is unknown. */
export function windText(w: WeatherState | undefined): string | undefined {
  if (!w || !finite(w.windSpeed) || !w.unit) return undefined;
  return w.unit === 'F' ? `${Math.round(w.windSpeed)} mph` : `${Math.round(w.windSpeed * 3.6)} km/h`;
}
