import { useState } from 'react';

import { useGoogleSignInMutation } from '@/features/auth/api/auth';
import { useGoogleIdentity } from '@/features/auth/hooks/useGoogleIdentity';
import { authErrorMessage } from '@/features/auth/lib/auth-errors';
import { setSession, type Session } from '@/shared/api/session';

/**
 * Botão "Continuar com Google" e o pedido ao backend.
 *
 * Três modos de uso, pela combinação das propriedades:
 * - entrar: sem `createAccount` — uma identidade desconhecida dá `google_account_not_found`;
 * - criar conta: `createAccount` — o clique no botão é o consentimento para criar a conta;
 * - aceitar convite: `invitationToken` — a conta nasce ligada ao convite.
 *
 * O backend responde `200` (sessão) ou `202` (falta confirmar o email). O hook pede um desafio
 * novo depois de cada tentativa, porque o `nonce` é de uso único.
 *
 * Sem cliente Google configurado a secção inteira, separador incluído, não aparece.
 *
 * @param onSignedIn Chamada com a sessão já guardada em memória.
 * @param onPending Chamada quando o Google confirmou a identidade mas falta confirmar o email.
 */
export function GoogleSignInSection({
  createAccount = false,
  invitationToken = null,
  onSignedIn,
  onPending,
}: {
  createAccount?: boolean;
  invitationToken?: string | null;
  onSignedIn: (session: Session) => void;
  onPending?: () => void;
}) {
  const mutation = useGoogleSignInMutation();
  const [failure, setFailure] = useState<string | null>(null);

  // Desestruturado: um valor passado a `ref=` é tratado pelo compilador do React como ref, e ler
  // `google.available` passaria a contar como ler uma ref durante o render.
  const { available, attachContainer, error } = useGoogleIdentity({
    purpose: 'sign-in',
    onCredential: submit,
  });

  async function submit(idToken: string): Promise<void> {
    setFailure(null);

    try {
      const outcome = await mutation.mutateAsync({
        id_token: idToken,
        invitation_token: invitationToken,
        create_account: createAccount,
      });

      if (outcome.kind === 'pending') {
        onPending?.();
        return;
      }

      setSession(outcome.session);
      onSignedIn(outcome.session);
    } catch (error) {
      setFailure(authErrorMessage(error));
    }
  }

  if (!available) return null;

  const message = failure ?? error;

  return (
    <div className="space-y-3">
      <div className="text-muted-foreground flex items-center gap-3 text-xs" aria-hidden>
        <span className="bg-border h-px flex-1" />
        ou
        <span className="bg-border h-px flex-1" />
      </div>

      <div ref={attachContainer} className="flex min-h-10 justify-center" />

      {message !== null && (
        <p role="alert" className="text-destructive text-sm">
          {message}
        </p>
      )}
    </div>
  );
}
