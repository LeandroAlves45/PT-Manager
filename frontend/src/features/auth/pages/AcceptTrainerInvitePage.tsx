import { useSearchParams } from 'react-router';

import { useAuth } from '@/app/providers/useAuth';
import { useTrainerInviteStatus } from '@/features/auth/api/trainer-invite';
import { AuthHeading } from '@/features/auth/components/AuthHeading';
import { LinkProblemPanel } from '@/features/auth/components/LinkProblemPanel';
import { TrainerInviteRegistration } from '@/features/auth/components/TrainerInviteRegistration';
import { authErrorMessage, authMessage } from '@/features/auth/lib/auth-errors';
import { Button } from '@/shared/components/ui/button';
import { Skeleton } from '@/shared/components/ui/skeleton';
import { AUTH_ROUTES } from '@/shared/config/auth-routes';

const LOGIN_EXIT = [{ to: AUTH_ROUTES.login, label: 'Entrar' }] as const;

function LoadingInvite({ label }: { label: string }) {
  return (
    <div role="status" aria-label={label} className="space-y-3">
      <Skeleton className="h-8 w-40" />
      <Skeleton className="h-4 w-full" />
      <Skeleton className="h-11 w-full" />
    </div>
  );
}

/** Pergunta ao servidor em que estado está o convite e mostra o ecrã que lhe corresponde. */
function TrainerInviteGate({ token }: { token: string }) {
  const status = useTrainerInviteStatus(token);

  if (status.isPending) return <LoadingInvite label="A verificar o convite…" />;

  if (status.isError)
    return (
      <LinkProblemPanel
        title="Não foi possível verificar o convite"
        message={authErrorMessage(status.error)}
        actions={LOGIN_EXIT}
      />
    );

  const { status: state, email } = status.data;

  if (state === 'valid' && email !== null)
    return <TrainerInviteRegistration token={token} email={email} />;

  if (state === 'expired')
    return (
      <LinkProblemPanel
        title="Convite expirado"
        message={authMessage('trainer_invitation_expired')}
        actions={LOGIN_EXIT}
      />
    );

  if (state === 'used')
    return (
      <LinkProblemPanel
        title="Convite já usado"
        message={authMessage('trainer_invitation_used')}
        actions={LOGIN_EXIT}
      />
    );

  // `invalid`, ou `valid` sem email (não acontece): nunca se diz porquê.
  return (
    <LinkProblemPanel
      title="Convite inválido"
      message={authMessage('trainer_invitation_invalid')}
      actions={LOGIN_EXIT}
    />
  );
}

/**
 * Convite de treinador criado pelo superuser (`/convite-treinador?token=`).
 *
 * Pública, com e sem sessão. Estados: sem token (inválido), a restaurar a sessão (esqueleto),
 * com sessão ativa (aviso para terminar sessão: a conta nova não se cria por cima de outra) e
 * anónimo (estado do convite → formulário, ou o painel do estado). O token fica no URL, como
 * nas outras páginas de link de email.
 */
export function AcceptTrainerInvitePage() {
  const [params] = useSearchParams();
  const token = params.get('token');
  const { status, session, signOut } = useAuth();

  if (token === null || token === '')
    return (
      <LinkProblemPanel
        title="Convite inválido"
        message={authMessage('trainer_invitation_invalid')}
        actions={LOGIN_EXIT}
      />
    );

  if (status === 'restoring') return <LoadingInvite label="A restaurar sessão…" />;

  if (session !== null)
    return (
      <div className="space-y-4">
        <AuthHeading
          autoFocus
          title="Já tens sessão iniciada"
          description="Termina a sessão para criar a conta de treinador."
        />
        <Button variant="outline" className="min-h-11 w-full" onClick={() => void signOut()}>
          Terminar sessão
        </Button>
      </div>
    );

  return <TrainerInviteGate token={token} />;
}
