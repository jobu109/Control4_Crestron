// Full-screen notice for Web XPanel connection problems. Without it a panel whose browser is not
// signed in to the processor (NOT_AUTHORIZED) just shows the clock and empty pages.

export type NoticeKind = 'sign-in' | 'not-connected';

export interface ConnectionNotice {
  show(kind: NoticeKind, loginUrl?: string): void;
  hide(): void;
  readonly visible: boolean;
}

const TEXT: Record<NoticeKind, { title: string; body: string }> = {
  'sign-in': {
    title: 'Sign in to the processor',
    body: 'This browser is not signed in to the control processor, so the panel cannot connect. ' +
      'Sign in with a processor account, then reconnect.'
  },
  'not-connected': {
    title: 'Not connected to the processor',
    body: 'The panel has not connected yet. Check that the processor is running and reachable, then reconnect.'
  }
};

export function createConnectionNotice(doc: Document = document, reload: () => void = () => location.reload()): ConnectionNotice {
  let root: HTMLElement | null = null;

  const button = (label: string, primary: boolean): HTMLButtonElement => {
    const b = doc.createElement('button');
    b.type = 'button';
    b.textContent = label;
    b.style.cssText = 'min-height:56px;padding:0 28px;border-radius:14px;font:500 18px Montserrat,system-ui,sans-serif;' +
      'letter-spacing:0.08em;text-transform:uppercase;cursor:pointer;' +
      (primary ? 'background:#fff;color:#0b0d10;border:0;' : 'background:transparent;color:#fff;border:2px solid rgba(255,255,255,0.6);');
    return b;
  };

  return {
    get visible() { return root !== null; },
    show(kind, loginUrl) {
      this.hide();
      const text = TEXT[kind];
      root = doc.createElement('div');
      root.setAttribute('role', 'alertdialog');
      root.setAttribute('aria-modal', 'true');
      root.dataset.notice = kind;
      root.style.cssText = 'position:fixed;inset:0;z-index:20;display:flex;align-items:center;justify-content:center;' +
        'background:rgba(8,10,13,0.94);color:#fff;font-family:Montserrat,system-ui,sans-serif';
      const card = doc.createElement('div');
      card.style.cssText = 'max-width:640px;padding:48px;border-radius:24px;background:#13171c;border:1px solid rgba(255,255,255,0.12)';
      const title = doc.createElement('h1');
      title.textContent = text.title;
      title.style.cssText = 'margin:0 0 16px;font-size:34px;font-weight:700';
      const body = doc.createElement('p');
      body.textContent = text.body;
      body.style.cssText = 'margin:0 0 32px;font-size:18px;font-weight:300;line-height:1.5;color:rgba(255,255,255,0.8)';
      const actions = doc.createElement('div');
      actions.style.cssText = 'display:flex;gap:16px;flex-wrap:wrap';
      if (kind === 'sign-in' && loginUrl) {
        const signIn = button('Sign in', true);
        // A new tab keeps this page; after signing in there, Reconnect picks up the session.
        signIn.addEventListener('click', () => { window.open(loginUrl, '_blank', 'noopener'); });
        actions.append(signIn);
      }
      const retry = button('Reconnect', kind !== 'sign-in' || !loginUrl);
      retry.addEventListener('click', () => reload());
      actions.append(retry);
      card.append(title, body, actions);
      root.append(card);
      doc.body.append(root);
    },
    hide() {
      root?.remove();
      root = null;
    }
  };
}

/** Login page for a NOT_AUTHORIZED redirect; only same-host http(s) URLs are followed. */
export function loginUrlFor(redirectTo: unknown, host: string): string {
  const fallback = `https://${host}/userlogin.html`;
  if (typeof redirectTo !== 'string' || !redirectTo) return fallback;
  try {
    const url = new URL(redirectTo, fallback);
    return (url.protocol === 'https:' || url.protocol === 'http:') && url.hostname === host ? url.href : fallback;
  } catch {
    return fallback;
  }
}
