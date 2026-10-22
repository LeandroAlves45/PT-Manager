import type { LucideIcon } from 'lucide-react';
import type { ReactNode } from 'react';

/**
 * Estado vazio: ícone discreto, título, uma frase e **uma** ação.
 *
 * @param titleAs `h1` quando o estado ocupa a página inteira e não há outro título de página;
 *   `h2` (omissão) quando fica por baixo do título da página.
 */
export function EmptyState({
  icon: Icon,
  title,
  description,
  action,
  titleAs: Title = 'h2',
}: {
  icon: LucideIcon;
  title: string;
  description: string;
  action?: ReactNode;
  titleAs?: 'h1' | 'h2';
}) {
  return (
    <div className="border-border bg-card flex flex-col items-center gap-3 rounded-xl border px-6 py-12 text-center">
      <Icon aria-hidden className="text-muted-foreground size-8" />
      <Title className="font-display pe-1 text-xl">{title}</Title>
      <p className="text-muted-foreground max-w-sm text-sm">{description}</p>
      {action !== undefined && <div className="pt-1">{action}</div>}
    </div>
  );
}
