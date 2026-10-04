import { addDays, format } from 'date-fns';

/**
 * Dias dos check-ins em `yyyy-MM-dd`, o formato do `<input type="date">` e do contrato
 * (`DateOnly`). Um check-in é um dia, não um instante: nunca há conversão de fuso.
 *
 * Duplica de propósito o `todayKey`: importar um helper interno de outra feature criaria
 * uma dependência só por isto.
 */

/** Hoje, no dia do browser. */
export function todayKey(now: Date = new Date()): string {
  return format(now, 'yyyy-MM-dd');
}

/** Amanhã, no dia do browser: o primeiro dia aceite para reagendar. */
export function tomorrowKey(now: Date = new Date()): string {
  return format(addDays(now, 1), 'yyyy-MM-dd');
}
