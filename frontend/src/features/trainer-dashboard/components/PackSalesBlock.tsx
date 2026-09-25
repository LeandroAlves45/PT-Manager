import { format } from 'date-fns';
import { pt } from 'date-fns/locale';
import { Receipt, TrendingDown, TrendingUp } from 'lucide-react';

import { BentoCard } from '@/features/trainer-dashboard/components/BentoCard';
import type { components } from '@/shared/api/schema';
import { formatCurrency, formatNumber } from '@/shared/lib/format';

type PackSales = components['schemas']['PackSalesResponse'];

/** Nome do mês em PT-PT a partir de ano e mês (1-12) devolvidos pela API. */
function monthName(year: number, month: number): string {
  return format(new Date(year, month - 1, 1), 'LLLL', { locale: pt });
}

/**
 * Bloco "Vendas de packs (estimado)".
 *
 * Não é faturação: é a soma de `PriceCents` dos packs comprados no mês local do personal trainer.
 * A comparação e as duas barras só aparecem para a moeda principal quando o
 * mês anterior tem valor na mesma moeda — comparar euros com libras não significa nada.
 */
export function PackSalesBlock({ data, className }: { data: PackSales; className?: string }) {
  const current = data.current_month;
  const primary = current.totals[0];
  const previous =
    primary === undefined
      ? undefined
      : data.previous_month.totals.find((total) => total.currency === primary.currency);

  const change =
    primary !== undefined && previous !== undefined && previous.amount_cents > 0
      ? Math.round(((primary.amount_cents - previous.amount_cents) / previous.amount_cents) * 100)
      : null;
  const max = Math.max(primary?.amount_cents ?? 0, previous?.amount_cents ?? 0);

  return (
    <BentoCard
      title={`Vendas de packs · ${monthName(current.year, current.month)}`}
      icon={Receipt}
      className={className}
    >
      {primary === undefined ? (
        <p className="text-muted-foreground text-sm">Sem packs vendidos este mês.</p>
      ) : (
        <div className="space-y-3">
          <p className="font-display text-4xl tabular-nums">
            {formatCurrency(primary.amount_cents, primary.currency)}
          </p>
          <p className="text-muted-foreground text-sm">
            {formatNumber(primary.pack_count)} {primary.pack_count === 1 ? 'pack' : 'packs'} ·
            estimado
          </p>
          {current.totals.slice(1).map((total) => (
            <p key={total.currency} className="text-sm tabular-nums">
              {formatCurrency(total.amount_cents, total.currency)}
            </p>
          ))}
          {change !== null && (
            <p className="flex items-center gap-1 text-sm">
              {change >= 0 ? (
                <TrendingUp aria-hidden className="text-success size-4" />
              ) : (
                <TrendingDown aria-hidden className="text-warning size-4" />
              )}
              {change >= 0 ? '+' : ''}
              {formatNumber(change)} % vs{' '}
              {monthName(data.previous_month.year, data.previous_month.month)}
            </p>
          )}
          {previous !== undefined && max > 0 && (
            <div aria-hidden className="flex h-12 items-end gap-2">
              <div
                className="bg-muted w-6 rounded-sm"
                style={{ height: `${Math.max(4, (previous.amount_cents / max) * 100)}%` }}
              />
              <div
                className="bg-primary w-6 rounded-sm"
                style={{ height: `${Math.max(4, (primary.amount_cents / max) * 100)}%` }}
              />
            </div>
          )}
        </div>
      )}
    </BentoCard>
  );
}
