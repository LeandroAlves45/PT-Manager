import { zodResolver } from '@hookform/resolvers/zod';
import { MailCheck } from 'lucide-react';
import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { Link } from 'react-router';

import { AuthHeading } from '@/features/auth/components/AuthHeading';
import { authErrorMessage } from '@/features/auth/lib/auth-errors';
import { applyProblemToForm } from '@/features/auth/lib/form-errors';
import { emailSchema, type EmailFormValues } from '@/features/auth/schemas/email.schema';
import { isApiProblem } from '@/shared/api/problem';
import { FormField } from '@/shared/components/FormField';
import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';
import { AUTH_ROUTES } from '@/shared/config/auth-routes';

/**
 * Formulário só com email que termina num ecrã de sucesso neutro.
 *
 * Partilhado por "Recuperar palavra-passe" e "Reenviar confirmação": os dois pedem um email,
 * o servidor responde `204` exista ou não conta, e a mensagem de sucesso é condicional para
 * não revelar que emails estão registados. As páginas só configuram textos e o pedido.
 *
 * @param request Faz o pedido; deve lançar `ApiProblem` em caso de falha.
 * @param successDescription Mensagem condicional do ecrã de sucesso.
 * @param defaultEmail Email pré-preenchido.
 */
export function EmailRequestForm({
  title,
  description,
  submitLabel,
  pendingLabel,
  successDescription,
  defaultEmail = '',
  request,
}: {
  title: string;
  description: string;
  submitLabel: string;
  pendingLabel: string;
  successDescription: string;
  defaultEmail?: string;
  request: (values: EmailFormValues) => Promise<unknown>;
}) {
  const [sent, setSent] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);

  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<EmailFormValues>({
    resolver: zodResolver(emailSchema),
    defaultValues: { email: defaultEmail },
  });

  async function onSubmit(values: EmailFormValues): Promise<void> {
    setFormError(null);

    try {
      await request(values);
      setSent(true);
    } catch (failure) {
      if (isApiProblem(failure) && applyProblemToForm(failure, setError, { email: 'email' }))
        return;

      setFormError(authErrorMessage(failure));
    }
  }

  if (sent)
    return (
      <div className="space-y-4">
        <MailCheck aria-hidden className="text-primary size-8" />
        <AuthHeading autoFocus title="Verifica o teu email" description={successDescription} />
        <Link
          to={AUTH_ROUTES.login}
          className="text-muted-foreground block text-sm underline underline-offset-4"
        >
          Voltar a entrar
        </Link>
      </div>
    );

  return (
    <div className="space-y-6">
      <AuthHeading title={title} description={description} />

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

        {formError !== null && (
          <p role="alert" aria-live="assertive" className="text-destructive text-sm">
            {formError}
          </p>
        )}

        <Button type="submit" className="min-h-11 w-full" disabled={isSubmitting}>
          {isSubmitting ? pendingLabel : submitLabel}
        </Button>
      </form>

      <Link
        to={AUTH_ROUTES.login}
        className="text-muted-foreground block text-sm underline underline-offset-4"
      >
        Voltar a entrar
      </Link>
    </div>
  );
}
