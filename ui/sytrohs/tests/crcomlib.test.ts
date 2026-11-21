import { describe, expect, it } from 'vitest';
import { resolveCrComLib } from '../src/adapters/crcomlib';

const api = { subscribeState: () => '1', unsubscribeState: () => {}, publishEvent: () => {} };

describe('CrComLib resolution', () => {
  it('finds the library where the bundled CommonJS module puts it', () => {
    // Shape observed in the built archive: { default: { CrComLib: { ...api, Ch5Button, ... } } }
    expect(resolveCrComLib({ default: { CrComLib: api } })).toBe(api);
    expect(resolveCrComLib({ default: api })).toBe(api);
    expect(resolveCrComLib(api)).toBe(api);
    expect(resolveCrComLib({ default: {} }, api)).toBe(api);
  });

  it('fails loudly instead of passing a wrapper object to the adapter', () => {
    expect(() => resolveCrComLib({ default: { CrComLib: {} } })).toThrow(/CrComLib did not load/);
  });
});
