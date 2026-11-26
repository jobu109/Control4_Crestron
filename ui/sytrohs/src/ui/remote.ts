import { css, html, nothing, type TemplateResult } from 'lit';
import { icon } from './icons';

// On-screen remote for the Watch tab. Only keys the room advertises are drawn;
// a pad with none of its keys is not offered. Volume and mute stay on the
// Now Playing volume bar, which uses the room's own volume commands.

export type Pad = 'nav' | 'keypad' | 'playback';

const ICONS: Record<string, string> = {
  Up: 'chevronUp', Down: 'chevronDown', Left: 'chevronLeft', Right: 'chevronRight',
  Play: 'play', Pause: 'pause', Skip_Back: 'previous', Skip_Forward: 'next'
};

const LABELS: Record<string, string> = {
  Select: 'OK', Page_Up: 'Page +', Page_Down: 'Page −', Channel_Up: 'Ch +', Channel_Down: 'Ch −',
  Fast_Forward: 'FFwd', Rewind: 'Rew', Star: '*', Pound: '#',
  Custom_1: 'Custom 1', Custom_2: 'Custom 2', Custom_3: 'Custom 3', Previous: 'Last', Control4: 'Control4'
};

const DPAD = ['Up', 'Left', 'Select', 'Right', 'Down'];
const NAV_SIDE = ['Back', 'Menu', 'Info', 'Guide', 'List', 'Previous', 'Channel_Up', 'Channel_Down', 'Page_Up', 'Page_Down', 'Control4'];
const DIGITS = ['Digit_1', 'Digit_2', 'Digit_3', 'Digit_4', 'Digit_5', 'Digit_6', 'Digit_7', 'Digit_8', 'Digit_9', 'Star', 'Digit_0', 'Pound'];
const COLORS = ['Red', 'Green', 'Yellow', 'Blue'];
const PLAYBACK = ['Skip_Back', 'Rewind', 'Play', 'Pause', 'Fast_Forward', 'Skip_Forward', 'Stop', 'Record', 'DVR', 'Custom_1', 'Custom_2', 'Custom_3', 'Watch', 'Listen'];

const PADS: Array<[Pad, string, string[]]> = [
  ['nav', 'Navigation', [...DPAD, ...NAV_SIDE]],
  ['keypad', 'Keypad', [...DIGITS, ...COLORS]],
  ['playback', 'Playback', PLAYBACK]
];

const TRANSPORT_KEYS = new Set(['Skip_Back', 'Rewind', 'Play', 'Pause', 'Fast_Forward', 'Skip_Forward', 'Stop', 'Record']);

export const remoteStyles = css`
  .pads { display: flex; gap: 4px; }
  .pads .btn { height: 36px; padding: 0 14px; font-size: 12px; background: transparent; }
  .pads .btn.sel { background: #fff; }
  .pads.sub .btn { border: 1.5px solid transparent; }
  .pads.sub .btn.sel { background: transparent; color: #fff; border-color: rgba(255,255,255,0.85); }
  .remote { flex: 1; display: flex; gap: 18px; min-height: 0; }
  .remote .btn { height: 56px; padding: 0 8px; display: inline-flex; align-items: center; justify-content: center; font-size: 14px; }
  .dpad { display: grid; grid-template-columns: repeat(3, 72px); grid-template-rows: repeat(3, 72px); gap: 8px; align-self: center; }
  .dpad .btn { width: 72px; height: 72px; padding: 0; }
  .dpad .ok { border-radius: 36px; background: #fff; color: #0b0d10; font-weight: 700; }
  .side { flex: 1; display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); grid-auto-rows: 56px; gap: 8px; align-content: start; overflow-y: auto; }
  .keys { flex: 1; display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); grid-auto-rows: 56px; gap: 8px; align-content: start; overflow-y: auto; }
  .keys .btn { font-size: 20px; }
  .colors { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 8px; margin-top: 10px; }
  .colors .btn { height: 40px; }
  .colors [data-key='Red'] { background: #c0392b; } .colors [data-key='Green'] { background: #1f9d4b; }
  .colors [data-key='Yellow'] { background: #c9a227; color: #0b0d10; } .colors [data-key='Blue'] { background: #2b6cb0; }
`;

/** The pads that have at least one advertised key. */
export function availablePads(keys: string[]): Pad[] {
  const set = new Set(keys);
  return PADS.filter(([, , pad]) => pad.some((k) => set.has(k))).map(([id]) => id);
}

export function padTabs(pads: Pad[], current: Pad, choose: (p: Pad) => void): TemplateResult {
  return html`<div class="pads sub" role="tablist" aria-label="Remote pad">
    ${PADS.filter(([id]) => pads.includes(id)).map(([id, label]) => html`<button type="button" role="tab" class="btn ${id === current ? 'sel' : ''}"
      aria-selected=${id === current ? 'true' : 'false'} @click=${() => choose(id)}>${label.toUpperCase()}</button>`)}
  </div>`;
}

export function remote(keys: string[], pad: Pad, press: (key: string) => void): TemplateResult {
  const set = new Set(keys);
  const key = (k: string, cls = 'btn') => set.has(k)
    ? html`<button type="button" class=${cls} data-key=${k} aria-label=${label(k)} @click=${() => press(k)}>${ICONS[k] ? icon(ICONS[k], 24, 2) : label(k)}</button>`
    : html`<span></span>`;
  if (pad === 'nav') {
    const side = NAV_SIDE.filter((k) => set.has(k));
    const hasDpad = DPAD.some((k) => set.has(k));
    return html`<div class="remote">
      ${hasDpad ? html`<div class="dpad">
        <span></span>${key('Up', 'btn neu-square')}<span></span>
        ${key('Left', 'btn neu-square')}${key('Select', 'btn ok')}${key('Right', 'btn neu-square')}
        <span></span>${key('Down', 'btn neu-square')}<span></span>
      </div>` : nothing}
      ${side.length ? html`<div class="side">${side.map((k) => key(k))}</div>` : nothing}
    </div>`;
  }
  if (pad === 'keypad') {
    const colors = COLORS.filter((k) => set.has(k));
    const digits = DIGITS.some((k) => set.has(k));
    return html`<div class="remote" style="flex-direction:column;gap:0">
      ${digits ? html`<div class="keys">${DIGITS.map((k) => key(k))}</div>` : html`<div style="flex:1"></div>`}
      ${colors.length ? html`<div class="colors">${COLORS.map((k) => key(k))}</div>` : nothing}
    </div>`;
  }
  return html`<div class="remote"><div class="keys">${PLAYBACK.filter((k) => set.has(k)).map((k) => key(k, TRANSPORT_KEYS.has(k) ? 'btn neu-square' : 'btn'))}</div></div>`;
}

export function label(key: string): string {
  return LABELS[key] ?? key.replace(/^Digit_/, '').replace(/_/g, ' ');
}
