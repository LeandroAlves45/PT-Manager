/**
 * supplementsApi.js — camada de acesso à API para suplementos e atribuições.
 *
 * Dois grupos de funções:
 *   1. Catálogo de suplementos (CRUD — Personal Trainer)
 *   2. Atribuição a clientes (assign/update/remove — Personal Trainer)
 *   3. Portal do cliente (read-only)
 *
 * Convenção do projecto:
 *   - Todas as funções devolvem response.data directamente
 *   - Nunca gerem estado — isso é responsabilidade dos componentes
 *   - Erros de rede são tratados pelo interceptor em axiosConfig.js
 */

import api from './axiosConfig';

// ============================================================
// CATÁLOGO DE SUPLEMENTOS (Personal Trainer)
// ============================================================

/**
 * Lista suplementos do Personal Trainer autenticado.
 * @param {boolean} includeArchived - Se true, inclui suplementos arquivados
 */

export const getSupplements = async (includeArchived = false) => {
  const response = await api.get('/api/v1/supplements', {
    params: { include_archived: includeArchived },
  });
  return response.data;
};

/**
 * Cria um novo suplemento no catálogo do Personal Trainer.
 * @param {Object} data - { name, description?, serving_size?, timing?, trainer_notes? }
 */
export const createSupplement = async (data) => {
  const response = await api.post('/api/v1/supplements', data);
  return response.data;
};

/**
 * Actualiza um suplemento (PATCH — apenas campos enviados são alterados).
 * @param {string} supplementId - UUID do suplemento
 * @param {Object} data - Campos a atualizar (todos opcionais)
 */
export const updateSupplement = async (supplementId, data) => {
  const response = await api.patch(`/api/v1/supplements/${supplementId}`, data);
  return response.data;
};

/**
 * Arquiva um suplemento (soft delete — desaparece para clientes mas histórico mantém-se).
 * @param {string} supplementId - UUID do suplemento
 */
export const archiveSupplement = async (supplementId) => {
  const response = await api.post(
    `/api/v1/supplements/${supplementId}/archive`
  );
  return response.data;
};

/**
 * Reactiva um suplemento arquivado.
 * @param {string} supplementId - UUID do suplemento
 */
export const unarchiveSupplement = async (supplementId) => {
  const response = await api.post(
    `/api/v1/supplements/${supplementId}/unarchive`
  );
  return response.data;
};

/**
 * Apaga permanentemente um suplemento.
 * @param {string} supplementId - UUID do suplemento
 */

export const deleteSupplement = async (supplementId) => {
  await api.delete(`/api/v1/supplements/${supplementId}`);
};

// ============================================================
// ATRIBUIÇÃO A CLIENTES
// ============================================================

/**
 * Lista os suplementos atribuídos a um cliente específico.
 * A resposta inclui os dados do suplemento expandidos (nome, descrição, etc.).
 * @param {string} clientId - UUID do cliente
 */

export const getClientSupplements = async (clientId) => {
  const response = await api.get(`/api/v1/clients/${clientId}/supplements`);
  return response.data;
};

/**
 * Atribui um suplemento a um cliente.
 * @param {string} clientId - UUID do cliente
 * @param {Object} data - { supplement_id, dose?, timing_notes?, notes? }
 */
export const assignSupplement = async (clientId, data) => {
  const response = await api.post(
    `/api/v1/clients/${clientId}/supplements`,
    data
  );
  return response.data;
};

/**
 * Actualiza a dose/timing/notas de uma atribuição existente.
 * @param {string} clientId - UUID do cliente
 * @param {string} assignmentId - UUID da atribuição
 * @param {Object} data - { dose?, timing_notes?, notes? }
 */
export const updateClientSupplement = async (clientId, assignmentId, data) => {
  const response = await api.patch(
    `/api/v1/clients/${clientId}/supplements/${assignmentId}`,
    data
  );
  return response.data;
};

/**
 * Remove a atribuição de um suplemento a um cliente.
 * O suplemento continua no catálogo — apenas a atribuição é removida.
 * @param {string} clientId - UUID do cliente
 * @param {string} assignmentId - UUID da atribuição
 */
export const removeClientAssignment = async (clientId, assignmentId) => {
  await api.delete(`/api/v1/clients/${clientId}/supplements/${assignmentId}`);
};

// ============================================================
// PORTAL DO CLIENTE
// ============================================================

/**
 * Devolve os suplementos atribuídos ao cliente autenticado.
 * Usado no portal do cliente — o JWT identifica o cliente automaticamente.
 * Não inclui trainer_notes (campo interno do Personal Trainer).
 */
export const getMySupplements = async () => {
  const response = await api.get('/api/v1/portal/my-supplements');
  return response.data;
};
