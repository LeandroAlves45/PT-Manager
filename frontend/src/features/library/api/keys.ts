/** Separadores da biblioteca; cada um é um recurso diferente da API. */
export type LibraryKind = 'exercises' | 'foods' | 'supplements';

/** Filtros de uma lista da biblioteca, já com a pesquisa estabilizada. */
export interface LibraryFilters {
  readonly search: string;
  readonly activity: 'active' | 'archived' | 'all';
  readonly page: number;
}

/**
 * Query keys da biblioteca.
 *
 * Uma escrita num exercício invalida `kind('exercises')` (todas as páginas e filtros desse
 * separador) sem tocar nos outros dois. A 6E-4 (editores de planos) usa `all` para as suas
 * pesquisas de catálogo, por isso uma escrita aqui também as refresca.
 */
export const libraryKeys = {
  all: ['library'] as const,
  kind: (kind: LibraryKind) => [...libraryKeys.all, kind] as const,
  list: (kind: LibraryKind, filters: LibraryFilters) =>
    [...libraryKeys.kind(kind), 'list', filters] as const,
};
