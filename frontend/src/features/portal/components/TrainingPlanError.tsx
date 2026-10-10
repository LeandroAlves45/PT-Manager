import { Dumbbell, UserX } from 'lucide-react';

import { isApiProblem } from '@/shared/api/problem';
import { EmptyState } from '@/shared/components/EmptyState';
import { ErrorState } from '@/shared/components/ErrorState';

/**
 * Falha da leitura do treino de hoje ou do plano.
 *
 * Os dois 404 de negócio são estados vazios, não erros: sem plano ativo
 * (`portal_training_plan_not_available`) e ficha arquivada
 * (`portal_profile_not_available`, igual ao Início). Tentar de novo não os resolveria. O resto
 * é uma falha técnica com "Tentar novamente".
 */
export function TrainingPlanError({ error, onRetry }: { error: Error; onRetry: () => void }) {
  if (isApiProblem(error) && error.code === 'portal_training_plan_not_available')
    return (
      <EmptyState
        icon={Dumbbell}
        title="Sem plano de treino"
        description="O teu personal trainer ainda não atribuiu um plano."
      />
    );

  if (isApiProblem(error) && error.code === 'portal_profile_not_available')
    return (
      <EmptyState
        icon={UserX}
        title="Portal indisponível"
        description="A tua ficha não está ativa. Fala com o teu personal trainer."
      />
    );

  return <ErrorState error={error} onRetry={onRetry} />;
}
