import { useQueries, useQuery } from '@tanstack/react-query';

import { prescriptionKeys } from '@/features/prescriptions/api/keys';
import type { FoodNutrients } from '@/features/prescriptions/lib/nutrition';
import { apiClient, unwrap } from '@/shared/api/client';
import { isApiProblem } from '@/shared/api/problem';

/** Query keys dos pedidos auxiliares do editor de planos alimentares. */
export const mealPlanKeys = {
  /** Valores por 100 g de um alimento escolhido no catálogo. */
  food: (foodId: string) => ['meal-plan-food', foodId] as const,
  /** Unidade de medida de um suplemento escolhido no catálogo. */
  supplementUnit: (supplementId: string) => ['meal-plan-supplement-unit', supplementId] as const,
};

/** Detalhe de um plano alimentar (cálculo, refeições, itens e totais). */
export function useMealPlanDetailQuery(planId: string | null) {
  return useQuery({
    queryKey: prescriptionKeys.mealsDetail(planId ?? ''),
    enabled: planId !== null,
    queryFn: ({ signal }) =>
      apiClient
        .GET('/api/v1/meal-plans/{mealPlanId}', {
          params: { path: { mealPlanId: planId ?? '' } },
          signal,
        })
        .then(unwrap),
  });
}

/** Ficha do cliente: nome, sexo e data de nascimento (idade e «Usar avaliação inicial»). */
export function useNutritionClientQuery(clientId: string) {
  return useQuery({
    queryKey: prescriptionKeys.nutritionClient(clientId),
    enabled: clientId !== '',
    queryFn: ({ signal }) =>
      apiClient
        .GET('/api/v1/clients/{clientId}', { params: { path: { clientId } }, signal })
        .then(unwrap),
  });
}

/** Avaliação inicial do cliente; `null` quando ainda não existe (404). */
export function useNutritionAssessmentQuery(clientId: string, enabled: boolean) {
  return useQuery({
    queryKey: prescriptionKeys.nutritionAssessment(clientId),
    enabled: enabled && clientId !== '',
    queryFn: async ({ signal }) => {
      try {
        return unwrap(
          await apiClient.GET('/api/v1/clients/{clientId}/initial-assessment', {
            params: { path: { clientId } },
            signal,
          })
        );
      } catch (failure) {
        if (isApiProblem(failure) && failure.status === 404) return null;
        throw failure;
      }
    },
  });
}

/**
 * Valores por 100 g dos alimentos do rascunho. Os do plano gravado vêm de `seed` (o detalhe
 * já os traz); os escolhidos agora no catálogo são pedidos uma vez por alimento
 * (`GET /foods/{id}`), porque o seletor só devolve o nome. Um alimento ainda por carregar
 * não entra no mapa e conta como zero.
 */
export function useFoodNutrients(
  foodIds: readonly string[],
  seed: Readonly<Record<string, FoodNutrients>>
): Record<string, FoodNutrients> {
  const missing = [...new Set(foodIds)].filter((id) => id !== '' && seed[id] === undefined);
  const results = useQueries({
    queries: missing.map((id) => ({
      queryKey: mealPlanKeys.food(id),
      staleTime: Infinity,
      queryFn: async ({ signal }: { signal: AbortSignal }): Promise<FoodNutrients> => {
        const food = unwrap(
          await apiClient.GET('/api/v1/foods/{foodId}', {
            params: { path: { foodId: id } },
            signal,
          })
        );
        return {
          kcal: food.kcal,
          protein: food.protein,
          carbs: food.carbs,
          fats: food.fats,
          fiber: food.fiber,
        };
      },
    })),
  });
  const lookup: Record<string, FoodNutrients> = { ...seed };
  results.forEach((result, index) => {
    const id = missing[index];
    if (id !== undefined && result.data !== undefined) lookup[id] = result.data;
  });
  return lookup;
}

/**
 * Unidade de medida dos suplementos do rascunho (para o sufixo da quantidade). Mesma ideia
 * de `useFoodNutrients`: o detalhe dá as do plano gravado, o catálogo as novas. Sem unidade
 * conhecida o ecrã mostra só o número.
 */
export function useSupplementUnits(
  supplementIds: readonly string[],
  seed: Readonly<Record<string, string>>
): Record<string, string> {
  const missing = [...new Set(supplementIds)].filter((id) => id !== '' && seed[id] === undefined);
  const results = useQueries({
    queries: missing.map((id) => ({
      queryKey: mealPlanKeys.supplementUnit(id),
      staleTime: Infinity,
      queryFn: async ({ signal }: { signal: AbortSignal }): Promise<string> =>
        unwrap(
          await apiClient.GET('/api/v1/supplements/{supplementId}', {
            params: { path: { supplementId: id } },
            signal,
          })
        ).unit_of_measure,
    })),
  });
  const lookup: Record<string, string> = { ...seed };
  results.forEach((result, index) => {
    const id = missing[index];
    if (id !== undefined && result.data !== undefined) lookup[id] = result.data;
  });
  return lookup;
}
