/**
 * Helpers de API para o portal autenticado do cliente.
 *
 * Todos os endpoints dependem do JWT para identificar o cliente atual, por isso
 * o frontend nunca precisa de enviar nem guardar o seu próprio `client_id`.
 */

import api from './axiosConfig';

/**
 * Branding white-label herdado do treinador responsável pela conta do cliente.
 *
 * @returns {{ app_name: string, logo_url: string | null, primary_color: string | null, body_color: string | null }}
 */
export const getPortalBranding = async () => {
  const response = await api.get('/api/v1/portal/branding');
  return response.data;
};

/** Dados de perfil do cliente autenticado. */
export const getMyProfile = async () => {
  const response = await api.get('/api/v1/portal/my-profile');
  return response.data;
};

/**
 * Altera a password do utilizador autenticado.
 *
 * O endpoint exige a password atual e a nova password:
 * `POST /api/v1/auth/users/me/change-password`.
 */
export const changeMyPassword = async (payload) => {
  const response = await api.post('/api/v1/auth/users/me/change-password', payload);
  return response.data;
};

/**
 * Plano de treino ativo do cliente autenticado.
 *
 * `GET /portal/my-plan` devolve o cabeçalho do plano mais os respetivos dias e
 * exercícios aninhados. Os exercícios podem incluir `client_set_logs`, que agora
 * seguem o contrato alinhado do backend e usam `logged_at` em vez do antigo
 * campo `created_at`.
 */
export const getMyTrainingPlan = async () => {
  const response = await api.get('/api/v1/portal/my-plan');
  return response.data;
};

/**
 * Cria ou atualiza os logs reais de séries do cliente para um exercício do plano.
 *
 * O backend devolve agora um payload completo no formato de
 * `ClientExerciseSetLogRead`, incluindo `exercise_id`, `exercise_name`,
 * `logged_at` e `updated_at`.
 *
 * @param {string} planDayExerciseId - ID do exercicio especifico do dia do plano
 * @param {{ logs: Array<{ set_number: number, weight_kg: number | null, reps_done: number | null, notes: string | null }> }} payload
 */
export const upsertExerciseSetLogs = async (planDayExerciseId, payload) => {
  const response = await api.put(
    `/api/v1/portal/my-plan/exercises/${planDayExerciseId}/set_logs`,
    payload
  );
  return response.data;
};

/** Planos alimentares ativos atribuídos ao cliente, incluindo refeições e macros. */
export const getMyMealPlans = async () => {
  const response = await api.get('/api/v1/portal/my-meal-plans');
  return response.data;
};

/** Todos os check-ins visíveis para o cliente autenticado. */
export const getMyCheckIns = async () => {
  const response = await api.get('/api/v1/portal/my-check-ins');
  return response.data;
};

/**
 * Submete a resposta do cliente para um check-in pendente.
 *
 * @param {string} checkinId
 * @param {Object} data - { weight_kg, body_fat, client_notes, questionnaire, photos }
 */
export const respondToCheckIn = async (checkinId, data) => {
  const response = await api.post(
    `/api/v1/portal/check-ins/${checkinId}/respond`,
    data
  );
  return response.data;
};
