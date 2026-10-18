import { useState } from 'react';

import { useLinkGoogleMutation } from '@/features/account/api/account';
import { PasswordField, authErrorMessage, useGoogleIdentity } from '@/features/auth';

/**
 * Cartão "Ligar conta Google" de uma conta com palavra-passe.
 *
 * Duas confirmações: a palavra-passe atual (escrita antes) e a escolha da conta Google (o
 * botão do GIS, com o desafio do link). O Google tem de ter o mesmo email da conta
 * (`google_link_email_mismatch`). Depois de qualquer tentativa pede-se um desafio novo.
 *
 * Sem cliente Google configurado o cartão não aparece: oferecer uma ação impossível é pior
 * do que não a oferecer.
 */
export function GoogleLinkCard() {
  const link = useLinkGoogleMutation();
  const [currentPassword, setCurrentPassword] = useState('');
  const [failure, setFailure] = useState<string | null>(null);

  const { available, attachContainer, error } = useGoogleIdentity({
    purpose: 'link',
    onCredential: submit,
  });

  async function submit(idToken: string): Promise<void> {
    setFailure(null);

    try {
      if (currentPassword === '') {
        setFailure('Introduz a palavra-passe atual antes de ligar o Google.');
        return;
      }

      await link.mutateAsync({ id_token: idToken, current_password: currentPassword });
      setCurrentPassword('');
    } catch (caught) {
      setFailure(authErrorMessage(caught));
    }
  }

  if (!available) return null;

  const message = failure ?? error;

  return (
    <section
      aria-labelledby="google-link-title"
      className="border-border bg-card space-y-3 rounded-lg border p-4"
    >
      <div>
        <h2 id="google-link-title" className="font-display text-xl">
          Ligar conta Google
        </h2>
        <p className="text-muted-foreground text-sm">
          Passas a poder entrar com o Google. A conta Google tem de usar o mesmo email.
        </p>
      </div>

      <PasswordField
        label="Palavra-passe para confirmar"
        autoComplete="current-password"
        value={currentPassword}
        onChange={(event) => setCurrentPassword(event.target.value)}
      />

      <div ref={attachContainer} className="flex min-h-10 justify-center" />

      {message !== null && (
        <p role="alert" className="text-destructive text-sm">
          {message}
        </p>
      )}
    </section>
  );
}
