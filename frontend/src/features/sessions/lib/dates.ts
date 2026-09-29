import { addDays, format, formatISO, isValid, parseISO } from 'date-fns';

/**
 * Datas da agenda no fuso do browser.
 *
 * O backend guarda `starts_at` como instante (DateTimeOffset) e só usa o fuso do trainer
 * para a regra "uma sessão por cliente por dia". Os pedidos levam sempre o offset local
 * (`formatISO`), nunca um "Z" inventado.
 */

/** Hoje, no formato do `<input type="date">`. */
export function todayKey(now: Date = new Date()): string {
  return format(now, 'yyyy-MM-dd');
}

/** Junta "2026-09-27" e "10:30" num instante ISO com o offset local. */
export function toStartsAt(date: string, time: string): string {
  return formatISO(parseISO(`${date}T${time}`));
}

/** Instante local de "2026-09-27" + "10:30", ou 'null' se a data ou a hora forem inválidas. */
export function localInstant(date: string, time: string): Date | null {
  if (date === '' || time === '') return null;
  const value = parseISO(`${date}T${time}`);
  return isValid(value) ? value : null;
}

/** Separa um 'starts_at' em data e hora locais, para pré-preencher "Reagendar". */
export function splitStartsAt(startsAt: string): { date: string; time: string } {
  const value = parseISO(startsAt);
  return { date: format(value, 'yyyy-MM-dd'), time: format(value, 'HH:mm') };
}

/** Janela (início do dia, início do dia seguinte) de um dia local, em ISO com offset. */
export function dayRange(day: string): { startsFrom: string; startsBefore: string } {
  const start = parseISO(day);
  return { startsFrom: formatISO(start), startsBefore: formatISO(addDays(start, 1)) };
}

/** Dia seguinte ou anterior de "yyyy-MM-dd". */
export function shiftDay(day: string, amount: number): string {
  return format(addDays(parseISO(day), amount), 'yyyy-MM-dd');
}
