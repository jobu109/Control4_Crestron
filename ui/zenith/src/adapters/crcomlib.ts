import type { CrComLibLike } from './crestron';

/**
 * CrComLib ships as a CommonJS bundle. Imported dynamically, its functions can sit at
 * module.default.CrComLib (observed with Vite 8 + ch5-crcomlib 2.20), on the module,
 * on `default`, or only on `window.CrComLib`. Return whichever has the API.
 */
export function resolveCrComLib(module: unknown, globalLib?: unknown): CrComLibLike {
  const m = module as { CrComLib?: unknown; default?: { CrComLib?: unknown; default?: unknown } } | undefined;
  for (const candidate of [m?.default?.CrComLib, m?.CrComLib, m, m?.default, m?.default?.default, globalLib]) {
    const lib = candidate as Partial<CrComLibLike> | undefined;
    if (lib && typeof lib.subscribeState === 'function' && typeof lib.publishEvent === 'function' && typeof lib.unsubscribeState === 'function')
      return lib as CrComLibLike;
  }
  throw new Error('Crestron CrComLib did not load (subscribeState/publishEvent missing)');
}
