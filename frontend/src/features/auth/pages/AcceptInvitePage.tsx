import { useSearchParams } from 'react-router';

import { useAuth } from '@/app/providers/useAuth';
import { AuthHeading } from '@/features/auth/components/AuthHeading';
import { InviteAcceptance } from '@/features/auth/components/InviteAcceptance';
import { InviteRegistration } from '@/features/auth/components/InviteRegistration';
import { LinkProblemPanel } from '@/features/auth/components/LinkProblemPanel';
import { authMessage } from '@/features/auth/lib/auth-errors';
import { Button } from '@/shared/components/ui/button';
import { Skeleton } from '@/shared/components/ui/skeleton';
import { AUTH_ROUTES } from '@/shared/config/auth-routes';

/**
 * Convite de um personal trainer (`/aceitar-convite?token=`).
 *
 * É pública mas tem de funcionar com e sem sessão, por isso não está atrás do
 * `RequireAuth`. Cinco estados:
 * - sem token: ecrã de erro com saída para o login;
 * - a restaurar a sessão: esqueleto (decidir antes disso mostraria o formulário errado);
 * - anónimo: criar conta (palavra-passe ou Google) ou entrar numa conta existente;
 * - cliente autenticado: aceitar com um clique (`InviteAcceptance`);
 * - treinador ou administrador autenticado: aviso e opção de terminar sessão.
 */
export function AcceptInvitePage() {
  const [params] = useSearchParams();
  const token = params.get('token');
  const { status, session, signOut } = useAuth();

  if (token === null || token === '')
    return (
      <LinkProblemPanel
        title="Convite inválido"
        message={authMessage('authentication_invitation_invalid')}
        actions={[{ to: AUTH_ROUTES.login, label: 'Entrar' }]}
      />
    );

  if (status === 'restoring')
    return (
      <div role="status" aria-label="A restaurar sessão…" className="space-y-3">
        <Skeleton className="h-8 w-40" />
        <Skeleton className="h-4 w-full" />
        <Skeleton className="h-11 w-full" />
      </div>
    );

  if (session === null) return <InviteRegistration token={token} />;

  if (session.role !== 'client')
    return (
      <div className="space-y-4">
        <AuthHeading
          autoFocus
          title="Esta conta não é de cliente"
          description="Só uma conta de cliente pode aceitar um convite. Termina a sessão para criar ou usar a conta do cliente."
        />
        <Button variant="outline" className="min-h-11 w-full" onClick={() => void signOut()}>
          Terminar sessão
        </Button>
      </div>
    );

  return <InviteAcceptance token={token} />;
}
