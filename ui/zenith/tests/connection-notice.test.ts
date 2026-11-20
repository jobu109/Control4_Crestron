import { describe, expect, it } from 'vitest';
import { loginUrlFor } from '../src/ui/connection-notice';

describe('Web XPanel sign-in notice', () => {
  it('follows the processor login redirect on the same host', () => {
    expect(loginUrlFor('https://192.168.1.169/userlogin.html', '192.168.1.169')).toBe('https://192.168.1.169/userlogin.html');
    expect(loginUrlFor('/userlogin.html?next=x', '192.168.1.169')).toBe('https://192.168.1.169/userlogin.html?next=x');
  });

  it('falls back to the processor login page for missing or foreign redirects', () => {
    expect(loginUrlFor(undefined, 'proc.local')).toBe('https://proc.local/userlogin.html');
    expect(loginUrlFor('https://elsewhere.example/login', 'proc.local')).toBe('https://proc.local/userlogin.html');
    expect(loginUrlFor('javascript:alert(1)', 'proc.local')).toBe('https://proc.local/userlogin.html');
  });
});
