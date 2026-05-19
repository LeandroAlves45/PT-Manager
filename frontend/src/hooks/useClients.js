import { useState, useEffect, useCallback } from 'react';
import { getClients } from '../api/clientsApi';

/**
 * Hook para gerenciar clientes
 * @param {Object} filters - Filtros para busca de clientes
 * @return {Object} Estado e funções para manipular clientes
 */

export const useClients = (filters = {}) => {
  const [clients, setClients] = useState([]); //lista de clientes
  const [loading, setLoading] = useState(true); // estado de carregamento
  const [error, setError] = useState(null); // estado de erro

  /**
   * useCallback memoriza a função para não ser recriada em cada render.
   * Isso é importante para evitar loops infinitos no useEffect que depende dessa função.
   */
  const fetchClients = useCallback(async () => {
    setLoading(true);
    setError(null);

    try {
      const data = await getClients(filters);
      setClients(data);
    } catch (err) {
      setError(err.response?.data?.detail || 'Erro ao carregar clientes');
      console.error('Erro ao carregar clientes:', err);
    } finally {
      setLoading(false);
    }
  }, [JSON.stringify(filters)]); // Reexecuta quando os filtros mudarem

  // Carrega clientes quando o componente é montado ou quando os filtros mudam
  useEffect(() => {
    fetchClients();
  }, [fetchClients]); // Dependência: função memoizada

  return {
    clients, //Lista de clientes
    loading, //true enquanto carrega
    error, //mensagem de erro
    refetch: fetchClients,
  }; //função para recarregar clientes
};
