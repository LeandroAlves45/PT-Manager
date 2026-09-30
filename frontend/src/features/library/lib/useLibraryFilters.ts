import type { LibraryFilters } from '@/features/library/api/keys';
import type { ACTIVITY_OPTIONS } from '@/features/library/lib/labels';
import { useDebounce } from '@/shared/hooks/useDebounce';

/** Estado da lista tal como está no URL, com os setters que o escrevem. */
export interface LibraryControls {
  readonly search: string;
  readonly activity: (typeof ACTIVITY_OPTIONS)[number];
  readonly page: number;
  readonly setSearch: (value: string) => void;
  readonly setActivity: (value: (typeof ACTIVITY_OPTIONS)[number]) => void;
  readonly setPage: (page: number) => void;
  readonly clear: () => void;
}

/**
 * Filtros a pedir ao servidor a partir do URL, com a pesquisa estabilizada (300 ms).
 *
 * Corre dentro de cada separador: o Radix desmonta os inativos, por isso o debounce de um
 * separador novo nasce já com o termo atual e nunca herda a pesquisa do anterior. Apagar a
 * pesquisa ("Limpar filtros") não espera pelo debounce.
 *
 * @returns `filtered` distingue "sem resultados" de "biblioteca vazia" (inclui página > 1).
 */
export function useLibraryFilters(controls: LibraryControls): {
  filters: LibraryFilters;
  filtered: boolean;
} {
  const term = controls.search.trim();
  const debounced = useDebounce(term, 300);
  const search = term === '' ? '' : debounced;

  return {
    filters: { search, activity: controls.activity, page: controls.page },
    filtered: search !== '' || controls.activity !== 'active' || controls.page > 1,
  };
}
