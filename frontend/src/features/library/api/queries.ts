import { keepPreviousData, useQuery, type Query } from '@tanstack/react-query';

import { isVideoInProgress } from '@/features/exercise-video';
import { libraryKeys, type LibraryFilters, type LibraryKind } from '@/features/library/api/keys';
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

type LibraryItemData =
  | { readonly kind: 'exercises'; readonly item: components['schemas']['ExerciseResponse'] }
  | { readonly kind: 'foods'; readonly item: components['schemas']['FoodResponse'] }
  | { readonly kind: 'supplements'; readonly item: components['schemas']['SupplementResponse'] };

/**
 * Um item da biblioteca (global ou privado) para a página de edição ou de leitura.
 *
 * Sem cache entre visitas (`gcTime: 0`): o formulário arranca com os valores recebidos, e um
 * valor antigo da cache deixaria o trainer a editar dados desatualizados.
 */
export function useLibraryItemQuery(kind: LibraryKind, id: string) {
  return useQuery({
    queryKey: libraryKeys.item(kind, id),
    gcTime: 0,
    refetchOnWindowFocus: false,
    queryFn: async ({ signal }): Promise<LibraryItemData> => {
      switch (kind) {
        case 'exercises':
          return {
            kind,
            item: unwrap(
              await apiClient.GET('/api/v1/exercises/{exerciseId}', {
                params: { path: { exerciseId: id } },
                signal,
              })
            ),
          };
        case 'foods':
          return {
            kind,
            item: unwrap(
              await apiClient.GET('/api/v1/foods/{foodId}', {
                params: { path: { foodId: id } },
                signal,
              })
            ),
          };
        case 'supplements':
          return {
            kind,
            item: unwrap(
              await apiClient.GET('/api/v1/supplements/{supplementId}', {
                params: { path: { supplementId: id } },
                signal,
              })
            ),
          };
      }
    },
  });
}
