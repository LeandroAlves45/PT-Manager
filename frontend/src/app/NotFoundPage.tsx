import { useEffect, useRef } from 'react';
import { Link } from 'react-router';

import { useAuth } from '@/app/providers/useAuth';
import { homeRouteFor } from '@/shared/api/session';
import { Button } from '@/shared/components/ui/button';
import { AUTH_ROUTES } from '@/shared/config/auth-routes';

/**
 * Página 404, pública: funciona com e sem sessão.
 *
 * O botão leva ao sítio certo para quem lá está: a home do papel com sessão, o login sem ela.
 * Enquanto a sessão restaura aponta para `/`, que espera pelo fim do arranque
 * (`RootRedirect`) e decide sozinho. O título recebe o foco: uma rota errada é uma navegação,
 * e o leitor de ecrã tem de anunciar onde chegou.
 */
export function NotFoundPage() {
  const { status, session } = useAuth();
  const titleRef = useRef<HTMLHeadingElement>(null);

  useEffect(() => {
    titleRef.current?.focus();
  }, []);

  const home =
    status === 'restoring'
      ? '/'
      : session === null
        ? AUTH_ROUTES.login
        : homeRouteFor(session.role);

  return (
    <main className="bg-background flex min-h-dvh flex-col items-center justify-center gap-4 px-4 text-center">
      <p className="font-display text-muted-foreground text-6xl leading-none" aria-hidden>
        404
      </p>
      <h1 ref={titleRef} tabIndex={-1} className="font-display text-3xl leading-none outline-none">
        Página não encontrada
      </h1>
      <p className="text-muted-foreground max-w-prose text-sm">
        O endereço que abriste não existe ou foi movido.
      </p>
      <Button asChild className="min-h-11">
        <Link to={home}>Voltar ao início</Link>
      </Button>
    </main>
  );
}
