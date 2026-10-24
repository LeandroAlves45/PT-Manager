import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { apiClient, unwrap } from '@/shared/api/client';
import { isApiProblem } from '@/shared/api/problem';
import type { components } from '@/shared/api/schema';

type LoggedSet = components['schemas']['MyLoggedSetResponse'];
type CheckInAnswer = components['schemas']['CheckInAnswerRequest'];
type ProfileUpdate = components['schemas']['UpdateMyProfileRequest'];
type Profile = components['schemas']['MyProfileResponse'];
type InitialAssessmentSubmission = components['schemas']['SubmitMyInitialAssessmentRequest'];

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
  workoutDays: () => [...portalKeys.all, 'workoutDays'] as const,
  workoutDay: (date: string) => [...portalKeys.all, 'workoutDay', date] as const,
  plan: () => [...portalKeys.all, 'plan'] as const,
  nutrition: () => [...portalKeys.all, 'nutrition'] as const,
  mealPlans: () => [...portalKeys.all, 'mealPlans'] as const,
  supplementIntakes: () => [...portalKeys.all, 'supplementIntakes'] as const,
  supplements: () => [...portalKeys.all, 'supplements'] as const,
  planSupplements: () => [...portalKeys.all, 'planSupplements'] as const,
  checkInDue: () => [...portalKeys.all, 'checkInDue'] as const,
  checkInNext: () => [...portalKeys.all, 'checkInNext'] as const,
  profile: () => [...portalKeys.all, 'profile'] as const,
  initialAssessment: () => [...portalKeys.all, 'initialAssessment'] as const,
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

/**
 * Dias passados com treino do plano ativo (filtro de dia), do mais recente para o mais antigo.
 * "Passado" é antes de hoje no fuso do personal trainer; o servidor decide, não o browser.
 */
export function usePortalWorkoutDaysQuery() {
  return useQuery({
    queryKey: portalKeys.workoutDays(),
    queryFn: async ({ signal }) =>
      unwrap(await apiClient.GET('/api/v1/portal/my-workout/days', { signal })),
  });
}

/**
 * Um dia passado do plano ativo, só para ler: o planeado, o registado, as notas das séries e a
 * nota de conclusão. Sem data (`null`) não faz pedido: o ecrã mostra o treino de hoje.
 *
 * @param date Dia local `AAAA-MM-DD` escolhido no filtro.
 */
export function usePortalWorkoutDayQuery(date: string | null) {
  return useQuery({
    queryKey: portalKeys.workoutDay(date ?? ''),
    queryFn: async ({ signal }) =>
      unwrap(
        await apiClient.GET('/api/v1/portal/my-workout/day', {
          params: { query: { date: date ?? undefined } },
          signal,
        })
      ),
    enabled: date !== null,
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
 * Plano alimentar que o cliente escolheu seguir, só de leitura.
 *
 * Sem escolha responde 404 `portal_nutrition_plan_not_selected` (há planos para escolher) ou
 * `portal_nutrition_plan_not_available` (não há nenhum). A app nunca escolhe pelo cliente.
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

/** Planos alimentares ativos do cliente, com o que ele segue marcado em `is_selected`. */
export function usePortalMealPlansQuery() {
  return useQuery({
    queryKey: portalKeys.mealPlans(),
    queryFn: async ({ signal }) =>
      unwrap(await apiClient.GET('/api/v1/portal/my-meal-plans', { signal })),
  });
}

/**
 * Guarda na conta o plano alimentar escolhido. Depois de gravar volta a ler a lista, o plano
 * aberto e o Início, que resumem a escolha.
 */
export function useSelectMealPlanMutation() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (mealPlanId: string) =>
      unwrap(
        await apiClient.PUT('/api/v1/portal/my-meal-plans/selection', {
          body: { meal_plan_id: mealPlanId },
        })
      ),
    onSettled: () =>
      Promise.all([
        queryClient.invalidateQueries({ queryKey: portalKeys.mealPlans() }),
        queryClient.invalidateQueries({ queryKey: portalKeys.nutrition() }),
        queryClient.invalidateQueries({ queryKey: portalKeys.planSupplements() }),
        queryClient.invalidateQueries({ queryKey: portalKeys.home() }),
      ]),
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

/**
 * Suplementos do plano alimentar escolhido (nas refeições e fora delas) com a toma de hoje.
 *
 * Sem plano escolhido o backend responde 404 `portal_nutrition_plan_not_selected`: é um estado
 * do ecrã («Escolhe o teu plano»), não uma falha, e a página trata-o pelo código do erro.
 */
export function usePortalPlanSupplementsQuery() {
  return useQuery({
    queryKey: portalKeys.planSupplements(),
    queryFn: async ({ signal }) =>
      unwrap(await apiClient.GET('/api/v1/portal/my-plan-supplements/today', { signal })),
  });
}

/** Toma de hoje de um suplemento do plano a marcar (`taken: true`) ou a desmarcar. */
export interface TogglePlanIntakeInput {
  itemId: string;
  taken: boolean;
}

/**
 * Marca (`PUT`) ou desmarca (`DELETE`) a toma de hoje de um suplemento do plano escolhido.
 *
 * Os dois pedidos são idempotentes. Em `onSettled` voltam a ler-se as tomas do plano e a home,
 * também quando a escrita falha: um 404 ou 409 quer dizer que o plano mudou.
 */
export function useTogglePlanIntakeMutation() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({ itemId, taken }: TogglePlanIntakeInput) => {
      const options = { params: { path: { itemId } } };

      if (taken) {
        unwrap(
          await apiClient.PUT('/api/v1/portal/my-plan-supplements/{itemId}/intakes/today', options)
        );
        return;
      }

      unwrap(
        await apiClient.DELETE('/api/v1/portal/my-plan-supplements/{itemId}/intakes/today', options)
      );
    },
    onSettled: () =>
      Promise.all([
        queryClient.invalidateQueries({ queryKey: portalKeys.planSupplements() }),
        queryClient.invalidateQueries({ queryKey: portalKeys.home() }),
      ]),
  });
}

/**
 * Lê um recurso opcional do portal: o backend responde 404 `resource_not_found` quando ele
 * não existe (`RespondOptionalAsync`), e isso é um estado do ecrã, não uma falha.
 */
async function orNullWhenAbsent<T>(read: () => Promise<T>): Promise<T | null> {
  try {
    return await read();
  } catch (error) {
    if (isApiProblem(error) && error.code === 'resource_not_found') return null;
    throw error;
  }
}

/**
 * Check-in por responder hoje, ou `null` quando não há.
 *
 * "Hoje" é o dia local do personal trainer, decidido pelo servidor. Um check-in de um dia que
 * já passou nunca aparece aqui: o cliente só pode responder no próprio dia.
 */
export function usePortalDueCheckInQuery() {
  return useQuery({
    queryKey: portalKeys.checkInDue(),
    queryFn: ({ signal }) =>
      orNullWhenAbsent(async () =>
        unwrap(await apiClient.GET('/api/v1/portal/my-check-ins/due', { signal }))
      ),
  });
}

/**
 * Próximo check-in agendado (hoje ou depois), ou `null` quando não há.
 *
 * @param enabled O ecrã só o pede quando não há check-in para responder: com um por responder,
 *   o próximo seria esse mesmo.
 */
export function usePortalNextCheckInQuery(enabled: boolean) {
  return useQuery({
    queryKey: portalKeys.checkInNext(),
    queryFn: ({ signal }) =>
      orNullWhenAbsent(async () =>
        unwrap(await apiClient.GET('/api/v1/portal/my-check-ins/next', { signal }))
      ),
    enabled,
  });
}

/**
 * Envia a resposta a um check-in.
 *
 * Em `onSettled` voltam a ler-se o check-in por responder, o próximo e o cartão do Início,
 * também quando o envio falha: um 409 quer dizer que o ecrã estava desatualizado (já
 * respondido, cancelado ou fora do dia).
 */
export function useRespondCheckInMutation() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({ checkInId, answer }: { checkInId: string; answer: CheckInAnswer }) =>
      unwrap(
        await apiClient.POST('/api/v1/portal/check-ins/{checkInId}/respond', {
          params: { path: { checkInId } },
          body: answer,
        })
      ),
    onSettled: () =>
      Promise.all([
        queryClient.invalidateQueries({ queryKey: portalKeys.checkInDue() }),
        queryClient.invalidateQueries({ queryKey: portalKeys.checkInNext() }),
        queryClient.invalidateQueries({ queryKey: portalKeys.home() }),
      ]),
  });
}

/**
 * Perfil do cliente autenticado: nome, contactos e fotografia.
 *
 * Fica fresco durante 5 minutos, como a marca: o menu do topo lê-o em todas as páginas do
 * portal e só muda quando o próprio cliente o edita (as escritas substituem a cache).
 *
 * @param enabled O menu de perfil é partilhado com o personal trainer e o administrador, que
 *   não têm perfil de cliente: só o cliente faz o pedido.
 */
export function usePortalProfileQuery(enabled = true) {
  return useQuery({
    queryKey: portalKeys.profile(),
    queryFn: async ({ signal }) =>
      unwrap(await apiClient.GET('/api/v1/portal/my-profile', { signal })),
    staleTime: 5 * 60 * 1000,
    enabled,
  });
}

/**
 * Todas as escritas do perfil devolvem o perfil completo: a resposta substitui a cache, sem
 * um `GET` a seguir, e o menu do topo acompanha.
 */
function useProfileMutation<TVariables>(write: (variables: TVariables) => Promise<Profile>) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: write,
    onSuccess: (profile) => queryClient.setQueryData(portalKeys.profile(), profile),
  });
}

/** Grava o nome e os contactos: um opcional a `null` apaga o valor guardado. */
export function useUpdateProfileMutation() {
  return useProfileMutation(async (body: ProfileUpdate) =>
    unwrap(await apiClient.PATCH('/api/v1/portal/my-profile', { body }))
  );
}

/**
 * Substitui a fotografia por `multipart/form-data` com a parte `file`.
 *
 * O OpenAPI descreve este corpo como um `IFormFile` serializado em formulário, que não
 * corresponde ao pedido real (igual ao logo do personal trainer). O `bodySerializer` envia o
 * `FormData` tal como está e o browser escreve o `boundary`.
 */
export function useReplaceAvatarMutation() {
  return useProfileMutation(async (file: File) => {
    const form = new FormData();
    form.append('file', file);

    return unwrap(
      await apiClient.PUT('/api/v1/portal/my-profile/avatar', {
        body: {},
        bodySerializer: () => form,
      })
    );
  });
}

/** Remove a fotografia. Idempotente: sem fotografia, devolve o mesmo perfil. */
export function useRemoveAvatarMutation() {
  return useProfileMutation(async () =>
    unwrap(await apiClient.DELETE('/api/v1/portal/my-profile/avatar'))
  );
}

/**
 * Se a avaliação inicial do cliente ainda falta (`pending`). Nunca devolve os dados dela.
 *
 * Responde 404 `portal_profile_not_available` com a ficha arquivada.
 */
export function usePortalInitialAssessmentStatusQuery() {
  return useQuery({
    queryKey: portalKeys.initialAssessment(),
    queryFn: async ({ signal }) =>
      unwrap(await apiClient.GET('/api/v1/portal/my-initial-assessment', { signal })),
  });
}

/**
 * Entrega a avaliação inicial (204). Em `onSettled` voltam a ler-se o estado e o Início, também
 * quando falha: um 409 quer dizer que a avaliação já existe e o ecrã estava desatualizado.
 */
export function useSubmitInitialAssessmentMutation() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (body: InitialAssessmentSubmission) =>
      unwrap(await apiClient.POST('/api/v1/portal/my-initial-assessment', { body })),
    onSettled: () =>
      Promise.all([
        queryClient.invalidateQueries({ queryKey: portalKeys.initialAssessment() }),
        queryClient.invalidateQueries({ queryKey: portalKeys.home() }),
      ]),
  });
}
