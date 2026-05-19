import { useState, useEffect, useCallback } from 'react';
import { getSessions } from '../api/sessionApi';

/**
 * Hook para gerir a lista de sessões de treino
 * Mesmo padrão de useClients
 * @param {Object} filters - Filtros (client_id, limit)
 * @returns {Object} { sessions, loading, error, refreshSessions }
 */
export function useSessions(filters) {
  const [sessions, setSessions] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  const fetchSessions = useCallback(async () => {
    setLoading(true);
    setError(null);

    try {
      const data = await getSessions(filters);
      setSessions(data);
    } catch (err) {
      setError(err.response?.data?.message || 'Erro ao carregar sessões');
      console.error('Erro ao buscar sessões:', err);
    } finally {
      setLoading(false);
    }
  }, [JSON.stringify(filters)]);

  useEffect(() => {
    fetchSessions();
  }, [fetchSessions]);

  return { sessions, loading, error, refreshSessions: fetchSessions };
}
