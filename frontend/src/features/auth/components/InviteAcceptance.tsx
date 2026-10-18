import { useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { useNavigate } from 'react-router';

import { useAcceptInviteMutation } from '@/features/auth/api/auth';
import { AuthHeading } from '@/features/auth/components/AuthHeading';
import { LinkProblemPanel } from '@/features/auth/components/LinkProblemPanel';
import { authErrorMessage, authMessage } from '@/features/auth/lib/auth-errors';
import { isApiProblem } from '@/shared/api/problem';
import { homeRouteFor, setSession } from '@/shared/api/session';
import { Button } from '@/shared/components/ui/button';

/** Códigos em que este convite não pode ser aceite por esta conta, por mais que se tente. */
const TERMINAL_CODES: ReadonlySet<string> = new Set([
  'authentication_invitation_invalid',
  'authentication_invitation_expired',
  'authentication_invitation_consumed',
  'authentication_invitation_email_mismatch',
  'authentication_relationship_conflict',
  'authentication_client_only',
  'authentication_account_inactive',
]);

/**
 * Convite aberto por um cliente com sessão iniciada: aceitar com um clique.
 *
 * Se a conta já acompanha outro personal trainer o servidor responde 409
 * `authentication_transfer_approval_required`: o ecrã explica a mudança e só repete o pedido
 * com `transfer_approved: true` depois de o cliente confirmar. No sucesso a sessão é
 * substituída (o `trainer_id` mudou) e a cache de dados é limpa: o utilizador é o mesmo, por
 * isso o `AuthProvider` não a limpa sozinho, e ficaria a mostrar dados do treinador anterior.
 *
 * @param token Token do convite, lido da query string pela página.
 */
export function InviteAcceptance({ token }: { token: string }) {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const accept = useAcceptInviteMutation();
  const [needsTransfer, setNeedsTransfer] = useState(false);
  const [terminal, setTerminal] = useState<string | null>(null);
  const [formError, setFormError] = useState<string | null>(null);

  async function onAccept(transferApproved: boolean): Promise<void> {
    setFormError(null);

    try {
      const session = await accept.mutateAsync({ token, transfer_approved: transferApproved });
      queryClient.clear();
      setSession(session);
      void navigate(homeRouteFor(session.role), { replace: true });
    } catch (failure) {
      if (isApiProblem(failure)) {
        if (failure.code === 'authentication_transfer_approval_required') {
          setNeedsTransfer(true);
          return;
        }
        if (TERMINAL_CODES.has(failure.code)) {
          setTerminal(authErrorMessage(failure));
          return;
        }
      }

      setFormError(authErrorMessage(failure));
    }
  }

  if (terminal !== null)
    return (
      <LinkProblemPanel
        title="Convite indisponível"
        message={terminal}
        actions={[{ to: homeRouteFor('client'), label: 'Voltar à página inicial' }]}
      />
    );

  return (
    <div className="space-y-6">
      <AuthHeading
        title="Aceitar convite"
        description={
          needsTransfer
            ? 'A tua conta já acompanha outro personal trainer.'
            : 'Um personal trainer convidou-te para a sua lista de clientes.'
        }
      />

      {needsTransfer && (
        <p role="alert" className="border-border bg-muted rounded-md border p-3 text-sm">
          {authMessage('authentication_transfer_approval_required')}
        </p>
      )}

      {formError !== null && (
        <p role="alert" aria-live="assertive" className="text-destructive text-sm">
          {formError}
        </p>
      )}

      <Button
        className="min-h-11 w-full"
        disabled={accept.isPending}
        onClick={() => void onAccept(needsTransfer)}
      >
        {accept.isPending
          ? 'A aceitar…'
          : needsTransfer
            ? 'Confirmar mudança e aceitar'
            : 'Aceitar convite'}
      </Button>
    </div>
  );
}
