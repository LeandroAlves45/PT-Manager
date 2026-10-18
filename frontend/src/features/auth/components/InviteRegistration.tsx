import { zodResolver } from '@hookform/resolvers/zod';
import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { Link, useNavigate } from 'react-router';

import { useRegisterInvitedMutation } from '@/features/auth/api/auth';
import { AuthHeading } from '@/features/auth/components/AuthHeading';
import { CheckEmailPanel } from '@/features/auth/components/CheckEmailPanel';
import { GoogleSignInSection } from '@/features/auth/components/GoogleSignInSection';
import { LinkProblemPanel } from '@/features/auth/components/LinkProblemPanel';
import { PasswordField } from '@/features/auth/components/PasswordField';
import { authErrorMessage } from '@/features/auth/lib/auth-errors';
import { applyProblemToForm } from '@/features/auth/lib/form-errors';
import {
  PASSWORD_RULE_HINT,
  inviteRegistrationSchema,
  type InviteRegistrationFormValues,
} from '@/features/auth/schemas/password.schema';
import { isApiProblem } from '@/shared/api/problem';
import { homeRouteFor, setSession } from '@/shared/api/session';
import { Button } from '@/shared/components/ui/button';
import { AUTH_ROUTES, withToken } from '@/shared/config/auth-routes';

const REGISTRATION_FIELDS = { password: 'password', confirmpassword: 'confirm_password' } as const;

/** Códigos em que o convite não serve: nenhuma nova tentativa com este link vai funcionar. */
const DEAD_INVITATION_CODES: ReadonlySet<string> = new Set([
  'authentication_invitation_invalid',
  'authentication_invitation_expired',
  'authentication_invitation_consumed',
  'authentication_relationship_conflict',
]);

/**
 * Convite aberto por quem não tem sessão: criar a conta de cliente.
 *
 * Duas portas, ambas ligadas ao token do convite: palavra-passe nova ou Google. Quem já tem
 * conta com o email do convite (`authentication_invitation_account_exists`) é mandado entrar
 * e volta a este link depois (`state.from`), já como cliente autenticado. Um convite morto
 * (inválido, expirado, já usado) termina num ecrã sem formulário.
 *
 * @param token Token do convite, lido da query string pela página.
 */
export function InviteRegistration({ token }: { token: string }) {
  const navigate = useNavigate();
  const register = useRegisterInvitedMutation();
  const [dead, setDead] = useState<string | null>(null);
  const [accountExists, setAccountExists] = useState(false);
  const [checkEmail, setCheckEmail] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);
  const invitePath = withToken(AUTH_ROUTES.acceptInvite, token);

  const form = useForm<InviteRegistrationFormValues>({
    resolver: zodResolver(inviteRegistrationSchema),
    defaultValues: { password: '', confirm_password: '' },
  });
  const { errors, isSubmitting } = form.formState;

  async function onSubmit(values: InviteRegistrationFormValues): Promise<void> {
    setFormError(null);

    try {
      const session = await register.mutateAsync({ token, ...values });
      setSession(session);
      void navigate(homeRouteFor(session.role), { replace: true });
    } catch (failure) {
      if (isApiProblem(failure)) {
        if (failure.code === 'authentication_invitation_account_exists') {
          setAccountExists(true);
          return;
        }
        if (DEAD_INVITATION_CODES.has(failure.code)) {
          setDead(authErrorMessage(failure));
          return;
        }
        if (applyProblemToForm(failure, form.setError, REGISTRATION_FIELDS)) return;
      }

      setFormError(authErrorMessage(failure));
    }
  }

  if (checkEmail) return <CheckEmailPanel />;

  if (dead !== null)
    return (
      <LinkProblemPanel
        title="Convite indisponível"
        message={dead}
        actions={[{ to: AUTH_ROUTES.login, label: 'Entrar' }]}
      />
    );

  if (accountExists)
    return (
      <div className="space-y-4">
        <AuthHeading
          autoFocus
          title="Já tens conta"
          description="Existe uma conta com o email deste convite. Entra para o aceitar."
        />
        <Button asChild className="min-h-11 w-full">
          <Link to={AUTH_ROUTES.login} state={{ from: invitePath }}>
            Entrar para aceitar
          </Link>
        </Button>
      </div>
    );

  return (
    <div className="space-y-6">
      <AuthHeading
        title="Aceitar convite"
        description="O teu personal trainer convidou-te. Cria a tua conta para começares."
      />

      <form
        onSubmit={(event) => void form.handleSubmit(onSubmit)(event)}
        className="space-y-4"
        noValidate
      >
        <PasswordField
          label="Palavra-passe"
          autoComplete="new-password"
          hint={PASSWORD_RULE_HINT}
          error={errors.password?.message}
          {...form.register('password')}
        />
        <PasswordField
          label="Confirmar palavra-passe"
          autoComplete="new-password"
          error={errors.confirm_password?.message}
          {...form.register('confirm_password')}
        />

        {formError !== null && (
          <p role="alert" aria-live="assertive" className="text-destructive text-sm">
            {formError}
          </p>
        )}

        <Button type="submit" className="min-h-11 w-full" disabled={isSubmitting}>
          {isSubmitting ? 'A criar conta…' : 'Criar conta e aceitar'}
        </Button>
      </form>

      <GoogleSignInSection
        invitationToken={token}
        onSignedIn={(session) => void navigate(homeRouteFor(session.role), { replace: true })}
        onPending={() => setCheckEmail(true)}
      />

      <p className="text-muted-foreground text-sm">
        Já tens conta?{' '}
        <Link
          to={AUTH_ROUTES.login}
          state={{ from: invitePath }}
          className="text-foreground underline underline-offset-4"
        >
          Entrar
        </Link>
      </p>
    </div>
  );
}
