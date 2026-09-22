import { format, formatDistanceToNowStrict, parseISO } from 'date-fns';
import { pt } from 'date-fns/locale';

/**
 * Formatação de datas e números para PT-PT.
 *
 * O backend envia datas em ISO 8601 (UTC para instantes, `yyyy-MM-dd` para dias locais do
 * personal trainer). Nunca reinterpretar o fuso no cliente: o servidor já decidiu qual é o dia.
 */

/** Converte uma data ISO do backend num 'Date', tolerando 'null'. */
function toDate(value: string | Date): Date {
  return value instanceof Date ? value : parseISO(value);
}

/** Data curta: '14/09/2026'.*/
export function formatDate(value: string | Date): string {
  return format(toDate(value), 'dd/MM/yyyy', { locale: pt });
}

/** Data e hora: '14/09/2026 10:00'. */
export function formatDateTime(value: string | Date): string {
  return format(toDate(value), 'dd/MM/yyyy HH:mm', { locale: pt });
}

/** Distância relativa ao momento atual: 'há 2 dias'. */
export function formatRelative(value: string | Date): string {
  return formatDistanceToNowStrict(toDate(value), { locale: pt, addSuffix: true });
}

/**
 * Número com separadores portugueses e casas decimais fixas.
 *
 * @param value Valor a formatar.
 * @param fractionDigits Casas decimais; por omissão nenhuma.
 */
export function formatNumber(value: number, fractionDigits = 0): string {
  return new Intl.NumberFormat('pt-PT', {
    minimumFractionDigits: fractionDigits,
    maximumFractionDigits: fractionDigits,
  }).format(value);
}

/**
 * Valor monetário a partir dos cêntimos que o backend guarda em `PriceCents`.
 *
 * @param cents Montante em cêntimos.
 * @param currency Código ISO 4217 devolvido pela API (ex.: `EUR`).
 */
export function formatCurrency(cents: number, currency: string): string {
  return new Intl.NumberFormat('pt-PT', {
    style: 'currency',
    currency: currency.toUpperCase(),
  }).format(cents / 100);
}

/** Percentagem inteira; 'null' vira travessão, para não fingir um zero que não existe. */
export function formatPercent(value: number | null): string {
  return value === null ? '—' : `${formatNumber(value)} %`;
}
