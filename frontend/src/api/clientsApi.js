/**
 * clientsApi.js — camada de acesso à API para gestão de clientes.
 *
 * Todas as funções recebem dados, chamam o Axios e devolvem response.data.
 * Nunca fazem gestão de estado — isso é responsabilidade dos hooks (useClients).
 * Nunca tratam erros de UI — o interceptor de resposta em axiosConfig.js
 * trata os logs; o componente trata a mensagem ao utilizador via toast.
 */

import api from './axiosConfig';

// == CLIENTES ==

/**
 *  Lista todos os clientes com filtros opcionais
 * @param {Object} params - Parâmetros de filtro
 * @param {string} params.Client_id - ID do cliente (opcional)
 * @param {number} params.Status - 1=ativos, 2=arquivados (opcional)
 * @param {number} params.Page_size - Tamanho da página (opcional)
 * @param {number} params.Page_number - Número da página (opcional)
 * @returns {Promise<Array>} Lista de clientes
 */
export const getClients = async (params = {}) => {
  const response = await api.get('/api/v1/clients', { params });
  return response.data;
};

/**
 * Busca um único cliente pelo ID
 * @param {string} clientId - ID do cliente
 * @returns {Promise<Object>} Dados do cliente
 */
export const getClient = async (clientId) => {
  const response = await api.get(`/api/v1/clients/${clientId}`);
  return response.data;
};

/**
 * * Cria um novo cliente
 * @param {Object} clientData - Dados do cliente
 * @param {string} clientData.full_name - Nome completo (obrigatório)
 * @param {string} clientData.phone - Telefone (obrigatório, único)
 * @param {string} [clientData.email] - Email (opcional, único)
 * @param {string} [clientData.birth_date] - Data nascimento formato YYYY-MM-DD (opcional)
 * @param {string} [clientData.sex] - Sexo: 'M', 'F', 'Other' (opcional)
 * @param {number} [clientData.height_cm] - Altura em cm (opcional)
 * @param {string} [clientData.objetive] - Objetivo (opcional)
 * @param {string} [clientData.notes] - Notas (opcional)
 * @param {string} [clientData.emergency_contact_name] - Nome contacto emergência (opcional)
 * @param {string} [clientData.emergency_contact_phone] - Telefone contacto emergência (opcional)
 * @returns {Promise<Object>} Cliente criado
 */
export const createClient = async (clientData) => {
  const response = await api.post('/api/v1/clients', clientData);
  return response.data;
};

/**
 * * Atualiza um cliente existente
 * @param {string} clientId - ID do cliente a ser atualizado
 * @param {Object} clientData - Campos a atualizar (apenas os que mudaram)
 * @returns {Promise<Object>} Cliente atualizado
 *
 */

export const updateClient = async (clientId, clientData) => {
  const response = await api.patch(`/api/v1/clients/${clientId}`, clientData);
  return response.data;
};

/** * * Arquiva um cliente (soft delete)
 * @param {string} clientId - ID
 * @return {Promise<Object>} Cliente arquivado
 */
export const archiveClient = async (clientId) => {
  const response = await api.post(`/api/v1/clients/${clientId}/archive`);
  return response.data;
};

/** * * Restaura um cliente arquivado
 * @param {string} clientId - ID
 * @return {Promise<Object>} Cliente reativado */

export const unarchiveClient = async (clientId) => {
  const response = await api.post(`/api/v1/clients/${clientId}/unarchive`);
  return response.data;
};

/**
 * Remove cliente
 * @param {string} clientId - ID do cliente a ser removido
 * @param {boolean} [hard=false] - false=arquivar, true=remover permanentemente
 * @returns {Promise<void>} Cliente removido
 */
export const deleteClient = async (clientId, hard = false) => {
  const response = await api.delete(`/api/v1/clients/${clientId}`, {
    params: { hard },
  });
  return response.data;
};
