import { keepPreviousData, useQuery, type Query } from '@tanstack/react-query';

import { isVideoInProgress } from '@/features/exercise-video';
import { libraryKeys, type LibraryFilters } from '@/features/library/api/keys';
import { apiClient, unwrap } from '@/shared/api/client';
import type { components } from '@/shared/api/schema';

/** Tamanho de página das três tabelas da biblioteca. */
export const LIBRARY_PAGE_SIZE = 25;

/** Acompanhamento de vídeos em processamento: o mesmo ritmo e limite do painel de vídeo. */
const VIDEO_POLL_MS = 3000;
const MAX_VIDEO_POLLS = 100;

/**
 * Query string comum: a API devolve globais ativos + privados do personal trainer, ordenadas por nome.
 */
function listQuery(filters: LibraryFilters) {
  return {
    activity: filters.activity,
    search: filters.search || undefined,
    page_number: filters.page,
    page_size: LIBRARY_PAGE_SIZE,
  };
}

type ExercisePage = components['schemas']['PagedResponseOfExerciseResponse'];

/** Intervalo da lista de exercícios: só enquanto um vídeo da página não está terminal. */
function pollWhileVideoInProgress(query: Query<ExercisePage>): number | false {
  const inProgress = query.state.data?.items.some((item) =>
    isVideoInProgress(item.managed_video_status)
  );
  return inProgress && query.state.dataUpdateCount < MAX_VIDEO_POLLS ? VIDEO_POLL_MS : false;
}

/**
 * Página de exercícios (globais + privados).
 *
 * Orçamento: um `GET /exercises` por combinação de filtros; o estado do vídeo vem na própria
 * linha (`has_ready_video`), nunca um pedido por exercício. `keepPreviousData` mantém a tabela
 * visível ao mudar de página ou filtro.
 *
 * Com um vídeo da página em envio/processamento, a lista repete-se a cada 3 s até ficar
 * terminal: o painel de vídeo pode fechar-se a meio e deixa de o acompanhar.
 */
export function useExerciseListQuery(filters: LibraryFilters) {
  return useQuery({
    queryKey: libraryKeys.list('exercises', filters),
    placeholderData: keepPreviousData,
    refetchInterval: pollWhileVideoInProgress,
    queryFn: async ({ signal }) =>
      unwrap(
        await apiClient.GET('/api/v1/exercises', { params: { query: listQuery(filters) }, signal })
      ),
  });
}

/** Página de alimentos (globais + privados); macros por 100 g e `kcal` calculado no servidor. */
export function useFoodListQuery(filters: LibraryFilters) {
  return useQuery({
    queryKey: libraryKeys.list('foods', filters),
    placeholderData: keepPreviousData,
    queryFn: async ({ signal }) =>
      unwrap(
        await apiClient.GET('/api/v1/foods', { params: { query: listQuery(filters) }, signal })
      ),
  });
}

/** Página de suplementos (globais + privados). */
export function useSupplementListQuery(filters: LibraryFilters) {
  return useQuery({
    queryKey: libraryKeys.list('supplements', filters),
    placeholderData: keepPreviousData,
    queryFn: async ({ signal }) =>
      unwrap(
        await apiClient.GET('/api/v1/supplements', {
          params: { query: listQuery(filters) },
          signal,
        })
      ),
  });
}
