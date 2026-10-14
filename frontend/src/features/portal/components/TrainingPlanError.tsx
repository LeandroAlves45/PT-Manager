import { Dumbbell } from 'lucide-react';

import { isApiProblem } from '@/shared/api/problem';
import { EmptyState } from '@/shared/components/EmptyState';
import { ErrorState } from '@/shared/components/ErrorState';

/**
 * Falha da leitura do treino de hoje ou do plano.
 *
 * O 404 `portal_training_plan_not_available` é um estado vazio, não um erro: tentar de novo
 * não o resolveria. O backend responde com o mesmo código a uma ficha arquivada. O resto é uma
 * falha técnica com "Tentar novamente".
 */
export function TrainingPlanError({
  error,
  onRetry,
}: {
  error: Error | null;
  onRetry: () => void;
}) {
  if (isApiProblem(error) && error.code === 'portal_training_plan_not_available')
    return (
      <EmptyState
        icon={Dumbbell}
        title="Sem plano de treino"
        description="O teu personal trainer ainda não atribuiu um plano."
      />
    );

  return <ErrorState error={error} onRetry={onRetry} />;
}
