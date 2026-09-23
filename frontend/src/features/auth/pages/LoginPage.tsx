import { zodResolver } from '@hookform/resolvers/zod';
import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { Navigate, useLocation, useNavigate } from 'react-router';

import { useAuth } from '@/app/providers/useAuth';
import { apiClient } from '@/shared/api/client';
import { toApiProblem, type ApiProblem } from '@/shared/api/problem';
import { homeRouteFor, setSession, toSession } from '@/shared/api/session';
import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';
import { Label } from '@/shared/components/ui/label';
import { loginSchema, type LoginFormValues } from '@/features/auth/schemas/login.schema';

function getReturnPath(state: unknown): string | null {
  const from = (state as { from?: unknown } | null)?.from;
  return typeof from === 'string' && from.startsWith('/') && !from.startsWith('//') ? from : null;
}

/**
 * Login mínimo de desenvolvimento.
 *
 * A página final — com recuperação de password, Google Sign-In e todo o copy — é da fase
 * 6G. Esta existe para que as fases 6D a 6F tenham como entrar na aplicação, e já respeita
 * as regras que não podem esperar: sem enumeração de contas na mensagem de erro, erros de
 * validação do servidor devolvidos aos campos, e 429 tratado sem repetir o pedido.
 */
export function LoginPage() {
  const { status, session } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [formError, setFormError] = useState<string | null>(null);
  const returnPath = getReturnPath(location.state);

  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<LoginFormValues>({
    resolver: zodResolver(loginSchema),
    defaultValues: { email: '', password: '' },
  });

  if (status === 'authenticated' && session !== null)
    return <Navigate to={returnPath ?? homeRouteFor(session.role)} replace />;

  function applyProblem(problem: ApiProblem): void {
    if (problem.hasFieldErrors) {
      for (const fieldError of problem.fieldErrors) {
        if (fieldError.field === 'email' || fieldError.field === 'password') {
          setError(fieldError.field, { message: fieldError.message });
        }
      }
      return;
    }

    if (problem.status === 429) {
      setFormError('Demasiadas tentativas. Tenta dentro de momentos.');
      return;
    }

    // Mensagem única para credenciais erradas e conta inexistente: dizer qual dos dois
    // falhou revelaria que aquele email está registado.
    setFormError(
      problem.status === 401
        ? 'Email ou password inválidos.'
        : 'Não foi possível iniciar sessão. Tenta novamente.'
    );
  }

  async function onSubmit(values: LoginFormValues): Promise<void> {
    setFormError(null);

    try {
      const result = await apiClient.POST('/api/v1/auth/login', { body: values });

      if (result.data === undefined) {
        applyProblem(toApiProblem(result.response.status, result.error));
        return;
      }

      const newSession = toSession(result.data);
      setSession(newSession);
      void navigate(returnPath ?? homeRouteFor(newSession.role), { replace: true });
    } catch {
      setFormError('Não foi possível contactar o servidor. Verifica a ligação e tenta novamente.');
    }
  }

  return (
    <form
      onSubmit={(event) => void handleSubmit(onSubmit)(event)}
      className="border-border bg-card space-y-4 rounded-xl border p-6"
      noValidate
    >
      <div className="space-y-2">
        <Label htmlFor="email">Email</Label>
        <Input id="email" type="email" autoComplete="email" {...register('email')} />
        {errors.email !== undefined && (
          <p className="text-destructive text-xs">{errors.email.message}</p>
        )}
      </div>

      <div className="space-y-2">
        <Label htmlFor="password">Password</Label>
        <Input
          id="password"
          type="password"
          autoComplete="current-password"
          {...register('password')}
        />
        {errors.password !== undefined && (
          <p className="text-destructive text-xs">{errors.password.message}</p>
        )}
      </div>

      {formError !== null && (
        <p role="alert" aria-live="assertive" className="text-destructive text-sm">
          {formError}
        </p>
      )}

      <Button type="submit" className="w-full" disabled={isSubmitting || status === 'restoring'}>
        {isSubmitting || status === 'restoring' ? 'A entrar…' : 'Entrar'}
      </Button>
    </form>
  );
}
