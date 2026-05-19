/**
 * adminApi.js — chamadas à API de administração da plataforma.
 *
 * Todos os endpoints requerem role="superuser".
 * Qualquer outro role recebe HTTP 403.
 */

import api from './axiosConfig';

/**
 * Métricas globais da plataforma.
 *
 * @returns {Promise<{
 *   total_trainers: number,
 *   active_trainers: number,
 *   trialing_trainers: number,
 *   total_clients: number,
 *   estimated_monthly_revenue_eur: number
 * }>}
 */

export const getPlatformMetrics = async () => {
  const response = await api.get('/api/v1/admin/metrics');
  return response.data;
};

/**
 * Lista todos os Personal Trainers da plataforma com o estado das suas subscrições.
 *
 * @param {string} [statusFilter] - Filtrar por status: active | trialing | past_due | cancelled
 * @returns {Promise<Array<{
 *   user_id, full_name, email, is_active, is_exempt_from_billing,
 *   subscription_status, subscription_tier, active_clients_count,
 *   monthly_eur, trial_end, joined_at
 * }>>}
 */
export const getTrainers = async (statusFilter) => {
  const params = statusFilter ? { status_filter: statusFilter } : {};
  const response = await api.get('/api/v1/admin/trainers', { params });
  return response.data;
};

/**
 * Suspende um Personal Trainer — bloqueia acesso imediatamente (is_active = false).
 *
 * @param {string} trainerId
 * @returns {Promise<{ detail: string }>}
 */
export const suspendTrainer = async (trainerId) => {
  const response = await api.post(
    `/api/v1/admin/trainers/${trainerId}/suspend`
  );
  return response.data;
};

/**
 * Reativa um Personal Trainer suspenso (is_active = true).
 *
 * @param {string} trainerId
 * @returns {Promise<{ detail: string }>}
 */
export const activateTrainer = async (trainerId) => {
  const response = await api.post(
    `/api/v1/admin/trainers/${trainerId}/activate`
  );
  return response.data;
};

/**
 * Concede isenção permanente de billing a um Personal Trainer (free-forever).
 * Equivale a subscrição PRO activa indefinidamente, sem Stripe.
 *
 * @param {string} trainerId
 * @returns {Promise<{ detail: string }>}
 */
export const grantExemption = async (trainerId) => {
  const response = await api.post(
    `/api/v1/admin/trainers/${trainerId}/grant-exemption`
  );
  return response.data;
};

/**
 * Revoga a isenção de billing de um Personal Trainer.
 *
 * @param {string} trainerId
 * @returns {Promise<{ detail: string }>}
 */
export const revokeExemption = async (trainerId) => {
  const response = await api.post(
    `/api/v1/admin/trainers/${trainerId}/revoke-exemption`
  );
  return response.data;
};

// =============================================================================
// CATÁLOGOS GLOBAIS — Exercícios, Alimentos, Suplementos
// =============================================================================
// O admin gere os registos globais (owner_trainer_id / created_by_user_id = NULL).
// Trainers individuais vêem estes registos mas não os podem editar.

// Exercícios

export const getGlobalExercises = async () => {
  const response = await api.get('/api/v1/exercises');
  return response.data.filter((e) => !e.owner_trainer_id);
};

export const createGlobalExercise = async (payload) => {
  const response = await api.post('/api/v1/exercises', {
    ...payload,
    owner_trainer_id: null,
  });
  return response.data;
};

export const updateGlobalExercise = async (id, payload) => {
  const response = await api.put(`/api/v1/exercises/${id}`, payload);
  return response.data;
};

export const deleteGlobalExercise = async (id) => {
  await api.delete(`/api/v1/exercises/${id}`);
};

// Alimentos

export const getGlobalFoods = async () => {
  const response = await api.get('/api/v1/nutrition/foods/');
  return response.data.filter((f) => !f.owner_trainer_id);
};

export const createGlobalFood = async (payload) => {
  const response = await api.post('/api/v1/nutrition/foods/', {
    ...payload,
    owner_trainer_id: null,
  });
  return response.data;
};

export const updateGlobalFood = async (id, payload) => {
  const response = await api.put(`/api/v1/nutrition/foods/${id}/`, payload);
  return response.data;
};

export const deleteGlobalFood = async (id) => {
  await api.delete(`/api/v1/nutrition/foods/${id}/`);
};

// Suplementos

export const getGlobalSupplements = async () => {
  const response = await api.get('/api/v1/supplements');
  return response.data.filter((s) => !s.owner_trainer_id);
};

export const createGlobalSupplement = async (payload) => {
  const response = await api.post('/api/v1/supplements', {
    ...payload,
    owner_trainer_id: null,
  });
  return response.data;
};

export const archiveGlobalSupplement = async (id) => {
  const response = await api.post(`/api/v1/supplements/${id}/archive`);
  return response.data;
};

export const updateGlobalSupplement = async (id, payload) => {
  const response = await api.put(`/api/v1/supplements/${id}`, payload);
  return response.data;
};

export const deleteGlobalSupplement = async (id) => {
  await api.delete(`/api/v1/supplements/${id}`);
};
