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

/**
 * Dose de um suplemento com o momento, quando existe: «5 g · Depois do treino» ou só «5 g».
 *
 * O momento é opcional desde a 6GB (business-rules.md, 2026-10-15).
 */
export function formatDoseAndTiming(servingSize: string, timing: string | null): string {
  return timing === null || timing.trim() === '' ? servingSize : `${servingSize} · ${timing}`;
}

const PLURAL_RULES = new Intl.PluralRules('pt-PT');

/**
 * Número com o substantivo concordante: «1 ativo», «0 ativos», «2 ativos».
 *
 * Usa as regras do português de Portugal em vez de um `=== 1` escrito à mão em cada ecrã.
 */
export function formatCount(count: number, singular: string, plural: string): string {
  return `${formatNumber(count)} ${PLURAL_RULES.select(count) === 'one' ? singular : plural}`;
}

/** Percentagem inteira; 'null' vira travessão, para não fingir um zero que não existe. */
export function formatPercent(value: number | null): string {
  return value === null ? '—' : `${formatNumber(value)} %`;
}

const LISBON_DATE_TIME = new Intl.DateTimeFormat('pt-PT', {
  timeZone: 'Europe/Lisbon',
  day: '2-digit',
  month: '2-digit',
  year: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
});

/**
 * Data e hora em Europe/Lisbon, independentemente do fuso do browser (RN-008).
 *
 * O superuser vê sempre as horas de Lisboa; `formatDateTime` usa o fuso da máquina.
 */
export function formatLisbonDateTime(value: string | Date): string {
  return LISBON_DATE_TIME.format(toDate(value));
}
