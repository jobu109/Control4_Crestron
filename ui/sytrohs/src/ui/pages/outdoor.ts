import { css, html, nothing } from 'lit';
import type { Command, OutdoorDevice } from '../../model/types';
import { ZPage, shared } from '../base';

const poolReadings = [
  ['PUMPMODE', 'Pump mode'], ['POOL_HEATMODE', 'Pool heat mode'],
  ['POOL_TEMPERATURE', 'Pool temperature'], ['POOL_SETPOINT', 'Pool setpoint'],
  ['SPAMODE', 'Spa mode'], ['SPA_HEATMODE', 'Spa heat mode'],
  ['SPA_TEMPERATURE', 'Spa temperature'], ['SPA_SETPOINT', 'Spa setpoint'],
  ['AIR_TEMPERATURE', 'Air temperature']
];
const programReadings = [['STEP', 'Reported step'], ['STEP_TIME', 'Reported step time']];

const modes = ['Off', 'Every Day', 'Odd Days', 'Even Days', 'Select Days'];
const days = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday'];
type IrrigationAction = Extract<Command, { kind: 'irrigation' }>['action'];

class OutdoorPage extends ZPage {
  protected kind: 'pool' | 'irrigation' = 'pool';

  static styles = [shared, css`
    .content { overflow-y: auto; display: grid; gap: 16px; align-content: start; }
    article { padding: 22px; border: 1px solid var(--line); border-radius: 16px; background: var(--panel); }
    h2 { margin: 0 0 16px; }
    dl { margin: 0; display: grid; grid-template-columns: 1fr auto; gap: 12px 24px; }
    .actions, form { display: flex; gap: 12px; margin-top: 16px; align-items: end; flex-wrap: wrap; }
    form label { display: grid; gap: 6px; }
    input, select { background: #292e36; color: white; border: 1px solid var(--line); border-radius: 8px; padding: 8px; }
    [disabled] { opacity: .45; }
    dd { margin: 0; font-weight: 700; }
  `];

  private programControls(device: OutdoorDevice, disabled: boolean) {
    const unavailable = (action: IrrigationAction) => disabled || device['can_' + action] !== 1;
    const send = (action: IrrigationAction, setting?: string, state?: string) =>
      this.send({ kind: 'irrigation', id: device.id, action, setting, state });
    const submit = (event: SubmitEvent, action: IrrigationAction) => {
      event.preventDefault();
      const values = new FormData(event.currentTarget as HTMLFormElement);
      send(action, String(values.get('setting')), action === 'day' ? String(values.get('state')) : undefined);
    };
    const transport = [['run', 'Run Program'], ['stop', 'Stop Program'], ['pause', 'Pause Program'], ['resume', 'Resume Program']] as const;
    return html`<div class="actions">${transport.map(([action, label]) => html`
      <button class="btn" ?disabled=${unavailable(action)} @click=${() => send(action)}>${label}</button>`)}
    </div>
    <form @submit=${(event: SubmitEvent) => submit(event, 'mode')}>
      <label>Program mode<select name="setting" required ?disabled=${unavailable('mode')}>
        <option value="" selected disabled>Choose mode</option>${modes.map((mode) => html`<option>${mode}</option>`)}
      </select></label><button class="btn" ?disabled=${unavailable('mode')}>Set Mode</button>
    </form>
    <form @submit=${(event: SubmitEvent) => submit(event, 'day')}>
      <label>Watering day<select name="setting" ?disabled=${unavailable('day')}>
        ${days.map((day) => html`<option>${day}</option>`)}
      </select></label>
      <label>State<select name="state" ?disabled=${unavailable('day')}><option>Enable</option><option>Disable</option></select></label>
      <button class="btn" ?disabled=${unavailable('day')}>Set Day</button>
    </form>
    ${(['hour', 'minute'] as const).map((action) => html`
      <form @submit=${(event: SubmitEvent) => submit(event, action)}>
        <label>${action === 'hour' ? 'Start hour' : 'Start minute'}
          <input name="setting" type="number" min="0" max=${action === 'hour' ? 24 : 59}
            step="1" required ?disabled=${unavailable(action)}>
        </label><button class="btn" ?disabled=${unavailable(action)}>Set ${action === 'hour' ? 'Hour' : 'Minute'}</button>
      </form>`)}`;
  }

  render() {
    const devices = this.store.config.outdoor?.[this.kind] ?? [];
    const readings = this.kind === 'pool' ? poolReadings : programReadings;
    const result = this.store.config.irrigationResult;
    return html`<section class="content">${devices.length ? devices.map((device) => {
      const fields = readings.filter(([key]) => {
        const capability = key.startsWith('SPA') ? 'HASSPA' : key.startsWith('POOL_') ? 'HASPOOL'
          : key === 'AIR_TEMPERATURE' ? 'HASAIR' : '';
        return device[key] !== undefined && (!capability || !/^(false|0)$/i.test(String(device[capability])));
      });
      const scale = /^(f|c)$/i.test(String(device.SCALE)) ? String(device.SCALE).toUpperCase() : '';
      return html`<article><h2>${device.name}</h2>${fields.length ? html`<dl>
        ${fields.map(([key, label]) => html`<dt>${label}</dt><dd>${device[key]}${scale &&
          /TEMPERATURE|SETPOINT/.test(key) ? ' °' + scale : ''}</dd>`)}
      </dl>` : html`<p>Waiting for Control4 feedback</p>`}
      ${this.kind === 'irrigation' ? this.programControls(device,
        result === 'pending' || this.store.connection !== 'online') : nothing}</article>`;
    }) : html`<p>No devices reported by Control4</p>`}
    ${this.kind === 'irrigation' && result ? html`<p role="status">${result === 'pending'
      ? 'Sending command…' : result === 'sent' ? 'Command accepted by Director' : 'Director did not accept the command'}</p>` : nothing}
    </section>`;
  }
}
customElements.define('z-pool', OutdoorPage);
customElements.define('z-irrigation', class extends OutdoorPage {
  protected override kind = 'irrigation' as const;
});
