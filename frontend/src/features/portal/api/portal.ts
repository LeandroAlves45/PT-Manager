import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { apiClient, unwrap } from '@/shared/api/client';
import type { components } from '@/shared/api/schema';

type LoggedSet = components['schemas']['MyLoggedSetResponse'];

/**
 * Query keys do portal do cliente.
 *
 * Exportadas pelo `index.ts`: as fases seguintes (treino, tomas, check-ins) invalidam a home
 * depois de escrever, porque os cartões resumem esses dados.
 */
export const portalKeys = {
  all: ['portal'] as const,
  branding: () => [...portalKeys.all, 'branding'] as const,
  home: () => [...portalKeys.all, 'home'] as const,
  workoutToday: () => [...portalKeys.all, 'workoutToday'] as const,
  plan: () => [...portalKeys.all, 'plan'] as const,
  nutrition: () => [...portalKeys.all, 'nutrition'] as const,
  supplementIntakes: () => [...portalKeys.all, 'supplementIntakes'] as const,
  supplements: () => [...portalKeys.all, 'supplements'] as const,
};

/**
 * Marca do personal trainer do cliente autenticado.
 *
 * Um pedido por sessão do portal: a marca só muda quando o personal trainer a edita, por isso fica
 * fresca durante 5 minutos e não volta a ser pedida ao trocar de ecrã.
 */
export function usePortalBrandingQuery() {
  return useQuery({
    queryKey: portalKeys.branding(),
    queryFn: async ({ signal }) =>
      unwrap(await apiClient.GET('/api/v1/portal/branding', { signal })),
    staleTime: 5 * 60 * 1000,
  });
}

/**
 * Home agregada do portal: treino, nutrição, suplementos e próximo check-in num só pedido.
 *
 * Orçamento: um `GET /api/v1/portal/home` ao abrir o Início; nenhum pedido por cartão.
 */
export function usePortalHomeQuery() {
  return useQuery({
    queryKey: portalKeys.home(),
    queryFn: async ({ signal }) => unwrap(await apiClient.GET('/api/v1/portal/home', { signal })),
  });
}

/**
 * Treino de hoje: estado do dia, prescrição e o registo mais recente de cada série.
 *
 * O "hoje" é o `local_date` da API (fuso do personal trainer), nunca o relógio do browser.
 */
export function usePortalWorkoutTodayQuery() {
  return useQuery({
    queryKey: portalKeys.workoutToday(),
    queryFn: async ({ signal }) =>
      unwrap(await apiClient.GET('/api/v1/portal/my-workout/today', { signal })),
  });
}

/** Plano de treino ativo completo, só de leitura. */
export function usePortalPlanQuery() {
  return useQuery({
    queryKey: portalKeys.plan(),
    queryFn: async ({ signal }) =>
      unwrap(await apiClient.GET('/api/v1/portal/my-plan', { signal })),
  });
}

/**
 * Volta a ler o treino de hoje e o cartão do Início depois de uma escrita.
 *
 * Usa-se em `onSettled`: também corre quando a escrita falha, porque um 409 quer dizer que o
 * ecrã estava desatualizado. Devolve a promessa para a mutation só terminar com os dados novos.
 */
function useRefreshWorkout() {
  const queryClient = useQueryClient();

  return () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: portalKeys.workoutToday() }),
      queryClient.invalidateQueries({ queryKey: portalKeys.home() }),
    ]);
}

/** Valores de uma série a gravar. */
export interface SaveSetInput {
  prescriptionId: string;
  setNumber: number;
  logged: LoggedSet | null;
  weightKg: number;
  repsDone: number;
}

/**
 * Regista ou corrige uma série.
 *
 * O `POST` não é idempotente (cada pedido insere uma linha), por isso só se usa quando a linha
 * ainda não tem registo; com `logged`, corrige-se sempre com `PATCH`. O RPE registado é
 * reenviado para a correção de kg/reps não o apagar.
 */
export function useSaveSetMutation() {
  const refresh = useRefreshWorkout();

  return useMutation({
    mutationFn: async ({ prescriptionId, setNumber, logged, weightKg, repsDone }: SaveSetInput) => {
      if (logged !== null)
        return unwrap(
          await apiClient.PATCH('/api/v1/portal/exercise-set-logs/{exerciseSetLogId}', {
            params: { path: { exerciseSetLogId: logged.log_id } },
            body: { weight_kg: weightKg, reps_done: repsDone, rpe: logged.rpe, notes: null },
          })
        );

      return unwrap(
        await apiClient.POST('/api/v1/portal/exercise-set-logs', {
          body: {
            training_plan_day_exercise_id: prescriptionId,
            set_number: setNumber,
            weight_kg: weightKg,
            reps_done: repsDone,
            rpe: null,
            notes: null,
          },
        })
      );
    },
    onSettled: refresh,
  });
}

/** Desmarca uma série (apaga o registo de hoje). O backend recusa depois de concluir o treino. */
export function useUnlogSetMutation() {
  const refresh = useRefreshWorkout();

  return useMutation({
    mutationFn: async (logId: string) =>
      unwrap(
        await apiClient.DELETE('/api/v1/portal/exercise-set-logs/{exerciseSetLogId}', {
          params: { path: { exerciseSetLogId: logId } },
        })
      ),
    onSettled: refresh,
  });
}

/** Conclui o treino de hoje; idempotente e com treino parcial permitido. */
export function useCompleteWorkoutMutation() {
  const refresh = useRefreshWorkout();

  return useMutation({
    mutationFn: async ({ dayId, notes }: { dayId: string; notes: string | null }) =>
      unwrap(
        await apiClient.POST('/api/v1/portal/workout-completions', {
          body: { training_plan_day_id: dayId, notes },
        })
      ),
    onSettled: refresh,
  });
}

/**
 * Plano alimentar ativo do cliente, só de leitura.
 *
 * Orçamento: um `GET /api/v1/portal/my-nutrition` ao abrir o ecrã; refeições, alimentos e
 * totais vêm no mesmo pedido.
 */
export function usePortalNutritionQuery() {
  return useQuery({
    queryKey: portalKeys.nutrition(),
    queryFn: async ({ signal }) =>
      unwrap(await apiClient.GET('/api/v1/portal/my-nutrition', { signal })),
  });
}

/**
 * Suplementos ativos com o estado da toma de hoje ("X de Y tomadas").
 *
 * O "hoje" é o `local_date` da resposta (fuso do personal trainer). Sem paginação: há no máximo
 * uma atribuição ativa por suplemento.
 */
export function usePortalSupplementIntakesQuery() {
  return useQuery({
    queryKey: portalKeys.supplementIntakes(),
    queryFn: async ({ signal }) =>
      unwrap(await apiClient.GET('/api/v1/portal/my-supplements/intakes/today', { signal })),
  });
}

/** Tamanho máximo de página do backend; chega para todas as atribuições ativas de um cliente. */
const SUPPLEMENTS_PAGE_SIZE = 100;

/**
 * Atribuições ativas com o que as tomas de hoje não trazem: notas do personal trainer e
 * descrição do suplemento.
 *
 * Uma só página de 100, sem paginação no ecrã: as linhas juntam-se às tomas pelo
 * `id` da atribuição. É um complemento; o ecrã das tomas funciona sem ele.
 */
export function usePortalSupplementsQuery() {
  return useQuery({
    queryKey: portalKeys.supplements(),
    queryFn: async ({ signal }) =>
      unwrap(
        await apiClient.GET('/api/v1/portal/my-supplements', {
          params: { query: { page_number: 1, page_size: SUPPLEMENTS_PAGE_SIZE } },
          signal,
        })
      ),
  });
}

/** Toma de hoje a marcar (`taken: true`) ou a desmarcar. */
export interface ToggleIntakeInput {
  assignmentId: string;
  taken: boolean;
}

/**
 * Marca (`PUT`) ou desmarca (`DELETE`) a toma de hoje de uma atribuição.
 *
 * Os dois pedidos são idempotentes no backend. Em `onSettled` voltam a ler-se as tomas e o
 * cartão do Início, também quando a escrita falha: um 409 `supplement_assignment_inactive`
 * quer dizer que a lista do ecrã estava desatualizada.
 */
export function useToggleIntakeMutation() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({ assignmentId, taken }: ToggleIntakeInput) => {
      const options = { params: { path: { assignmentId } } };

      if (taken) {
        unwrap(
          await apiClient.PUT('/api/v1/portal/my-supplements/{assignmentId}/intakes/today', options)
        );
        return;
      }

      unwrap(
        await apiClient.DELETE(
          '/api/v1/portal/my-supplements/{assignmentId}/intakes/today',
          options
        )
      );
    },
    onSettled: () =>
      Promise.all([
        queryClient.invalidateQueries({ queryKey: portalKeys.supplementIntakes() }),
        queryClient.invalidateQueries({ queryKey: portalKeys.home() }),
      ]),
  });
}
