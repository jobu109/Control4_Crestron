import { LitElement, css, html, unsafeCSS, type TemplateResult } from 'lit';
import type { HouseStore } from '../model/store';
import type { Command } from '../model/types';
import { icon } from './icons';

export const GREEN = '#2EB85C';

/** Raised square controls, including icon faces inside larger tap targets. */
export const squareButtonStyles = css`
  :is(.neu-square, button.btn.square, a.sq, button.sq, button > .tile, a > .tile) {
    border: 1px solid #3e444f; border-radius: var(--square-radius, 12px);
    background: #2c3038; color: #c4cbd4;
    box-shadow: 4px 4px 8px #0d1014, -3px -3px 7px #424957;
    transition: box-shadow .18s, color .18s, transform .08s;
  }
  :is(.neu-square.primary, a.sq.solid, button.sq.solid) { color: #f3f6f9; }
  :is(.neu-square.selected, button.btn.square.on, .neu-square.on, .neu-square.sel, button > .tile.on, a > .tile.on),
  :is(a.neu-square, a.sq, button.sq:enabled, button.btn.square:enabled, button.neu-square:enabled):active,
  :is(button:enabled, a):active > .tile {
    background: #292e36; color: var(--square-accent, #04dcb9);
    box-shadow: inset 3px 3px 7px #171a20, inset -3px -3px 7px #3e4450, 0 0 5px var(--square-glow, #04dcb955);
  }
  :is(a.neu-square, a.sq, button.sq:enabled, button.btn.square:enabled, button.neu-square:enabled):active,
  :is(button:enabled, a):active > .tile { transform: translateY(1px); }
  :is(.neu-square, button.btn.square, a.sq, button.sq):focus-visible {
    outline: 2px solid var(--square-accent, #a5fff1); outline-offset: 3px;
  }
  :is(button.btn.square, button.neu-square, button.sq):disabled { opacity: 0.45; cursor: default; }
`;

/** Styles every page shares. Values come from the approved Sytrohs mockups. */
export const shared = css`
  :host { color-scheme: dark; }
  * { scrollbar-width: thin; scrollbar-color: #535d69 transparent; }
  @supports selector(::-webkit-scrollbar) { * { scrollbar-width: auto; scrollbar-color: auto; } }
  *::-webkit-scrollbar { width: 8px; height: 8px; }
  *::-webkit-scrollbar-track, *::-webkit-scrollbar-corner { background: transparent; }
  *::-webkit-scrollbar-thumb { background: #535d69; border-radius: 8px; }
  *::-webkit-scrollbar-thumb:hover { background: #697583; }
  *::-webkit-scrollbar-button, *::-webkit-scrollbar-button:single-button { display: none; width: 0; height: 0; }
  :host {
    position: absolute;
    inset: 0;
    display: block;
    color: #fff;
    font-family: Montserrat, system-ui, sans-serif;
    --green: ${unsafeCSS(GREEN)};
    --panel: rgba(8, 10, 13, 0.72);
    --line: rgba(255, 255, 255, 0.14);
    --soft: #c3c8ce;
    --idle: rgba(255, 255, 255, 0.12);
  }
  * { box-sizing: border-box; }
  button { font-family: inherit; color: inherit; cursor: pointer; }
  a { color: inherit; text-decoration: none; }
  h1, h2, p { margin: 0; }
  .content { position: absolute; left: 48px; right: 48px; top: 168px; height: 560px; }
  .toolbar { position: absolute; left: 48px; right: 48px; top: 104px; height: 48px; display: flex; justify-content: space-between; align-items: center; gap: 16px; }
  .hint { font-size: 14px; font-weight: 500; letter-spacing: 1.5px; color: #e6e8eb; text-transform: uppercase; }
  .panel { border-radius: 16px; background: var(--panel); border: 1px solid var(--line); }
  .label { font-size: 13px; font-weight: 700; letter-spacing: 1.5px; color: var(--soft); text-transform: uppercase; }
  .head { height: 36px; display: flex; justify-content: space-between; align-items: center; border-bottom: 1px solid rgba(255,255,255,0.18); }
  .row { display: flex; align-items: center; gap: 14px; border-bottom: 1px solid rgba(255,255,255,0.10); }
  .btn { height: 50px; padding: 0 18px; border: none; border-radius: 10px; background: var(--idle); color: #e6e8eb; font-size: 14px; font-weight: 500; letter-spacing: 1px; }
  .btn.sel { background: #fff; color: #0b0d10; font-weight: 700; }
  .btn.on { background: var(--green); color: #0b0d10; font-weight: 700; }
  .btn.outline { background: rgba(0,0,0,0.3); border: 1.5px solid rgba(255,255,255,0.85); color: #fff; font-weight: 700; }
  .rocker { display: flex; align-items: stretch; gap: 4px; padding: 5px; border: 0; border-radius: 999px;
    background: #2c3038; box-shadow: 5px 5px 12px #171a20, -5px -5px 12px #444b57; }
  .rocker > button.btn { flex: 1 1 0; min-width: 0; width: auto; padding: 0 12px; border: 0; border-radius: 999px;
    background: transparent; color: #d8dde6; font-weight: 800; display: flex; align-items: center;
    justify-content: center; gap: 8px; text-align: center; }
  .rocker > button.btn.sel, .rocker > button.btn:enabled:active { color: #35e0c0;
    text-shadow: 0 0 8px #35e0c0; box-shadow: inset 5px 5px 11px #171a20, inset -3px -3px 8px #444b57, inset 0 0 20px #35e0c045; }
  .rocker > button.btn:focus-visible { outline: 2px solid #a5fff1; outline-offset: -2px; }
  .rocker > button.btn:disabled { opacity: 0.45; cursor: default; }
  .btn.square { width: 56px; padding: 0; display: inline-flex; align-items: center; justify-content: center; font-size: 26px; font-weight: 300; }
  .tile { width: 48px; height: 48px; flex-shrink: 0; border-radius: 10px; display: inline-flex; align-items: center; justify-content: center; border: 1.5px solid rgba(255,255,255,0.85); color: #fff; }
  .tile.on { background: #fff; border-color: #fff; color: #0b0d10; }
  .toggle { width: 64px; height: 36px; flex-shrink: 0; border: none; border-radius: 18px; padding: 3px; display: flex; justify-content: flex-start; background: rgba(255,255,255,0.28); }
  .toggle.on { justify-content: flex-end; background: var(--green); }
  .toggle::after { content: ''; width: 30px; height: 30px; border-radius: 15px; background: #fff; }
  .name { font-size: 18px; font-weight: 700; }
  .sub { font-size: 13px; color: var(--soft); }
  .grow { flex: 1; min-width: 0; }
  input[type='range'] {
    -webkit-appearance: none; appearance: none; width: 100%; height: 28px; background: transparent; margin: 0;
  }
  input[type='range']::-webkit-slider-runnable-track {
    height: 6px; border-radius: 3px;
    background: linear-gradient(to right, var(--green) var(--pct, 0%), rgba(255,255,255,0.28) var(--pct, 0%));
  }
  input[type='range']::-webkit-slider-thumb {
    -webkit-appearance: none; width: 28px; height: 28px; margin-top: -11px; border-radius: 14px; background: #fff; border: none;
  }
  input[type='range']::-moz-range-track { height: 6px; border-radius: 3px; background: rgba(255,255,255,0.28); }
  input[type='range']::-moz-range-progress { height: 6px; border-radius: 3px; background: var(--green); }
  input[type='range']::-moz-range-thumb { width: 28px; height: 28px; border-radius: 14px; background: #fff; border: none; }
  input[type='range']:disabled { opacity: 0.45; }
  .scrim { position: absolute; inset: 0; background: rgba(0,0,0,0.55); }
  .close { width: 76px; height: 76px; --square-radius: 50%; --square-accent: #fff; --square-glow: #ffffff55; display: flex; align-items: center; justify-content: center; }
  ${squareButtonStyles}
`;

/** Base class for pages: re-renders whenever the store changes. */
export class ZPage extends LitElement {
  static properties = {
    store: { attribute: false },
    params: { attribute: false }
  };

  store!: HouseStore;
  params: string[] = [];
  private onChange = () => this.requestUpdate();

  connectedCallback(): void {
    super.connectedCallback();
    this.store?.addEventListener('change', this.onChange);
  }

  disconnectedCallback(): void {
    this.store?.removeEventListener('change', this.onChange);
    super.disconnectedCallback();
  }

  protected send(command: Command): void {
    this.store.send(command);
  }
}

/** Range input that only commits on release, matching the processor's expectations. */
export function slider(value: number, onCommit: (v: number) => void, label: string, disabled = false): TemplateResult {
  return html`<input
    type="range" min="0" max="100" .value=${String(value)} ?disabled=${disabled}
    aria-label=${label}
    style="--pct:${value}%"
    @input=${(e: Event) => {
      const el = e.target as HTMLInputElement;
      el.style.setProperty('--pct', `${el.value}%`);
    }}
    @change=${(e: Event) => onCommit(Number((e.target as HTMLInputElement).value))}
  />`;
}

export function toggle(on: boolean, onTap: () => void, label: string): TemplateResult {
  return html`<button type="button" class="toggle ${on ? 'on' : ''}" role="switch" aria-checked=${on ? 'true' : 'false'} aria-label=${label} @click=${onTap}></button>`;
}

export function iconButton(name: string, label: string, onTap: () => void, cls = 'btn square'): TemplateResult {
  return html`<button type="button" class=${cls} aria-label=${label} @click=${onTap}>${icon(name, 22)}</button>`;
}
