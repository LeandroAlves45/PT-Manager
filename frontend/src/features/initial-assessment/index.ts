/**
 * API pública da feature initial-assessment: regras e campos partilhados pelo formulário do
 * personal trainer e pelo preenchimento do cliente, e a lista de avaliações por rever.
 * A página da lista entra no router pelo caminho (rota lazy).
 */
export {
  initialAssessmentKeys,
  usePendingInitialAssessmentsQuery,
} from '@/features/initial-assessment/api/pendingReview';
export {
  BodyAndGoalsFields,
  HabitsFields,
  MeasurementsFields,
} from '@/features/initial-assessment/components/AssessmentFields';
export {
  ASSESSMENT_STEPS,
  assessmentDefaults,
  assessmentIssues,
  assessmentSchema,
  emptyAssessmentValues,
  serverAssessmentIssues,
  toAssessmentRequest,
  toNumber,
  SERVER_FIELDS,
  type AssessmentField,
  type AssessmentIssue,
  type AssessmentRequest,
  type AssessmentValues,
} from '@/features/initial-assessment/lib/initialAssessmentSchema';
