import api from './axiosConfig';

// == TIPOS DE PACKS ==

/**
 * Lista todos os tipos de packs
 *@returns {Promise<Array>} Lista de tipos de packs
 */
export const getPackTypes = async () => {
  const response = await api.get('/api/v1/pack-types');
  return response.data;
};

/**
 * Cria um novo tipo de pack
 * @param {Object} data - Dados do tipo de pack {name , sessions_total}
 * @returns {Promise<Object>} Tipo de pack criado
 */
export const createPackType = async (data) => {
  const response = await api.post('/api/v1/pack-types', data);
  return response.data;
};

/**
 * Atualiza um tipo de pack existente
 * @param {string} id - ID do tipo de pack
 * @param {Object} data - Dados do tipo de pack a atualizar (mesmos campos de createPackType)
 * @returns {Promise<Object>} Tipo de pack atualizado
 */
export const updatePackType = async (id, data) => {
  const response = await api.put(`/api/v1/pack-types/${id}`, data);
  return response.data;
};

/**
 * Elimina um tipo de pack
 * @param {string} id - ID do tipo de pack a ser eliminado
 * @returns {Promise<void>} Tipo de pack eliminado
 */
export const deletePackType = async (id) => {
  await api.delete(`/api/v1/pack-types/${id}`);
};
