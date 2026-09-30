import { useMutation, useQueryClient } from '@tanstack/react-query';

import { libraryKeys, type LibraryKind } from '@/features/library/api/keys';
import { apiClient, unwrap } from '@/shared/api/client';
import type { components } from '@/shared/api/schema';

type ExerciseRequest = components['schemas']['CreateExerciseRequest'];
type FoodRequest = components['schemas']['CreateFoodRequest'];
type SupplementRequest = components['schemas']['CreateSupplementRequest'];

/**
 * Cria (`exerciseId === null`) ou atualiza um exercício privado.
 *
 * O PATCH substitui todos os campos, por isso o corpo é o mesmo nos dois casos. A resposta do
 * PATCH já traz o estado do vídeo (o servidor relê o exercício depois de gravar).
 */
export function useSaveExerciseMutation(exerciseId: string | null) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (body: ExerciseRequest) =>
      exerciseId === null
        ? unwrap(await apiClient.POST('/api/v1/exercises', { body }))
        : unwrap(
          await apiClient.PATCH('/api/v1/exercises/{exerciseId}', {
            params: { path: { exerciseId } },
            body,
          })
        ),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: libraryKeys.kind('exercises') }),
  });
}

/** Cria ou atualiza um alimento privado (PATCH substitui todos os campos). */
export function useSaveFoodMutation(foodId: string | null) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (body: FoodRequest) =>
      foodId === null
        ? unwrap(await apiClient.POST('/api/v1/foods', { body }))
        : unwrap(
          await apiClient.PATCH('/api/v1/foods/{foodId}', {
            params: { path: { foodId } },
            body,
          })
        ),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: libraryKeys.kind('foods') }),
  });
}

/** Cria ou atualiza um suplemento privado (PATCH substitui todos os campos). */
export function useSaveSupplementMutation(supplementId: string | null) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (body: SupplementRequest) =>
      supplementId === null
        ? unwrap(await apiClient.POST('/api/v1/supplements', { body }))
        : unwrap(
          await apiClient.PATCH('/api/v1/supplements/{supplementId}', {
            params: { path: { supplementId } },
            body,
          })
        ),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: libraryKeys.kind('supplements') }),
  });
}

/**
 * Arquiva ou reativa um item privado (204 sem corpo). Não há apagar: arquivar tira o item das
 * escolhas de planos novos sem mexer nos planos que já o usam.
 */
export function useLibraryActivityMutation(kind: LibraryKind) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({ id, action }: { id: string; action: 'archive' | 'reactivate' }) => {
      switch (kind) {
        case 'exercises': {
          const init = { params: { path: { exerciseId: id } } };
          return unwrap(
            action === 'archive'
              ? await apiClient.POST('/api/v1/exercises/{exerciseId}/archive', init)
              : await apiClient.POST('/api/v1/exercises/{exerciseId}/reactivate', init)
          );
        }
        case 'foods': {
          const init = { params: { path: { foodId: id } } };
          return unwrap(
            action === 'archive'
              ? await apiClient.POST('/api/v1/foods/{foodId}/archive', init)
              : await apiClient.POST('/api/v1/foods/{foodId}/reactivate', init)
          );
        }
        case 'supplements': {
          const init = { params: { path: { supplementId: id } } };
          return unwrap(
            action === 'archive'
              ? await apiClient.POST('/api/v1/supplements/{supplementId}/archive', init)
              : await apiClient.POST('/api/v1/supplements/{supplementId}/reactivate', init)
          );
        }
      }
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: libraryKeys.kind(kind) }),
  });
}
