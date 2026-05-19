/**
 * inviteApi.js — camada de acesso à API para o fluxo de convite de clientes (AU-09).
 *
 * Três funções distintas:
 *   generateInvite   — Personal Trainer gera o link (requer autenticação)
 *   validateInvite   — valida o token ao carregar /invite/:token (público)
 *   setPasswordViaInvite — cliente define a password (público, devolve JWT)
 *
 * Nota: validateInvite e setPasswordViaInvite NÃO enviam Authorization header
 * porque o cliente ainda não tem sessão. O interceptor em axiosConfig.js
 * adiciona o token automaticamente se existir no localStorage — se não existir,
 * o header é omitido, o que é o comportamento correcto para estes endpoints.
 */

import api from './axiosConfig';

/**
 * Gera um link de convite para um cliente.
 * Requer que o utilizador autenticado seja Personal Trainer com subscrição activa.
 * @param {string} clientId - UUID do cliente
 * @returns {Promise<{invite_link: string, expires_in_days: number}>}
 */
export const generateInvite = async (clientId) => {
  const response = await api.post(
    `/api/v1/clients/${clientId}/generate-invite`
  );
  return response.data;
};

/**
 * Valida um token de convite.
 * Público — não requer autenticação.
 * Chamado ao carregar a página /invite/:token para mostrar o nome do cliente.
 * @param {string} token - Raw token extraído do URL
 * @returns {Promise<{valid: boolean, client_name: string, message: string}>}
 */
export const validateInvite = async (token) => {
  const response = await api.get(`/api/v1/invite/validate/${token}`);
  return response.data;
};

/**
 * Define a password do cliente via link de convite.
 * Público — não requer autenticação.
 * Devolve um JWT para login automático após definição de password.
 * @param {string} token - Raw token extraído do URL
 * @param {string} newPassword - Nova password do cliente
 * @returns {Promise<{access_token: string, role: string, user_id: string, full_name: string}>}
 */
export const setPasswordViaInvite = async (token, newPassword) => {
  const response = await api.post(`/api/v1/invite/set-password/${token}`, {
    newPassword: newPassword,
  });
  return response.data;
};
