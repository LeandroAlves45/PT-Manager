import type { LucideIcon } from 'lucide-react';

import { cn } from '@/shared/lib/utils';

/**
 * Cartão de leitura (não navegável) dos totais de treinadores e faturação.
 *
 * RN-018: clicar num número não abre lista nenhuma, por isso é uma `section` e não um link.
 * `attention` aplica o destaque aprovado (borda primária + glow) só quando há algo a tratar
 * e expõe `data-attention` para os testes não dependerem de nomes de classes.
 */
export function TrainerStatCard({
  label,
  icon: Icon,
  value,
  helper,
  attention = false,
}: {
  label: string;
  icon: LucideIcon;
  value: string;
  helper?: string;
  attention?: boolean;
}) {
  return (
    <section
      aria-label={label}
      data-attention={attention ? 'true' : undefined}
      className={cn(
        'border-border bg-card rounded-xl border p-5',
        attention && 'border-primary/40 shadow-[0_0_60px_-20px_var(--color-primary)]'
      )}
    >
      <div className="text-muted-foreground flex items-center gap-2 text-sm">
        <Icon aria-hidden className={cn('size-4', attention && 'text-primary')} />
        {label}
      </div>
      <p className="font-display mt-3 text-3xl tabular-nums">{value}</p>
      {helper !== undefined && <p className="text-muted-foreground mt-1 text-sm">{helper}</p>}
    </section>
  );
}
