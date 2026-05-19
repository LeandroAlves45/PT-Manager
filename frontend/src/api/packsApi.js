import api from './axiosConfig';

// == PACKS ==

/**
 * Compra um pack para um cliente
 * Cria uma nova compra de pack para um cliente específico. Com snapshot dos dados do cliente e do pack no momento da compra.
 * @param {string} clientId - ID do cliente
 * @param {Object} data - Dados da compra do pack
 * @param {string} data.pack_type_id - ID do pack comprado (obrigatório)
 * @returns {Promise<Object>} Detalhes da compra do pack
 */
export const purchasePack = async (clientId, data) => {
    const response = await api.post(`/api/v1/packs/clients/${clientId}/purchase`, data);
    return response.data;
};

/**
 * Lista compras de packs de um cliente
 * Ordenados por data de compra (mais recente primeiro)
 * @param {string} clientId - ID do cliente
 * @returns {Promise<Array>} Lista de compras de packs
 */
export const getClientPacks = async (clientId) => {
    const response = await api.get(`/api/v1/packs/clients/${clientId}/purchases`);
    return response.data;
};

/**
 * Obtém o pack ativo de um cliente (se houver)
 * Um pack ativo é um pack que ainda tem sessões disponíveis e não expirou
 * 
 * @param {string} clientId - ID do cliente
 * @return {Promise<Object|null>} Detalhes do pack ativo ou null se não houver
 */
export const getActivePack = async (clientId) => {
    const response = await api.get(`/api/v1/packs/clients/${clientId}/active`);
    return response.data;
};
 
