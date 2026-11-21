// Sunrise and sunset from latitude and longitude, using the standard sunrise
// equation (NOAA/Meeus approximation, accurate to about a minute at mid
// latitudes). No network access is needed.

export interface SunTimes {
  /** Undefined when the sun does not rise or set that day (polar night or midnight sun). */
  sunrise?: Date;
  sunset?: Date;
  polar?: 'night' | 'day';
}

const J2000 = 2451545.0;
const UNIX_EPOCH_JD = 2440587.5;
const DAY_MS = 86400000;
const RAD = Math.PI / 180;

/**
 * @param day any instant on the wanted local day; local noon is best (`new Date(y, m, d, 12)`).
 * @param latitude degrees, north positive
 * @param longitude degrees, east positive
 */
export function sunTimes(day: Date, latitude: number, longitude: number): SunTimes {
  const jd = day.getTime() / DAY_MS + UNIX_EPOCH_JD;
  // Solar day whose transit is nearest the given instant at this longitude.
  const n = Math.round(jd - J2000 - 0.0009 + longitude / 360);
  const meanNoon = n + 0.0009 - longitude / 360;
  const m = mod(357.5291 + 0.98560028 * meanNoon, 360);
  const center = 1.9148 * sin(m) + 0.02 * sin(2 * m) + 0.0003 * sin(3 * m);
  const lambda = mod(m + center + 180 + 102.9372, 360);
  const transit = J2000 + meanNoon + 0.0053 * sin(m) - 0.0069 * sin(2 * lambda);
  const declination = Math.asin(sin(lambda) * sin(23.4397));
  const cosHour = (sin(-0.833) - sin(latitude) * Math.sin(declination)) / (Math.cos(latitude * RAD) * Math.cos(declination));
  if (cosHour > 1) return { polar: 'night' };
  if (cosHour < -1) return { polar: 'day' };
  const hour = Math.acos(cosHour) / RAD / 360;
  return { sunrise: fromJulian(transit - hour), sunset: fromJulian(transit + hour) };
}

function fromJulian(j: number): Date {
  return new Date(Math.round((j - UNIX_EPOCH_JD) * DAY_MS));
}

function sin(degrees: number): number {
  return Math.sin(degrees * RAD);
}

function mod(a: number, b: number): number {
  return ((a % b) + b) % b;
}

export function validCoordinates(latitude: unknown, longitude: unknown): boolean {
  return typeof latitude === 'number' && typeof longitude === 'number' && Number.isFinite(latitude) && Number.isFinite(longitude) &&
    Math.abs(latitude) <= 90 && Math.abs(longitude) <= 180;
}
