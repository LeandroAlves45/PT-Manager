import type { Breadcrumb, ErrorEvent } from '@sentry/react';

/**
 * Remove de um URL tudo o que pode transportar segredos: a query string e o fragmento.
 *
 * Os links de confirmação de email, reposição de password e convites levam o token na query
 * (`?token=...`). Um URL que não se consiga interpretar devolve-se sem query por corte simples,
 * nunca inteiro: é preferível perder o caminho a deixar passar o token.
 */
export function stripUrlSecrets(url: string): string {
  try {
    const parsed = new URL(url, 'https://placeholder.invalid');
    const isAbsolute = /^[a-z][a-z0-9+.-]*:/i.test(url);
    return isAbsolute ? `${parsed.origin}${parsed.pathname}` : parsed.pathname;
  } catch {
    return url.split(/[?#]/)[0] ?? '';
  }
}

/**
 * Limpa um evento antes de sair do browser. Espelha a política do backend
 * (`SentryEventScrubber`): só chega ao Sentry a pilha e o caminho, sem cookies, headers,
 * corpo, query nem dados do utilizador.
 *
 * Devolve sempre o evento (nunca `null`): perder um erro por causa da limpeza seria pior do
 * que enviá-lo sem os campos sensíveis.
 */
export function scrubSentryEvent(event: ErrorEvent): ErrorEvent {
  delete event.user;

  if (event.request) {
    delete event.request.cookies;
    delete event.request.headers;
    delete event.request.data;
    delete event.request.query_string;
    if (event.request.url !== undefined) event.request.url = stripUrlSecrets(event.request.url);
  }

  if (event.breadcrumbs) {
    event.breadcrumbs = event.breadcrumbs
      .map(scrubBreadcrumb)
      .filter((crumb): crumb is Breadcrumb => crumb !== null);
  }

  return event;
}

/**
 * Limpa um breadcrumb. Os de consola e de interacção com a interface (texto escrito,
 * conteúdo clicado) são descartados; os de rede e navegação ficam só com o caminho.
 */
export function scrubBreadcrumb(crumb: Breadcrumb): Breadcrumb | null {
  if (crumb.category === 'console' || crumb.category?.startsWith('ui.') === true) return null;

  const data = crumb.data;
  if (data === undefined) return crumb;

  const cleaned: Record<string, unknown> = { ...data };
  for (const key of ['url', 'from', 'to'] as const) {
    const value = cleaned[key];
    if (typeof value === 'string') cleaned[key] = stripUrlSecrets(value);
  }
  delete cleaned.request_body_size;

  return { ...crumb, data: cleaned };
}
