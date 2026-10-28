import { useQueryClient } from '@tanstack/react-query';
import { useState, type FormEvent } from 'react';

import { useDeleteMyAccountMutation } from '@/features/account/api/account';
import {
  PasswordField,
  authErrorMessage,
  endLocalSession,
  setPendingAuthNotice,
} from '@/features/auth';
import { isApiProblem } from '@/shared/api/problem';
import { FormField } from '@/shared/components/FormField';
import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';

/** Palavra que o cliente escreve para confirmar (igual à que o servidor valida). */
export const DELETE_CONFIRMATION_WORD = 'ELIMINAR';

const PASSWORD_INVALID = 'portal_account_deletion_password_invalid';
const CONFIRMATION_REQUIRED = 'portal_account_deletion_confirmation_required';

interface DeleteAccountCardProps {
  /** Contas só com Google não têm palavra-passe: a confirmação é só a palavra escrita. */
  hasPassword: boolean;
}

/**
 * Eliminar a conta (só no portal do cliente).
 *
 * Irreversível, por isso pede duas coisas: a palavra-passe atual e a palavra ELIMINAR escrita
 * à mão. O botão só fica ativo com a palavra certa. No sucesso o servidor já revogou todas as
 * sessões: esquece-se a sessão local e vai-se para o login com um aviso, como ao mudar a
 * palavra-passe. A palavra-passe errada volta como erro do campo (400), sem refresh da sessão.
 */
export function DeleteAccountCard({ hasPassword }: DeleteAccountCardProps) {
  const queryClient = useQueryClient();
  const deletion = useDeleteMyAccountMutation();
  const [password, setPassword] = useState('');
  const [confirmation, setConfirmation] = useState('');
  const [passwordError, setPasswordError] = useState<string | undefined>(undefined);
  const [confirmationError, setConfirmationError] = useState<string | undefined>(undefined);
  const [formError, setFormError] = useState<string | null>(null);

  const confirmed = confirmation.trim().toUpperCase() === DELETE_CONFIRMATION_WORD;
  const canSubmit = confirmed && (!hasPassword || password.length > 0) && !deletion.isPending;

  async function onSubmit(event: FormEvent<HTMLFormElement>): Promise<void> {
    event.preventDefault();
    setPasswordError(undefined);
    setConfirmationError(undefined);
    setFormError(null);
    if (!canSubmit) return;

    try {
      await deletion.mutateAsync({
        password: hasPassword ? password : null,
        confirmation: confirmation.trim(),
      });
      setPendingAuthNotice('A tua conta foi eliminada.');
      endLocalSession(queryClient);
    } catch (failure) {
      const codes = isApiProblem(failure) ? failure.fieldErrors.map((error) => error.code) : [];
      if (codes.includes(PASSWORD_INVALID)) {
        setPasswordError('A palavra-passe não está correta.');
        return;
      }
      if (codes.includes(CONFIRMATION_REQUIRED)) {
        setConfirmationError(`Escreve ${DELETE_CONFIRMATION_WORD} para confirmar.`);
        return;
      }
      setFormError(authErrorMessage(failure));
    }
  }

  return (
    <section
      aria-labelledby="delete-account-title"
      className="border-destructive/40 bg-card space-y-3 rounded-lg border p-4"
    >
      <div className="space-y-1">
        <h2 id="delete-account-title" className="font-display text-xl">
          Eliminar conta
        </h2>
        <p className="text-muted-foreground text-sm">
          Deixas de poder entrar e os teus dados pessoais (nome, contactos, data de nascimento,
          condições médicas e fotografia) são apagados. O histórico de treinos e avaliações fica com
          o teu personal trainer, sem te identificar. Não é possível desfazer.
        </p>
      </div>

      <form onSubmit={(event) => void onSubmit(event)} className="space-y-4" noValidate>
        {hasPassword && (
          <PasswordField
            label="Palavra-passe atual"
            autoComplete="current-password"
            error={passwordError}
            value={password}
            onChange={(event) => setPassword(event.target.value)}
          />
        )}

        <FormField
          label={`Escreve ${DELETE_CONFIRMATION_WORD} para confirmar`}
          error={confirmationError}
        >
          {(control) => (
            <Input
              {...control}
              autoComplete="off"
              autoCapitalize="characters"
              spellCheck={false}
              value={confirmation}
              onChange={(event) => setConfirmation(event.target.value)}
            />
          )}
        </FormField>

        {formError !== null && (
          <p role="alert" aria-live="assertive" className="text-destructive text-sm">
            {formError}
          </p>
        )}

        <Button
          type="submit"
          variant="destructive"
          className="min-h-11 w-full sm:w-auto"
          disabled={!canSubmit}
        >
          {deletion.isPending ? 'A eliminar…' : 'Eliminar conta'}
        </Button>
      </form>
    </section>
  );
}
