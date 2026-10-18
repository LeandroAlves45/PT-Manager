import { TriangleAlert } from 'lucide-react';
import { Link } from 'react-router';

import { AuthHeading } from '@/features/auth/components/AuthHeading';

/**
 * Ecrã de um link de email que não pode ser usado (inválido, expirado, já usado).
 *
 * Termina sempre numa saída: o link para pedir outro, ou para entrar. O título recebe o
 * foco porque o ecrã aparece sem o utilizador ter feito nada (chegou por um link).
 *
 * @param title Título curto do problema.
 * @param message Explicação PT-PT (vem de `authErrorMessage`).
 * @param actions Ligações de saída, a primeira é a principal.
 */
export function LinkProblemPanel({
  title,
  message,
  actions,
}: {
  title: string;
  message: string;
  actions: ReadonlyArray<{ to: string; label: string; state?: unknown }>;
}) {
  return (
    <div className="space-y-4">
      <TriangleAlert aria-hidden className="text-destructive size-8" />
      <AuthHeading autoFocus title={title} />
      <p role="alert" className="text-muted-foreground text-sm">
        {message}
      </p>
      <nav aria-label="Saídas" className="space-y-2 text-sm">
        {actions.map((action, index) => (
          <Link
            key={action.to}
            to={action.to}
            state={action.state}
            className={
              index === 0
                ? 'text-foreground block underline underline-offset-4'
                : 'text-muted-foreground block underline underline-offset-4'
            }
          >
            {action.label}
          </Link>
        ))}
      </nav>
    </div>
  );
}
