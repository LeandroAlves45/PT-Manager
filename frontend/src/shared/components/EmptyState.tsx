import type { LucideIcon } from 'lucide-react';
import type { ReactNode } from 'react';

/** Estado vazio: ícone discreto, título, uma frase e **uma** ação. */
export function EmptyState({
  icon: Icon,
  title,
  description,
  action,
}: {
  icon: LucideIcon;
  title: string;
  description: string;
  action?: ReactNode;
}) {
  return (
    <div className="border-border bg-card flex flex-col items-center gap-3 rounded-xl border px-6 py-12 text-center">
      <Icon aria-hidden className="text-muted-foreground size-8" />
      <h2 className="font-display text-xl">{title}</h2>
      <p className="text-muted-foreground max-w-sm text-sm">{description}</p>
      {action !== undefined && <div className="pt-1">{action}</div>}
    </div>
  );
}
