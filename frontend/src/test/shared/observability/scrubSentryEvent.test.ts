import type { Breadcrumb, ErrorEvent } from '@sentry/react';
import { describe, expect, it } from 'vitest';

import {
  scrubBreadcrumb,
  scrubSentryEvent,
  stripUrlSecrets,
} from '@/shared/observability/scrubSentryEvent';

const SENTINEL = 'SENTINELA-SEGREDO';

function eventWithSecrets(): ErrorEvent {
  return {
    type: undefined,
    user: { id: SENTINEL, email: `${SENTINEL}@x.test` },
    request: {
      url: `https://app.test/confirmar-email?token=${SENTINEL}#frag`,
      query_string: `token=${SENTINEL}`,
      cookies: { session: SENTINEL },
      headers: { Authorization: `Bearer ${SENTINEL}` },
      data: { password: SENTINEL },
    },
    breadcrumbs: [
      { category: 'console', message: SENTINEL },
      { category: 'ui.input', message: SENTINEL },
      {
        category: 'fetch',
        data: { url: `https://api.test/api/v1/x?token=${SENTINEL}`, status_code: 200 },
      },
      { category: 'navigation', data: { from: `/a?t=${SENTINEL}`, to: `/b?t=${SENTINEL}` } },
    ],
  };
}

describe('scrubSentryEvent', () => {
  it('não deixa sair nenhum valor sentinela', () => {
    const scrubbed = scrubSentryEvent(eventWithSecrets());

    expect(JSON.stringify(scrubbed)).not.toContain(SENTINEL);
  });

  it('mantém o caminho do pedido e remove query e fragmento', () => {
    const scrubbed = scrubSentryEvent(eventWithSecrets());

    expect(scrubbed.request?.url).toBe('https://app.test/confirmar-email');
    expect(scrubbed.user).toBeUndefined();
    expect(scrubbed.request?.headers).toBeUndefined();
  });

  it('descarta breadcrumbs de consola e de interface e limpa os de rede', () => {
    const scrubbed = scrubSentryEvent(eventWithSecrets());

    expect(scrubbed.breadcrumbs?.map((crumb) => crumb.category)).toEqual(['fetch', 'navigation']);
    expect(scrubbed.breadcrumbs?.[0]?.data).toMatchObject({
      url: 'https://api.test/api/v1/x',
      status_code: 200,
    });
  });

  it('devolve sempre o evento, mesmo sem pedido nem breadcrumbs', () => {
    const bare: ErrorEvent = { type: undefined };

    expect(scrubSentryEvent(bare)).toBe(bare);
  });
});

describe('stripUrlSecrets', () => {
  it.each([
    ['https://a.test/p?token=x#f', 'https://a.test/p'],
    ['/rota?token=x', '/rota'],
    ['/rota', '/rota'],
  ])('%s -> %s', (input, expected) => {
    expect(stripUrlSecrets(input)).toBe(expected);
  });
});

describe('scrubBreadcrumb', () => {
  it('mantém um breadcrumb sem dados tal como está', () => {
    const crumb: Breadcrumb = { category: 'fetch', message: 'ok' };

    expect(scrubBreadcrumb(crumb)).toBe(crumb);
  });
});
