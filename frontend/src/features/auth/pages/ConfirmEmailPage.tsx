import { useEffect, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router';

import { LinkProblemPanel } from '@/features/auth/components/LinkProblemPanel';
import { authErrorMessage, CONNECTION_ERROR_MESSAGE } from '@/features/auth/lib/auth-errors';
import { confirmEmailOnce } from '@/features/auth/lib/confirm-email';
import { type ApiProblem } from '@/shared/api/problem';
import { Skeleton } from '@/shared/components/ui/skeleton';
import { AUTH_ROUTES } from '@/shared/config/auth-routes';

const NO_TOKEN_MESSAGE = 'O link de confirmação não é válido.';

/**
 * Confirmação do email (`/confirmar-email?token=`).
 *
 * Confirma ao montar, sem botão: o utilizador já clicou no link do email. O pedido é
 * deduplicado por token (`confirmEmailOnce`) porque o token é de uso único e o StrictMode
 * monta o efeito duas vezes. No sucesso vai para o login com um aviso; nos erros mostra o
 * motivo e a saída (pedir novo email, ou entrar quando o link já tinha sido usado).
 */
export function ConfirmEmailPage() {
  const [params] = useSearchParams();
  const token = params.get('token');
  const navigate = useNavigate();
  const [failure, setFailure] = useState<ApiProblem | 'network' | null>(null);

  useEffect(() => {
    if (token === null || token === '') return;

    let cancelled = false;

    confirmEmailOnce(token).then(
      (result) => {
        if (cancelled) return;

        if (result.ok) {
          void navigate(AUTH_ROUTES.login, {
            replace: true,
            state: { notice: 'Email confirmado. Já podes entrar.' },
          });
          return;
        }

        setFailure(result.problem);
      },
      () => {
        if (!cancelled) setFailure('network');
      }
    );

    return () => {
      cancelled = true;
    };
  }, [token, navigate]);

  if (token === null || token === '')
    return (
      <LinkProblemPanel
        title="Link inválido"
        message={NO_TOKEN_MESSAGE}
        actions={[{ to: AUTH_ROUTES.resendConfirmation, label: 'Pedir um novo email' }]}
      />
    );

  if (failure === null)
    return (
      <div role="status" aria-label="A confirmar o email…" className="space-y-3">
        <Skeleton className="h-8 w-40" />
        <Skeleton className="h-4 w-full" />
      </div>
    );

  if (failure === 'network')
    return (
      <LinkProblemPanel
        title="Não foi possível confirmar"
        message={CONNECTION_ERROR_MESSAGE}
        actions={[{ to: AUTH_ROUTES.resendConfirmation, label: 'Pedir um novo email' }]}
      />
    );

  const consumed = failure.code === 'authentication_confirmation_token_consumed';

  return (
    <LinkProblemPanel
      title={consumed ? 'Email já confirmado' : 'Não foi possível confirmar'}
      message={authErrorMessage(failure)}
      actions={
        consumed
          ? [{ to: AUTH_ROUTES.login, label: 'Entrar' }]
          : [
              { to: AUTH_ROUTES.resendConfirmation, label: 'Pedir um novo email' },
              { to: AUTH_ROUTES.login, label: 'Voltar a entrar' },
            ]
      }
    />
  );
}
