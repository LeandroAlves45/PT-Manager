/**
 * authApi.js — chamadas de autenticação e registo.
 *
 * Login não está aqui — é feito directamente no AuthContext
 * porque precisa de coordenar token, branding e redirect.
 * Este ficheiro trata do signup público do Personal Trainer.
 */

import api from './axiosConfig';

/**
 * Regista um novo Personal Trainer na plataforma.
 *
 * Cria o utilizador, a subscrição Stripe em trial (15 dias),
 * e devolve um JWT para login imediato.
 *
 * @param {Object} data
 * @param {string} data.full_name  - Nome completo (mín. 2 caracteres)
 * @param {string} data.email      - Email único na plataforma
 * @param {string} data.password   - Password (mín. 8 caracteres)
 * @returns {Promise<{access_token, user_id, full_name, checkout_url, trial_end, message}>}
 */
export const signupTrainer = async (data) => {
  const response = await api.post('/api/v1/signup/trainer', data);
  return response.data;
};

/**
 * Cria uma conta de utilizador para um cliente existente.
 * Chamado após createClient para que o fluxo de convite funcione.
 *
 * @param {Object} data
 * @param {string} data.email      - Email do cliente
 * @param {string} data.password   - Password temporária (substituída via convite)
 * @param {string} data.full_name  - Nome completo do cliente
 * @param {string} data.role       - "client"
 * @param {string} data.client_id  - UUID do cliente criado
 * @returns {Promise<Object>}
 */
export const createUser = async (data) => {
  const response = await api.post('/api/v1/auth/users', data);
  return response.data;
};
