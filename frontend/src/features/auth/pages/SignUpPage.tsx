import { zodResolver } from '@hookform/resolvers/zod';
import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { Link, Navigate, useNavigate } from 'react-router';

import { useAuth } from '@/app/providers/useAuth';
import { useSignUpMutation } from '@/features/auth/api/auth';
import { AuthHeading } from '@/features/auth/components/AuthHeading';
import { CheckEmailPanel } from '@/features/auth/components/CheckEmailPanel';
import { GoogleSignInSection } from '@/features/auth/components/GoogleSignInSection';
import { PasswordField } from '@/features/auth/components/PasswordField';
import { authErrorMessage } from '@/features/auth/lib/auth-errors';
import { applyProblemToForm } from '@/features/auth/lib/form-errors';
import { PASSWORD_RULE_HINT } from '@/features/auth/schemas/password.schema';
import { signUpSchema, type SignUpFormValues } from '@/features/auth/schemas/signup.schema';
import { isApiProblem } from '@/shared/api/problem';
import { homeRouteFor } from '@/shared/api/session';
import { FormField } from '@/shared/components/FormField';
import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';
import { AUTH_ROUTES } from '@/shared/config/auth-routes';

/** Campos do formulário por nome de campo do servidor (ver `applyProblemToForm`). */
const SIGN_UP_FIELDS = { email: 'email', password: 'password', fullname: 'full_name' } as const;

/**
 * Criação de conta de personal trainer (`/criar-conta`).
 *
 * O servidor responde `202` quer o email seja novo quer já exista conta, e o ecrã seguinte
 * é igual nos dois casos ("Verifica o teu email"): nada aqui revela se o email estava
 * registado. Com Google, o clique no botão é o consentimento para criar a conta
 * (`createAccount`): `200` entra de imediato, `202` pede a confirmação do email.
 */
export function SignUpPage() {
  const { status, session } = useAuth();
  const navigate = useNavigate();
  const signUp = useSignUpMutation();
  const [sent, setSent] = useState<{ email?: string } | null>(null);
  const [formError, setFormError] = useState<string | null>(null);

  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<SignUpFormValues>({
    resolver: zodResolver(signUpSchema),
    defaultValues: { full_name: '', email: '', password: '' },
  });

  if (status === 'authenticated' && session !== null)
    return <Navigate to={homeRouteFor(session.role)} replace />;

  if (sent !== null) return <CheckEmailPanel {...sent} />;

  async function onSubmit(values: SignUpFormValues): Promise<void> {
    setFormError(null);

    try {
      await signUp.mutateAsync(values);
      setSent({ email: values.email });
    } catch (failure) {
      if (isApiProblem(failure) && applyProblemToForm(failure, setError, SIGN_UP_FIELDS)) return;

      setFormError(authErrorMessage(failure));
    }
  }

  return (
    <div className="space-y-6">
      <AuthHeading
        title="Criar conta"
        description="Para personal trainers. Começa com um período experimental."
      />

      <form
        onSubmit={(event) => void handleSubmit(onSubmit)(event)}
        className="space-y-4"
        noValidate
      >
        <FormField label="Nome completo" error={errors.full_name?.message}>
          {(control) => (
            <Input {...control} autoComplete="name" className="h-11" {...register('full_name')} />
          )}
        </FormField>

        <FormField label="Email" error={errors.email?.message}>
          {(control) => (
            <Input
              {...control}
              type="email"
              autoComplete="email"
              className="h-11"
              {...register('email')}
            />
          )}
        </FormField>

        <PasswordField
          label="Palavra-passe"
          autoComplete="new-password"
          hint={PASSWORD_RULE_HINT}
          error={errors.password?.message}
          {...register('password')}
        />

        {formError !== null && (
          <p role="alert" aria-live="assertive" className="text-destructive text-sm">
            {formError}
          </p>
        )}

        <Button type="submit" className="min-h-11 w-full" disabled={isSubmitting}>
          {isSubmitting ? 'A criar conta…' : 'Criar conta'}
        </Button>
      </form>

      <GoogleSignInSection
        createAccount
        onSignedIn={(newSession) => void navigate(homeRouteFor(newSession.role), { replace: true })}
        onPending={() => setSent({})}
      />

      <p className="text-muted-foreground text-sm">
        Já tens conta?{' '}
        <Link to={AUTH_ROUTES.login} className="text-foreground underline underline-offset-4">
          Entrar
        </Link>
      </p>
    </div>
  );
}
