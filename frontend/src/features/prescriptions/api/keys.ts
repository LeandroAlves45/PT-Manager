/**
 * Query keys da feature prescriptions. As chaves de cada recurso começam pelo prefixo do
 * recurso (`training`, `logsAll`, `meals`, `assignments`), para que uma escrita invalide
 * a lista e o detalhe com um único `invalidateQueries`.
 */
export const prescriptionKeys = {
  /** Prefixo de listas e detalhes de planos de treino. */
  training: ['training-plans'] as const,
  /** Lista paginada; `clientId` a `null` é a lista global do tenant. */
  trainingList: (clientId: string | null, activity: string, search: string, page: number) =>
    [...prescriptionKeys.training, 'list', clientId, activity, search, page] as const,
  /** Detalhe com estrutura e `has_history`; também invalidado ao registar uma série. */
  trainingDetail: (id: string) => [...prescriptionKeys.training, 'detail', id] as const,

  /** Prefixo das páginas de séries realizadas, invalidado após registo ou correção. */
  logsAll: ['exercise-set-logs'] as const,
  /** Página de séries de um plano de um cliente. */
  logs: (clientId: string, planId: string, page: number) =>
    [...prescriptionKeys.logsAll, clientId, planId, page] as const,

  /** Ficha do cliente, usada só para sugerir sexo e idade num plano alimentar novo. */
  nutritionClient: (clientId: string) => ['nutrition-client', clientId] as const,
  /** Avaliação inicial (`null` se 404), usada para sugerir medidas num plano novo. */
  nutritionAssessment: (clientId: string) => ['nutrition-assessment', clientId] as const,

  /** Prefixo de listas e detalhes de planos alimentares. */
  meals: ['meal-plans'] as const,
  /** Lista paginada; `clientId` a `null` é a lista global do tenant. */
  mealsList: (clientId: string | null, activity: string, search: string, page: number) =>
    [...prescriptionKeys.meals, 'list', clientId, activity, search, page] as const,
  /** Detalhe com cálculo, refeições, itens e suplementos por refeição. */
  mealsDetail: (id: string) => [...prescriptionKeys.meals, 'detail', id] as const,

  /** Prefixo das atribuições diretas de suplementos. */
  assignments: ['supplement-assignments'] as const,
  /** Lista paginada; `clientId` a `null` é a lista global. */
  assignmentList: (clientId: string | null, activity: string, page: number) =>
    [...prescriptionKeys.assignments, 'list', clientId, activity, page] as const,
};
