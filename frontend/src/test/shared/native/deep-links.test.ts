import { describe, expect, it } from 'vitest';

import { resolveDeepLink } from '@/shared/native/deep-links';

const TOKEN = 'AbCdEfGhIjKlMnOpQrStUvWxYz0123456789-_abcde';

describe('resolveDeepLink', () => {
  it.each([
    [`https://app.ptmanager.pt/aceitar-convite?token=${TOKEN}`, `/aceitar-convite?token=${TOKEN}`],
    [`https://app.ptmanager.pt/repor-password?token=${TOKEN}`, `/repor-password?token=${TOKEN}`],
  ])('opens the client link %s inside the app', (url, route) => {
    expect(resolveDeepLink(url)).toBe(route);
  });

  it.each([
    ['another host', `https://evil.test/aceitar-convite?token=${TOKEN}`],
    ['a look-alike host', `https://app.ptmanager.pt.evil.test/aceitar-convite?token=${TOKEN}`],
    ['plain http', `http://app.ptmanager.pt/aceitar-convite?token=${TOKEN}`],
    ['a custom scheme', `ptmanager://app.ptmanager.pt/aceitar-convite?token=${TOKEN}`],
    ['an explicit port', `https://app.ptmanager.pt:8443/aceitar-convite?token=${TOKEN}`],
    ['user info', `https://user@app.ptmanager.pt/aceitar-convite?token=${TOKEN}`],
    ['a trainer-only route', `https://app.ptmanager.pt/confirmar-email?token=${TOKEN}`],
    ['another app route', `https://app.ptmanager.pt/trainer/billing?token=${TOKEN}`],
    ['a trailing slash', `https://app.ptmanager.pt/aceitar-convite/?token=${TOKEN}`],
    ['no token', 'https://app.ptmanager.pt/aceitar-convite'],
    ['an extra parameter', `https://app.ptmanager.pt/aceitar-convite?token=${TOKEN}&next=/admin`],
    ['two tokens', `https://app.ptmanager.pt/aceitar-convite?token=${TOKEN}&token=${TOKEN}`],
    ['a malformed token', 'https://app.ptmanager.pt/aceitar-convite?token=%3Cscript%3E'],
    ['a short token', 'https://app.ptmanager.pt/aceitar-convite?token=abc'],
    ['not a URL', 'aceitar-convite?token=abc'],
  ])('ignores %s', (_case, url) => {
    expect(resolveDeepLink(url)).toBeNull();
  });
});
