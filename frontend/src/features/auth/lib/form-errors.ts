import type { FieldPath, FieldValues, UseFormSetError } from 'react-hook-form';

import type { ApiProblem } from '@/shared/api/problem';
import { authFieldMessage } from '@/features/auth/lib/auth-errors';

/**
 * Normaliza o nome de campo devolvido pelo servidor (`FullName`, `full_name`, `fullName`) para
 * uma chave comparável (`fullname`).
 */
function fieldKey(field: string): string {
  return field.replaceAll('_', '').toLowerCase();
}

/**
 * Devolve os erros de validação do servidor aos campos do formulário.
 *
 * @param problem Falha do pedido.
 * @param setError `setError` do react-hook-form.
 * @param fields Campos do formulário por chave normalizada (`{ fullname: 'full_name' }`).
 * @returns `true` quando pelo menos um erro foi para um campo; `false` quando o chamador deve
 *   mostrar a falha como mensagem geral.
 */
export function applyProblemToForm<T extends FieldValues>(
  problem: ApiProblem,
  setError: UseFormSetError<T>,
  fields: Readonly<Record<string, FieldPath<T>>>
): boolean {
  let placed = false;

  for (const fieldError of problem.fieldErrors) {
    const key = fieldKey(fieldError.field);
    const name = Object.hasOwn(fields, key) ? fields[key] : undefined;
    if (name === undefined) continue;

    setError(name, { message: authFieldMessage(fieldError) });
    placed = true;
  }

  return placed;
}
