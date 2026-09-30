import type { FieldValues, Path, UseFormSetError } from 'react-hook-form';

import { isApiProblem } from '@/shared/api/problem';

/**
 * Códigos de erro da biblioteca (validators e stores do backend) em PT-PT.
 *
 * O backend manda as mensagens de validação em inglês (texto do FluentValidation); só o
 * `code` é estável, por isso a tradução faz-se aqui, por código.
 */
const LIBRARY_ERROR_MESSAGES: Readonly<Record<string, string>> = {
  exercise_name_required: 'Indica o nome do exercício.',
  exercise_name_too_long: 'O nome não pode exceder 255 caracteres.',
  exercise_muscle_groups_too_long: 'Escolhe menos grupos musculares.',
  exercise_muscle_groups_invalid: 'Escolhe os grupos musculares da lista.',
  exercise_equipment_too_long: 'O equipamento não pode exceder 255 caracteres.',
  exercise_difficulty_too_long: 'A dificuldade não pode exceder 50 caracteres.',
  food_name_required: 'Indica o nome do alimento.',
  food_name_too_long: 'O nome não pode exceder 255 caracteres.',
  food_protein_invalid: 'A proteína tem de estar entre 0 e 100 g.',
  food_carbs_invalid: 'Os hidratos têm de estar entre 0 e 100 g.',
  food_fats_invalid: 'A gordura tem de estar entre 0 e 100 g.',
  food_fiber_invalid: 'A fibra tem de estar entre 0 e 100 g.',
  food_macros_total_invalid: 'Proteína, hidratos e gordura não podem somar mais de 100 g.',
  food_default_serving_invalid:
    'A porção tem de ser maior que 0 e até 1000 g, com até duas casas decimais.',
  supplement_name_required: 'Indica o nome do suplemento.',
  supplement_name_too_long: 'O nome não pode exceder 255 caracteres.',
  supplement_unit_required: 'Indica a unidade de medida.',
  supplement_unit_too_long: 'A unidade não pode exceder 50 caracteres.',
  supplement_serving_size_required: 'Indica a dose.',
  supplement_serving_size_too_long: 'A dose não pode exceder 100 caracteres.',
  supplement_timing_required: 'Indica quando tomar.',
  supplement_timing_too_long: 'O momento não pode exceder 255 caracteres.',
  supplement_inactive: 'Este suplemento está arquivado. Reativa-o antes de o editar.',
  global_exercise_read_only: 'Os exercícios globais só podem ser consultados.',
  global_food_read_only: 'Os alimentos globais só podem ser consultados.',
  global_supplement_read_only: 'Os suplementos globais só podem ser consultados.',
  exercise_not_found: 'Este exercício já não existe.',
  food_not_found: 'Este alimento já não existe.',
  supplement_not_found: 'Este suplemento já não existe.',
};

const GENERIC_SAVE_ERROR = 'Não foi possível guardar. Tenta novamente.';

/** Mensagem PT-PT de um código da biblioteca, com a genérica como recurso. */
export function libraryErrorMessage(code: string | null | undefined): string {
  return (code ? LIBRARY_ERROR_MESSAGES[code] : undefined) ?? GENERIC_SAVE_ERROR;
}

/** Leva os erros do Zod para os campos (primeiro segmento do caminho). */
export function applyZodIssues<T extends FieldValues>(
  issues: readonly { readonly path: readonly PropertyKey[]; readonly message: string }[],
  fields: readonly Path<T>[],
  setError: UseFormSetError<T>
): void {
  for (const issue of issues) {
    const field = fields.find((candidate) => candidate === issue.path[0]);
    if (field !== undefined) setError(field, { message: issue.message });
  }
}

/**
 * Leva um erro da API para o formulário: erros de campo (`errors[].field` em PascalCase) vão
 * para o campo correspondente, com a mensagem do código; o resto vai para `root`.
 *
 * @param serverFields Campo do comando no backend → campo do formulário.
 */
export function applyServerErrors<T extends FieldValues>(
  error: unknown,
  serverFields: Readonly<Record<string, Path<T>>>,
  setError: UseFormSetError<T>
): void {
  if (!isApiProblem(error)) {
    setError('root', { message: GENERIC_SAVE_ERROR });
    return;
  }

  let unmapped = !error.hasFieldErrors;
  for (const fieldError of error.fieldErrors) {
    const field = serverFields[fieldError.field];
    if (field === undefined) unmapped = true;
    else setError(field, { message: libraryErrorMessage(fieldError.code) });
  }
  if (unmapped) setError('root', { message: libraryErrorMessage(error.code) });
}
