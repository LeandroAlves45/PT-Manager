import { zodResolver } from '@hookform/resolvers/zod';
import { useEffect, useState } from 'react';
import { useForm, useWatch } from 'react-hook-form';
import { Link, Navigate, useLocation, useNavigate } from 'react-router';

import { useAuth } from '@/app/providers/useAuth';
import { useLoginMutation } from '@/features/auth/api/auth';
import { AuthHeading } from '@/features/auth/components/AuthHeading';
import { GoogleSignInSection } from '@/features/auth/components/GoogleSignInSection';
import { PasswordField } from '@/features/auth/components/PasswordField';
import { consumePendingAuthNotice, peekPendingAuthNotice } from '@/shared/lib/auth-notice';
import { authErrorMessage } from '@/features/auth/lib/auth-errors';
import { applyProblemToForm } from '@/features/auth/lib/form-errors';
import { readNotice } from '@/features/auth/lib/location-state';
import { getReturnPath } from '@/features/auth/lib/return-path';
import { loginSchema, type LoginFormValues } from '@/features/auth/schemas/login.schema';
import { isApiProblem } from '@/shared/api/problem';
import { homeRouteFor, setSession } from '@/shared/api/session';
import { FormField } from '@/shared/components/FormField';
import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';
import { AUTH_ROUTES } from '@/shared/config/auth-routes';

/** Campos do formulário por nome de campo do servidor (ver `applyProblemToForm`). */
const LOGIN_FIELDS = { email: 'email', password: 'password' } as const;

/**
 * Início de sessão (`/entrar`).
 *
 * Regras que não podem falhar:
 * - sem enumeração de contas: credenciais erradas, conta inexistente e email por confirmar
 *   têm a mesma resposta (`authentication_invalid_credentials`);
 * - erros de validação do servidor voltam aos campos; o 429 não repete o pedido;
 * - o destino guardado pelo `RequireAuth` só é seguido se for interno (`getReturnPath`);
 * - um aviso de outro ecrã (`state.notice`: "Palavra-passe alterada...") aparece por cima.
 *
 * O link "Não recebeste o email?" leva o email escrito em `state`, nunca na query string.
 */
export function LoginPage() {
  const { status, session } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const login = useLoginMutation();
  const [formError, setFormError] = useState<string | null>(null);
  const returnPath = getReturnPath(location.state);
  // O aviso do state ganha; senão o flash de quem terminou a sessão (mudar palavra-passe).
  // Lê-se sem consumir no inicializador (o StrictMode chama-o duas vezes) e consome-se no efeito.
  const [notice] = useState(() => readNotice(location.state) ?? peekPendingAuthNotice());
  useEffect(() => {
    consumePendingAuthNotice();
  }, []);

  const {
    register,
    handleSubmit,
    setError,
    control,
    formState: { errors, isSubmitting },
  } = useForm<LoginFormValues>({
    resolver: zodResolver(loginSchema),
    defaultValues: { email: '', password: '' },
  });

  const typedEmail = useWatch({ control, name: 'email' });

  if (status === 'authenticated' && session !== null)
    return <Navigate to={returnPath ?? homeRouteFor(session.role)} replace />;

  async function onSubmit(values: LoginFormValues): Promise<void> {
    setFormError(null);

    try {
      const newSession = await login.mutateAsync(values);
      setSession(newSession);
      void navigate(returnPath ?? homeRouteFor(newSession.role), { replace: true });
    } catch (failure) {
      if (isApiProblem(failure) && applyProblemToForm(failure, setError, LOGIN_FIELDS)) return;

      setFormError(authErrorMessage(failure));
    }
  }

  const busy = isSubmitting || status === 'restoring';

  return (
    <div className="space-y-6">
      <AuthHeading title="Entrar" description="Acede à tua conta PT Manager." />

      {notice !== null && (
        <p role="status" className="border-border bg-muted rounded-md border p-3 text-sm">
          {notice}
        </p>
      )}

      <form
        onSubmit={(event) => void handleSubmit(onSubmit)(event)}
        className="space-y-4"
        noValidate
      >
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
          autoComplete="current-password"
          error={errors.password?.message}
          {...register('password')}
        />

        {formError !== null && (
          <p role="alert" aria-live="assertive" className="text-destructive text-sm">
            {formError}
          </p>
        )}

        <Button type="submit" className="min-h-11 w-full" disabled={busy}>
          {busy ? 'A entrar…' : 'Entrar'}
        </Button>
      </form>

      <GoogleSignInSection
        onSignedIn={(newSession) =>
          void navigate(returnPath ?? homeRouteFor(newSession.role), { replace: true })
        }
      />

      <nav aria-label="Outras opções" className="space-y-2 text-sm">
        <Link
          to={AUTH_ROUTES.forgotPassword}
          className="text-foreground block underline underline-offset-4"
        >
          Esqueceste a palavra-passe?
        </Link>
        <Link
          to={AUTH_ROUTES.resendConfirmation}
          state={{ email: typedEmail }}
          className="text-muted-foreground block underline underline-offset-4"
        >
          Não recebeste o email de confirmação?
        </Link>
        <p className="text-muted-foreground">
          Ainda não tens conta?{' '}
          <Link to={AUTH_ROUTES.signUp} className="text-foreground underline underline-offset-4">
            Criar conta
          </Link>
        </p>
      </nav>
    </div>
  );
}
