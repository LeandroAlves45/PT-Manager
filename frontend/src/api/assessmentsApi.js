/**
 * assessmentsApi.js — camada de acesso à API para avaliações e check-ins.
 *
 * Dois domínios cobertos neste ficheiro porque partilham a mesma página
 * e estão conceptualmente ligados (ambos monitorizam o progresso do cliente):
 *
 *   1. InitialAssessment — avaliação completa feita pelo Personal Trainer
 *   2. CheckIn           — progresso periódico pedido pelo Personal Trainer, respondido pelo cliente
 */

import api from './axiosConfig';

// ============================================================
// AVALIAÇÕES INICIAIS (InitialAssessment)
// ============================================================

/**
 * Lista todas as avaliações iniciais de um cliente específico.
 * Ordenadas da mais recente para a mais antiga.
 *
 * @param {string} clientId - UUID do cliente
 * @returns {Promise<Array>} Lista de avaliações com biometria e questionário
 */
export const getAssessmentsByClient = async (clientId) => {
  const response = await api.get(`/api/v1/assessments/client/${clientId}`);
  return response.data;
};

/**
 * Obtém o detalhe de uma avaliação específica.
 *
 * @param {string} assessmentId - UUID da avaliação
 * @returns {Promise<Object>} Detalhe completo da avaliação
 */
export const getAssessment = async (assessmentId) => {
  const response = await api.get(`/api/v1/assessments/${assessmentId}`);
  return response.data;
};

/**
 * Cria uma nova avaliação inicial para um cliente.
 * O backend define automatically assessed_by_trainer_id = current_user.id.
 *
 * @param {Object} data - { client_id, weight_kg?, height_cm?, body_fat?, health_questionnaire?, notes? }
 * @returns {Promise<Object>} Avaliação criada
 */
export const createAssessment = async (data) => {
  const response = await api.post('/api/v1/assessments/', data);
  return response.data;
};

/**
 * Actualiza os campos de uma avaliação existente (PATCH — apenas campos enviados).
 *
 * @param {string} assessmentId - UUID da avaliação
 * @param {Object} data         - Campos a atualizar (todos opcionais)
 * @returns {Promise<Object>} Avaliação actualizada
 */
export const updateAssessment = async (assessmentId, data) => {
  const response = await api.patch(
    `/api/v1/assessments/${assessmentId}/`,
    data
  );
  return response.data;
};

// ============================================================
// CHECK-INS
// ============================================================

/**
 * Lista todos os check-ins de um cliente, do mais recente para o mais antigo.
 *
 * @param {string} clientId - UUID do cliente
 * @returns {Promise<Array>} Lista de check-ins com estado e respostas
 */
export const getCheckinsByClient = async (clientId) => {
  const response = await api.get(`/api/v1/check-ins/client/${clientId}`);
  return response.data;
};

/**
 * Cria um novo pedido de check-in para um cliente.
 * O cliente recebe um alerta visual na sua dashboard.
 *
 * @param {string} clientId - UUID do cliente
 * @returns {Promise<Object>} Check-in criado com status="pending"
 */
export const createCheckin = async (clientId, targetDate = null) => {
  const response = await api.post('/api/v1/check-ins/', {
    client_id: clientId,
    ...(targetDate ? { target_date: targetDate } : {}),
  });
  return response.data;
};

/**
 * Trainer adiciona notas internas a um check-in respondido.
 * As notas não são visíveis ao cliente.
 *
 * @param {string} checkinId    - UUID do check-in
 * @param {string} trainerNotes - Texto das notas
 * @returns {Promise<Object>} Check-in actualizado
 */
export const addCheckinNotes = async (checkinId, trainerNotes) => {
  const response = await api.patch(
    `/api/v1/check-ins/${checkinId}/trainer-notes`,
    { trainer_notes: trainerNotes }
  );
  return response.data;
};

/**
 * Trainer ignora um check-in pendente (status passa para "skipped").
 * Útil quando o check-in ficou desactualizado.
 *
 * @param {string} checkinId - UUID do check-in
 * @returns {Promise<Object>} Check-in com status="skipped"
 */
export const skipCheckin = async (checkinId) => {
  const response = await api.post(`/api/v1/check-ins/${checkinId}/skip`);
  return response.data;
};
