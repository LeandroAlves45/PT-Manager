import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';

import { describe, expect, it } from 'vitest';

interface Rule {
  source: string;
  headers: { key: string; value: string }[];
}

const config = JSON.parse(readFileSync(resolve(__dirname, '../../../vercel.json'), 'utf8')) as {
  headers: Rule[];
};

function headersFor(source: string): Record<string, string> {
  const rule = config.headers.find((r) => r.source === source);
  return Object.fromEntries((rule?.headers ?? []).map((h) => [h.key.toLowerCase(), h.value]));
}

function cspDirectives(): Record<string, string> {
  const value = headersFor('/(.*)')['content-security-policy'] ?? '';
  return Object.fromEntries(
    value
      .split(';')
      .map((part) => part.trim())
      .filter(Boolean)
      .map((part) => {
        const [name, ...rest] = part.split(/\s+/);
        return [name!, rest.join(' ')];
      })
  );
}

describe('headers de segurança (vercel.json)', () => {
  it('CSP estrita: nada inline, nada de terceiros', () => {
    const policy = cspDirectives();
    expect(policy['default-src']).toBe("'none'");
    expect(policy['script-src']).toBe("'self'");
    expect(policy['style-src']).toBe("'self'");
    expect(policy['font-src']).toBe("'self'");
    expect(policy['frame-ancestors']).toBe("'none'");
    expect(policy['base-uri']).toBe("'none'");
    expect(policy['form-action']).toBe("'none'");
    expect(policy['object-src']).toBe("'none'");
    // A página não faz pedidos (fetch/XHR/WebSocket).
    expect(policy['connect-src']).toBe("'none'");
    expect(Object.values(policy).join(' ')).not.toMatch(/unsafe-inline|unsafe-eval|\*|https?:/);
  });

  it('HSTS ≥ 1 ano, nosniff, anti-framing, referrer e permissions', () => {
    const h = headersFor('/(.*)');
    const maxAge = Number(/max-age=(\d+)/.exec(h['strict-transport-security'] ?? '')?.[1]);
    expect(maxAge).toBeGreaterThanOrEqual(31536000);
    expect(h['x-content-type-options']).toBe('nosniff');
    expect(h['x-frame-options']).toBe('DENY');
    expect(h['referrer-policy']).toBe('strict-origin-when-cross-origin');
    expect(h['permissions-policy']).toContain('camera=()');
    expect(h['cross-origin-opener-policy']).toBe('same-origin');
  });

  it('assets com hash ficam em cache imutável', () => {
    expect(headersFor('/assets/(.*)')['cache-control']).toContain('immutable');
  });
});
