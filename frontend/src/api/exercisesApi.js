import api from './axiosConfig';

// == EXERCÍCIOS ==

/**
 * Lista todos os exercícios com filtros opcionais
 *
 * @param {Object} params - Parâmetros de filtro
 * @param {string} [params.q] - pesquisa por nome
 * @param {boolean} [params.only_active] - só exercícios ativos
 * @param {number} [params.page_size] - tamanho da página
 * @param {number} [params.page_number] - número da página
 * @returns {Promise<Array>} Lista de exercícios
 */
export const getExercises = async (params = {}) => {
  const response = await api.get('/api/v1/exercises/', { params });
  return response.data;
};

/**
 * Busca um exercício específico por ID.
 *
 * @param {string} id - ID do exercício
 * @returns {Promise<Object>} Detalhes do exercício
 */

export const getExercise = async (id) => {
  const response = await api.get(`/api/v1/exercises/${id}/`);
  return response.data;
};

/**
 * Cria um novo exercício
 *
 * @param {Object} data - Dados do exercício {name, muscles...}
 * @returns {Promise<Object>} Exercício criado
 */
export const createExercise = async (data) => {
  const response = await api.post('/api/v1/exercises/', data);
  return response.data;
};

/**
 * Atualiza um exercício existente
 *
 * @param {string} exercise_id - ID do exercício
 * @param {Object} data - Dados do exercício a atualizar (mesmos campos de createExercise)
 * @returns {Promise<Object>} Exercício atualizado
 */
export const updateExercise = async (exercise_id, data) => {
  const response = await api.put(`/api/v1/exercises/${exercise_id}/`, data);
  return response.data;
};

/**
 * Remove um exercício.
 *
 * @param {string} exerciseId - ID do exercício
 * @returns {Promise<void>}
 */
export const deleteExercise = async (exerciseId) => {
  await api.delete(`/api/v1/exercises/${exerciseId}/`);
};
