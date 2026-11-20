// Offline nearest-town lookup for the Home page Location. The project coordinates never leave
// the panel: the table (src/data/places.txt, GeoNames cities1000, CC BY 4.0) ships in the archive
// and loads only when coordinates are known. Built by tools/gen-places.py.

export interface PlaceTable {
  regions: string[];
  names: string[];
  region: Uint16Array;
  lat: Int16Array; // degrees * 100
  lon: Int16Array; // degrees * 100
}

export interface NearestPlace {
  label: string; // e.g. "Kansas City, MO"
  distanceKm: number;
}

/** Beyond this the nearest listed town is not a fair name for the location. */
export const MAX_PLACE_KM = 25;

export function parsePlaces(text: string): PlaceTable {
  const regions: string[] = [];
  const names: string[] = [];
  const region: number[] = [];
  const lat: number[] = [];
  const lon: number[] = [];
  for (const line of text.split('\n')) {
    if (!line || line.startsWith('#')) continue;
    if (line.startsWith('@')) { regions.push(line.slice(1)); continue; }
    const [name, r, la, lo] = line.split('\t');
    const ri = Number(r), lai = Number(la), loi = Number(lo);
    if (!name || !Number.isInteger(ri) || !Number.isInteger(lai) || !Number.isInteger(loi) || ri >= regions.length) continue;
    names.push(name); region.push(ri); lat.push(lai); lon.push(loi);
  }
  return { regions, names, region: Uint16Array.from(region), lat: Int16Array.from(lat), lon: Int16Array.from(lon) };
}

export function nearestPlace(table: PlaceTable, latitude: number, longitude: number, maxKm = MAX_PLACE_KM): NearestPlace | undefined {
  if (!Number.isFinite(latitude) || !Number.isFinite(longitude)) return undefined;
  const kmPerDeg = 111.32;
  const cos = Math.cos((latitude * Math.PI) / 180);
  let best = -1;
  let bestD2 = Infinity;
  for (let i = 0; i < table.names.length; i++) {
    const dLat = table.lat[i] / 100 - latitude;
    let dLon = Math.abs(table.lon[i] / 100 - longitude);
    if (dLon > 180) dLon = 360 - dLon;
    const d2 = dLat * dLat + dLon * cos * (dLon * cos);
    if (d2 < bestD2) { bestD2 = d2; best = i; }
  }
  if (best < 0) return undefined;
  const distanceKm = Math.sqrt(bestD2) * kmPerDeg;
  if (distanceKm > maxKm) return undefined;
  return { label: `${table.names[best]}, ${table.regions[table.region[best]]}`, distanceKm };
}

let table: Promise<PlaceTable> | undefined;

/** Lazily loads the bundled table (a separate chunk) and returns the nearest town, if any. */
export async function lookupPlace(latitude: number, longitude: number): Promise<NearestPlace | undefined> {
  table ??= import('../data/places.txt?raw').then((m) => parsePlaces(m.default));
  try {
    return nearestPlace(await table, latitude, longitude);
  } catch (error) {
    console.warn('Place table unavailable', error);
    table = undefined;
    return undefined;
  }
}
