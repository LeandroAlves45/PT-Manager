import { format, parseISO } from 'date-fns';
import { pt } from 'date-fns/locale';

/**
 * Dias da semana do plano de treino. O backend usa 0 = segunda … 6 = domingo
 * (`Domain/Services/TrainingPlanSchedule.cs`), não a convenção do JavaScript (0 = domingo).
 */
const WEEKDAYS = ['Segunda', 'Terça', 'Quarta', 'Quinta', 'Sexta', 'Sábado', 'Domingo'] as const;

/** Nome do dia do plano (`day_of_week` 0–6). Fora do intervalo devolve `null`. */
export function weekdayLabel(dayOfWeek: number): string | null {
  return WEEKDAYS[dayOfWeek] ?? null;
}

/**
 * Título de um dia do plano: "Semana 3 · Terça". O modelo não tem nome de treino
 * ("Empurrar A"), por isso o título sai da semana e do dia.
 */
export function planDayTitle(weekNumber: number, dayOfWeek: number): string {
  const weekday = weekdayLabel(dayOfWeek);
  return weekday === null ? `Semana ${weekNumber}` : `Semana ${weekNumber} · ${weekday}`;
}

/**
 * Data local da API ("2026-10-04") como "Domingo, 04/10/2026".
 *
 * A data vem já no fuso do personal trainer. `parseISO` de uma data sem hora usa a meia-noite local
 * do browser e o formato só lê dia, mês e ano, por isso não há deslocamento de fuso.
 */
export function longLocalDate(localDate: string): string {
  const text = format(parseISO(localDate), 'EEEE, dd/MM/yyyy', { locale: pt });
  return text.charAt(0).toUpperCase() + text.slice(1);
}

/** Data local curta para cartões: "quinta-feira, 08/10". */
export function shortLocalDate(localDate: string): string {
  return format(parseISO(localDate), 'EEEE, dd/MM', { locale: pt });
}

/**
 * Rótulo de um dia passado no seletor do treino: «Qui, 01/10 · Semana 1».
 *
 * O dia da semana sai da própria data local da API, não do relógio do browser.
 */
export function pastDayOptionLabel(localDate: string, weekNumber: number): string {
  const date = parseISO(localDate);
  const weekday = format(date, 'EEE', { locale: pt });
  return `${weekday.charAt(0).toUpperCase()}${weekday.slice(1)}, ${format(date, 'dd/MM')} · Semana ${weekNumber}`;
}
