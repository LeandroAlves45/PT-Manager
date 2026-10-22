import type { LucideIcon } from 'lucide-react';
import type { ReactNode } from 'react';
import { Link } from 'react-router';

import { Button } from '@/shared/components/ui/button';
import { cn } from '@/shared/lib/utils';

/**
 * Secção do portal a que o cartão pertence. A cor identifica a secção (treino na cor da marca,
 * alimentação em verde, suplementos em ciano, avaliações em índigo) e nunca é a única pista: o
 * título e o ícone dizem o mesmo por texto.
 */
export type PortalTone = 'training' | 'nutrition' | 'supplements' | 'assessment' | 'neutral';

const TONE_CLASSES: Record<PortalTone, { readonly border: string; readonly icon: string }> = {
  training: { border: 'border-t-primary', icon: 'bg-primary/12 text-primary' },
  nutrition: { border: 'border-t-macro-fiber', icon: 'bg-macro-fiber/12 text-macro-fiber' },
  supplements: {
    border: 'border-t-section-supplements',
    icon: 'bg-section-supplements/12 text-section-supplements',
  },
  assessment: {
    border: 'border-t-section-assessment',
    icon: 'bg-section-assessment/12 text-section-assessment',
  },
  neutral: { border: 'border-t-border', icon: 'bg-muted text-muted-foreground' },
};

/**
 * Cartão do portal do cliente: título com ícone na cor da secção, conteúdo e, no máximo, uma
 * ação. Usado no Início e como contentor das outras páginas, para todas terem o mesmo aspeto.
 *
 * A ação é um link de largura total com 44 px de altura, o alvo de toque mínimo no telemóvel.
 * Sem título, o cartão é só o contentor (o título da página já diz o que é).
 */
export function PortalCard({
  title,
  icon: Icon,
  tone = 'neutral',
  action,
  className,
  children,
}: {
  title?: string;
  icon?: LucideIcon;
  tone?: PortalTone;
  action?: { readonly label: string; readonly to: string };
  className?: string;
  children: ReactNode;
}) {
  const classes = TONE_CLASSES[tone];

  return (
    <section
      aria-label={title}
      className={cn(
        'bg-card border-border flex min-w-0 flex-col gap-3 rounded-xl border border-t-4 p-4 shadow-xs',
        classes.border,
        className
      )}
    >
      {title !== undefined && (
        <h2 className="text-muted-foreground flex items-center gap-2 font-mono text-xs tracking-wide uppercase">
          {Icon !== undefined && (
            <span
              aria-hidden
              className={cn('flex size-7 items-center justify-center rounded-md', classes.icon)}
            >
              <Icon className="size-4" />
            </span>
          )}
          {title}
        </h2>
      )}
      <div className="min-w-0 flex-1 space-y-1">{children}</div>
      {action !== undefined && (
        <Button asChild className="min-h-11 w-full">
          <Link to={action.to}>{action.label}</Link>
        </Button>
      )}
    </section>
  );
}
