import type { ReactNode } from 'react';

import { cn } from '@/shared/lib/utils';

/**
 * Cabeçalho de página: título display, descrição e, no máximo, uma ação primária.
 */
export function PageHeader({
  title,
  description,
  action,
  className,
}: {
  title: string;
  description?: string;
  action?: ReactNode;
  className?: string;
}) {
  return (
    <header className={cn('flex flex-wrap items-end justify-between gap-4', className)}>
      <div className="space-y-1">
        <h1 className="font-display text-foreground text-3xl leading-none sm:text-4xl">{title}</h1>
        {description !== undefined && (
          <p className="text-muted-foreground max-w-prose text-sm">{description}</p>
        )}
      </div>
      {action !== undefined && <div className="shrink-0">{action}</div>}
    </header>
  );
}
