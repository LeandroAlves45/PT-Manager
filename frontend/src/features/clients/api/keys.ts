/** Filtros da listagem de clientes, tal como vivem no URL. */
export interface ClientListFilters {
  readonly search: string;
  readonly activity: 'active' | 'archived' | 'all';
  readonly page: number;
}

/**
 * Query keys da feature clients.
 *
 * A hierarquia permite invalidar tudo (`all`), só as listas, ou um detalhe concreto —
 * sem ter de saber que filtros estavam ativos noutro ecrã. O resumo e a avaliação inicial
 * vivem debaixo do detalhe: invalidar o detalhe de um cliente refresca os três.
 */
export const clientKeys = {
  all: ['clients'] as const,
  lists: () => [...clientKeys.all, 'list'] as const,
  list: (filters: ClientListFilters) => [...clientKeys.lists(), filters] as const,
  search: (term: string) => [...clientKeys.lists(), 'search', term] as const,
  detail: (clientId: string) => [...clientKeys.all, 'detail', clientId] as const,
  summary: (clientId: string) => [...clientKeys.detail(clientId), 'summary'] as const,
  initialAssessment: (clientId: string) =>
    [...clientKeys.detail(clientId), 'initial-assessment'] as const,
};
