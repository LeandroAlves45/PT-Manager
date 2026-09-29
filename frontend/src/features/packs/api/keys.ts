/** Filtros da tabela de tipos de pack, tal como vivem na URL. */
export interface PackTypeListFilters {
  readonly search: string;
  readonly activity: 'active' | 'archived' | 'all';
  readonly page: number;
}

/** Filtros da tabela de packs dos clientes; 'clientId' fixa um cliente (tab de detalhe). */
export interface ClientPackListFilters {
  readonly clientId: string | null;
  readonly activity: 'usable' | 'completed' | 'all';
  readonly page: number;
}

/**
 * Query keys da feature packs.
 *
 * Tipos de pack e packs dos clientes vivem debaixo de `all`: uma escrita num pack do cliente
 * invalida `clientPacks()` (listas e `usable` de todos os clientes) sem ter de saber que
 * filtros estavam ativos noutro ecrã.
 */
export const packKeys = {
  all: ['packs'] as const,
  types: () => [...packKeys.all, 'types'] as const,
  typeList: (filters: PackTypeListFilters) => [...packKeys.types(), 'list', filters] as const,
  activeTypes: () => [...packKeys.types(), 'active'] as const,
  clientPacks: () => [...packKeys.all, 'client-packs'] as const,
  clientPackList: (filters: ClientPackListFilters) =>
    [...packKeys.clientPacks(), 'list', filters] as const,
  usable: (clientId: string) => [...packKeys.clientPacks(), 'usable', clientId] as const,
};
