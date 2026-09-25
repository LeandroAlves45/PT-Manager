import type { LucideIcon } from 'lucide-react';
import type { ReactNode } from 'react';

import { cn } from '@/shared/lib/utils';

/**
 * Cartão do bento do painel: eyebrow com ícone, conteúdo e, no máximo, uma ação.
 *
 * `highlight` é o KPI principal (check-ins por rever): borda primária e o glow do desenho
 * aprovado (`0 0 60px -20px`), feito com o token `--color-primary` e não com um hex.
 */
export function BentoCard({
  title,
  icon: Icon,
  aside,
  action,
  highlight = false,
  className,
  children,
}: {
  title: string;
  icon: LucideIcon;
  aside?: ReactNode;
  action?: ReactNode;
  highlight?: boolean;
  className?: string;
  children: ReactNode;
}) {
  return (
    <section
      aria-label={title}
      className={cn(
        'bg-card border-border flex flex-col gap-4 rounded-xl border p-5',
        highlight && 'border-primary/40 shadow-[0_0_60px_-20px_var(--color-primary)]',
        className
      )}
    >
      <header className="flex items-center justify-between gap-2">
        <h2 className="text-muted-foreground flex items-center gap-2 font-mono text-xs tracking-wide uppercase">
          <Icon aria-hidden className={cn('size-4', highlight && 'text-primary')} />
          {title}
        </h2>
        {aside}
      </header>
      <div className="flex-1">{children}</div>
      {action !== undefined && <div>{action}</div>}
    </section>
  );
}
