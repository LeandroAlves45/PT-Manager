import { formatNumber } from '@/shared/lib/format';

/**
 * Alvo de repetições de uma série, com o intervalo escrito do maior para o menor.
 *
 * `min` é o mínimo/alvo (`planned_reps`) e `max` o topo opcional do intervalo
 * (`planned_reps_max`). Um intervalo de 10 a 12 lê-se «12-10», como o personal trainer o
 * escreve; sem máximo (ou igual ao mínimo) lê-se só o valor, «8». Sem mínimo não há alvo e
 * devolve `''`: um máximo sozinho não é um alvo válido e nunca se mostra.
 */
export function formatRepsTarget(min: number | null | undefined, max?: number | null): string {
  if (min === null || min === undefined) return '';
  if (max === null || max === undefined || max === min) return formatNumber(min);

  return `${formatNumber(max)}-${formatNumber(min)}`;
}
