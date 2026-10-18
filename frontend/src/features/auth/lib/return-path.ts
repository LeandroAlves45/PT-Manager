/**
 * Destino guardado pelo `RequireAuth` (ou pelo convite), aceite só se for interno.
 *
 * Comparar prefixos não chega: o browser trata `\` como `/`, por isso `/\host` é tão
 * relativo ao protocolo como `//host`. Resolver contra a origem atual e comparar origens
 * cobre todas as variantes.
 *
 * @param state `location.state` do router.
 * @returns Caminho (com query string e hash) ou `null` quando não há destino seguro.
 */
export function getReturnPath(state: unknown): string | null {
  const from = (state as { from?: unknown } | null)?.from;
  if (typeof from !== 'string' || !from.startsWith('/')) return null;

  const target = new URL(from, window.location.origin);
  // `/.//host` resolve para a mesma origem mas com pathname `//host`, que um `navigate` voltaria
  // a tratar como relativo ao protocolo.
  return target.origin === window.location.origin && !target.pathname.startsWith('//')
    ? `${target.pathname}${target.search}${target.hash}`
    : null;
}
