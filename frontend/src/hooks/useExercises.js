import { useState, useEffect } from 'react';
import { getExercises } from '@/api/exercisesApi';

export const useExercises = (filters = {}) => {
  const [exercises, setExercises] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  const fetchExercises = async () => {
    try {
      setLoading(true);
      const data = await getExercises(filters);
      setExercises(data);
      setError(null);
    } catch (err) {
      setError(err.message || 'Erro ao carregar exercícios');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchExercises();
  }, [JSON.stringify(filters)]); // Reexecuta quando os filtros mudarem

  return { exercises, loading, error, refetch: fetchExercises };
};
