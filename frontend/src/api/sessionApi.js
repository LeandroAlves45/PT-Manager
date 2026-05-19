import api from './axiosConfig';

// == SESSÕES DE TREINO ==

/**
 * Lista todas as sessões de treino com filtros opcionais
 * @param {Object} params - Parâmetros de filtro
 * @param {string} [params.client_id] - filtra por cliente
 * @param {number} [params.limit] - número máximo de sessões a retornar, max:200
 * @return {Promise<Array>} Lista de sessões de treino
 */
export const getSessions = async (params = {}) => {
  const response = await api.get('/api/v1/sessions', { params });
  return response.data;
};

/**
 * Agenda uma nova sessão de treino
 * @param {string} clientId - ID do cliente
 * @param {Object} sessionData - Dados da sessão {date, time, pack_id, notes}
 * @param {string} sessionData.starts_at - Data da sessão (ISO Datetime)
 * @param {number} sessionData.duration_minutes - Duração da sessão em minutos
 * @param {string} [session.Data.location] - Local da sessão (opcional)
 * @param {string} [sessionData.notes] - Notas adicionais (opcional)
 * @returns {Promise<Object>} Sessão de treino criada
 */
export const createSession = async (clientId, sessionData) => {
  const response = await api.post(
    `/api/v1/sessions/clients/${clientId}`,
    sessionData
  );
  return response.data;
};

/**
 * Atualiza uma sessão de treino existente
 * @param {string} sessionId - ID da sessão a ser atualizada
 * @param {Object} sessionData - Dados da sessão a serem atualizados (mesmos campos de createSession)
 * @returns {Promise<Object>} Sessão de treino atualizada
 */
export const updateSession = async (sessionId, sessionData) => {
  const response = await api.put(`/api/v1/sessions/${sessionId}`, sessionData);
  return response.data;
};

/**
 * Marca uma sessão de treino como realizada (consome 1 sessão do pack)
 * @param {string} sessionId - ID da sessão a ser marcada como realizada
 * @returns {Promise<Object>} Sessão de treino atualizada
 */
export const completeSession = async (sessionId) => {
  const response = await api.post(`/api/v1/sessions/${sessionId}/complete`);
  return response.data;
};

/**
 * Cancela uma sessão de treino agendada, não consome sessão do pack
 * @param {string} sessionId - ID da sessão a ser cancelada
 * @returns {Promise<Object>} Sessão de treino atualizada com status "missed"
 */
export const markSessionMissed = async (sessionId) => {
  const response = await api.post(`/api/v1/sessions/${sessionId}/missed`);
  return response.data;
};

/**
 * Cancela uma sessão de treino agendada, não consome sessão do pack
 * @param {string} sessionId - ID da sessão a ser cancelada
 * @returns {Promise<Object>} Sessão de treino atualizada com status "cancelled"
 */
export const cancelSession = async (sessionId) => {
  const response = await api.post(`/api/v1/sessions/${sessionId}/cancel`);
  return response.data;
};
