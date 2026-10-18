import { zodResolver } from '@hookform/resolvers/zod';
import { useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { useNavigate, useSearchParams } from 'react-router';

import { useCompletePasswordResetMutation } from '@/features/auth/api/auth';
import { AuthHeading } from '@/features/auth/components/AuthHeading';
import { LinkProblemPanel } from '@/features/auth/components/LinkProblemPanel';
import { PasswordField } from '@/features/auth/components/PasswordField';
import { authErrorMessage, authMessage } from '@/features/auth/lib/auth-errors';
import { applyProblemToForm } from '@/features/auth/lib/form-errors';
import { endLocalSession } from '@/features/auth/lib/local-session';
import {
  PASSWORD_RULE_HINT,
  resetPasswordSchema,
  type ResetPasswordFormValues,
} from '@/features/auth/schemas/password.schema';
import { isApiProblem } from '@/shared/api/problem';
import { Button } from '@/shared/components/ui/button';
import { AUTH_ROUTES } from '@/shared/config/auth-routes';

const RESET_FIELDS = {
  newpassword: 'new_password',
  confirmnewpassword: 'confirm_new_password',
} as const;

const REQUEST_NEW_LINK = { to: AUTH_ROUTES.forgotPassword, label: 'Pedir um novo link' } as const;

/**
 * Definir a nova palavra-passe (`/repor-password?token=`).
 *
 * O token do email é de uso único. Sem token, ou com o servidor a recusá-lo
 * (`authentication_password_reset_invalid`), o ecrã termina num link para pedir outro. No
 * sucesso o servidor já revogou todas as sessões: esquece-se a sessão local e vai-se para o
 * login com um aviso. Uma nova palavra-passe recusada pela política do servidor fica no campo.
 */
export function ResetPasswordPage() {
  const [params] = useSearchParams();
  const token = params.get('token');
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const complete = useCompletePasswordResetMutation();
  const [invalid, setInvalid] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);

  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<ResetPasswordFormValues>({
    resolver: zodResolver(resetPasswordSchema),
    defaultValues: { new_password: '', confirm_new_password: '' },
  });

  if (token === null || token === '' || invalid)
    return (
      <LinkProblemPanel
        title="Link inválido"
        message={authMessage('authentication_password_reset_invalid')}
        actions={[REQUEST_NEW_LINK, { to: AUTH_ROUTES.login, label: 'Voltar a entrar' }]}
      />
    );

  async function onSubmit(values: ResetPasswordFormValues): Promise<void> {
    setFormError(null);

    try {
      await complete.mutateAsync({ token: token ?? '', ...values });
      endLocalSession(queryClient);
      void navigate(AUTH_ROUTES.login, {
        replace: true,
        state: { notice: 'Palavra-passe alterada. Entra com a nova.' },
      });
    } catch (failure) {
      if (isApiProblem(failure)) {
        if (failure.code === 'authentication_password_reset_invalid') {
          setInvalid(true);
          return;
        }
        if (failure.code === 'authentication_new_password_rejected') {
          setError('new_password', { message: authErrorMessage(failure) });
          return;
        }
        if (applyProblemToForm(failure, setError, RESET_FIELDS)) return;
      }

      setFormError(authErrorMessage(failure));
    }
  }

  return (
    <div className="space-y-6">
      <AuthHeading title="Nova palavra-passe" description="Escolhe a palavra-passe da tua conta." />

      <form
        onSubmit={(event) => void handleSubmit(onSubmit)(event)}
        className="space-y-4"
        noValidate
      >
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

        <Button type="submit" className="min-h-11 w-full" disabled={isSubmitting}>
          {isSubmitting ? 'A guardar…' : 'Guardar palavra-passe'}
        </Button>
      </form>
    </div>
  );
}
