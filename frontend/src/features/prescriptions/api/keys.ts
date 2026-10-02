/** Query keys da feature prescriptions. */
export const prescriptionKeys = {
  training: ['training-plans'] as const,
  trainingList: (clientId: string | null, activity: string, search: string, page: number) =>
    [...prescriptionKeys.training, 'list', clientId, activity, search, page] as const,
  trainingDetail: (id: string) => [...prescriptionKeys.training, 'detail', id] as const,

  logsAll: ['exercise-set-logs'] as const,
  logs: (clientId: string, planId: string, page: number) =>
    [...prescriptionKeys.logsAll, clientId, planId, page] as const,

  nutritionClient: (clientId: string) => ['nutrition-client', clientId] as const,
  nutritionAssessment: (clientId: string) => ['nutrition-assessment', clientId] as const,

  meals: ['meal-plans'] as const,
  mealsList: (clientId: string | null, activity: string, search: string, page: number) =>
    [...prescriptionKeys.meals, 'list', clientId, activity, search, page] as const,
  mealsDetail: (id: string) => [...prescriptionKeys.meals, 'detail', id] as const,

  assignments: ['supplement-assignments'] as const,
  assignmentList: (clientId: string | null, activity: string, page: number) =>
    [...prescriptionKeys.assignments, 'list', clientId, activity, page] as const,
};
