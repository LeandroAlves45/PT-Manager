/**
 * Query keys da feature clients.
 *
 * A hierarquia permite invalidar tudo (`all`), só as listas, ou um detalhe concreto —
 * sem ter de saber que filtros estavam ativos noutro ecrã.
 */
export const clientKeys = {
  all: ['clients'] as const,
  lists: () => [...clientKeys.all, 'list'] as const,
  search: (term: string) => [...clientKeys.lists(), 'search', term] as const,
  detail: (clientId: string) => [...clientKeys.all, 'detail', clientId] as const,
};
