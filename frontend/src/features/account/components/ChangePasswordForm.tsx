import { zodResolver } from '@hookform/resolvers/zod';
import { useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { useForm } from 'react-hook-form';

import { useChangePasswordMutation } from '@/features/account/api/account';
import {
  PASSWORD_RULE_HINT,
  PasswordField,
  applyProblemToForm,
  authErrorMessage,
  changePasswordSchema,
  endLocalSession,
  setPendingAuthNotice,
  type ChangePasswordFormValues,
} from '@/features/auth';
import { isApiProblem } from '@/shared/api/problem';
import { Button } from '@/shared/components/ui/button';

const CHANGE_FIELDS = {
  currentpassword: 'current_password',
  newpassword: 'new_password',
  confirmnewpassword: 'confirm_new_password',
} as const;

/**
 * Mudar a palavra-passe com sessão iniciada.
 *
 * O servidor revoga todas as sessões da conta, a atual incluída: no sucesso esquece-se a
 * sessão local e vai-se para o login com um aviso, em vez de deixar o utilizador num ecrã
 * onde o próximo pedido dá 401. A palavra-passe atual errada (401 de domínio) fica no campo
 * e não despoleta um refresh da sessão (ver `carriesDomainCode` no cliente HTTP).
 */
export function ChangePasswordForm() {
  const queryClient = useQueryClient();
  const change = useChangePasswordMutation();
  const [formError, setFormError] = useState<string | null>(null);

  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<ChangePasswordFormValues>({
    resolver: zodResolver(changePasswordSchema),
    defaultValues: { current_password: '', new_password: '', confirm_new_password: '' },
  });

  async function onSubmit(values: ChangePasswordFormValues): Promise<void> {
    setFormError(null);

    try {
      await change.mutateAsync(values);
      // O servidor revogou as sessões. Quem leva ao login é o `RequireAuth`, ao ver a sessão
      // acabar; ele não leva avisos no state, por isso o aviso vai por um flash em memoria.
      setPendingAuthNotice('Palavra-passe alterada. Entra com a nova.');
      endLocalSession(queryClient);
    } catch (failure) {
      if (isApiProblem(failure)) {
        if (failure.code === 'authentication_current_password_invalid') {
          setError('current_password', { message: authErrorMessage(failure) });
          return;
        }
        if (failure.code === 'authentication_new_password_rejected') {
          setError('new_password', { message: authErrorMessage(failure) });
          return;
        }
        if (applyProblemToForm(failure, setError, CHANGE_FIELDS)) return;
      }

      setFormError(authErrorMessage(failure));
    }
  }

  return (
    <section
      aria-labelledby="change-password-title"
      className="border-border bg-card space-y-3 rounded-lg border p-4"
    >
      <div>
        <h2 id="change-password-title" className="font-display text-xl">
          Mudar palavra-passe
        </h2>
        <p className="text-muted-foreground text-sm">
          Depois de mudar, terás de entrar de novo em todos os dispositivos.
        </p>
      </div>

      <form
        onSubmit={(event) => void handleSubmit(onSubmit)(event)}
        className="space-y-4"
        noValidate
      >
        <PasswordField
          label="Palavra-passe atual"
          autoComplete="current-password"
          error={errors.current_password?.message}
          {...register('current_password')}
        />
        <PasswordField
          label="Nova palavra-passe"
          autoComplete="new-password"
          hint={PASSWORD_RULE_HINT}
          error={errors.new_password?.message}
          {...register('new_password')}
        />
        <PasswordField
          label="Confirmar nova palavra-passe"
          autoComplete="new-password"
          error={errors.confirm_new_password?.message}
          {...register('confirm_new_password')}
        />

        {formError !== null && (
          <p role="alert" aria-live="assertive" className="text-destructive text-sm">
            {formError}
          </p>
        )}

        <Button type="submit" className="min-h-11 w-full sm:w-auto" disabled={isSubmitting}>
          {isSubmitting ? 'A guardar…' : 'Mudar palavra-passe'}
        </Button>
      </form>
    </section>
  );
}
