import * as Sentry from '@sentry/react';

import { scrubBreadcrumb, scrubSentryEvent } from '@/shared/observability/scrubSentryEvent';

/**
 * Liga a monitorização de erros do browser, se o DSN estiver configurado.
 *
 * `VITE_SENTRY_DSN` é opcional e, tal como no backend, desligado por omissão: sem DSN não há
 * pedidos para o Sentry. O DSN é público por desenho (vai no bundle), por isso não é um segredo.
 *
 * A política é a do backend: só erros, sem traces, sem replay e sem dados pessoais. Os erros
 * não apanhados e as promises rejeitadas são capturados pelas integrações por omissão.
 *
 * @returns `true` quando o Sentry ficou ligado.
 */
export function initSentry(): boolean {
  const dsn = import.meta.env.VITE_SENTRY_DSN?.trim();
  if (dsn === undefined || dsn.length === 0) return false;

  Sentry.init({
    dsn,
    environment: import.meta.env.MODE,
    release: import.meta.env.VITE_SENTRY_RELEASE?.trim() || undefined,
    dataCollection: {
      userInfo: false,
      cookies: false,
      httpHeaders: false,
      httpBodies: [],
      urlQueryParams: false,
    },
    tracesSampleRate: 0,
    beforeSend: scrubSentryEvent,
    beforeBreadcrumb: scrubBreadcrumb,
  });
  return true;
}
