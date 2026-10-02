import { isApiProblem } from '@/shared/api/problem';

/** Traduz códigos estáveis do servidor sem mostrar mensagens internas em inglês. */
const MESSAGES: Readonly<Record<string, string>> = {
  training_structure_has_history:
    'Este plano já tem histórico. A estrutura e as datas não podem mudar.',
  active_training_plan_conflict: 'O cliente já tem um plano de treino ativo.',
  training_structure_reference_not_found: 'A estrutura mudou. Reabre o plano e tenta novamente.',
  meal_plan_active_conflict: 'O cliente já tem um plano alimentar ativo.',
  supplement_assignment_already_exists: 'Este suplemento já foi atribuído ao cliente.',
  supplement_assignment_inactive: 'Reativa a atribuição antes de a editar.',
  rate_limit_exceeded: 'Foram feitos demasiados pedidos. Aguarda e tenta novamente.',
};

/** Mensagem adequada para falhas de prescrição esperadas. */
export function prescriptionError(error: unknown): string {
  if (isApiProblem(error))
    return MESSAGES[error.code] ?? 'Não foi possível guardar. Revê os dados.';

  return 'Não foi possível contactar o servidor. Tenta novamente.';
}
