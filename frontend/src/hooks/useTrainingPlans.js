import { useState, useEffect } from 'react';
import { getTrainingPlans } from '@/api/trainingPlan';

export const useTrainingPlans = (filters = {}) => {
  const [plans, setPlans] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  const fetchPlans = async () => {
    try {
      setLoading(true);
      const data = await getTrainingPlans(filters);
      setPlans(data);
      setError(null);
    } catch (err) {
      setError(err.message || 'Erro ao carregar planos de treino');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchPlans();
  }, [JSON.stringify(filters)]); // Reexecuta quando os filtros mudarem

  return { plans, loading, error, refetch: fetchPlans };
};
