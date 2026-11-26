// Vendored Simple Icons assets. Logos are bundled with the panel archive.
import amazonMusic from '../assets/source-icons/amazonmusic.svg?url';
import appleTv from '../assets/source-icons/appletv.svg?url';
import googleTv from '../assets/source-icons/googletv.svg?url';
import hulu from '../assets/source-icons/hulu.svg?url';
import max from '../assets/source-icons/max.svg?url';
import netflix from '../assets/source-icons/netflix.svg?url';
import nvidia from '../assets/source-icons/nvidia.svg?url';
import pandora from '../assets/source-icons/pandora.svg?url';
import paramountPlus from '../assets/source-icons/paramountplus.svg?url';
import spotify from '../assets/source-icons/spotify.svg?url';
import tuneIn from '../assets/source-icons/tunein.svg?url';
import youtube from '../assets/source-icons/youtube.svg?url';
import type { Source } from '../model/types';

const audio: [RegExp, string][] = [
  [/\bamazon music\b/i, amazonMusic],
  [/\bspotify\b/i, spotify],
  [/\bpandora\b/i, pandora],
  [/\btune\s?in\b/i, tuneIn]
];

const video: [RegExp, string][] = [
  [/^apple\s*tv(?:\s*(?:\+|plus))?$/i, appleTv],
  [/^google\s*tv$/i, googleTv],
  [/^hulu$/i, hulu],
  [/^(?:hbo\s+)?max$/i, max],
  [/^netflix$/i, netflix],
  [/^(?:nvidia\s+)?shield\s+tv$/i, nvidia],
  [/^paramount\s*(?:\+|plus)$/i, paramountPlus],
  [/^you\s*tube(?:\s+tv)?$/i, youtube]
];

export function sourceIcon(source: Source): string | undefined {
  return (source.kind === 'video' ? video : audio).find(([pattern]) => pattern.test(source.name.trim()))?.[1];
}