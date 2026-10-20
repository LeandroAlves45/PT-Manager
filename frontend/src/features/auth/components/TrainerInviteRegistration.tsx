import { zodResolver } from '@hookform/resolvers/zod';
import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { Link } from 'react-router';
import { z } from 'zod';

import { useRegisterInvitedTrainerMutation } from '@/features/auth/api/trainer-invite';
import { AuthHeading } from '@/features/auth/components/AuthHeading';
import { LinkProblemPanel } from '@/features/auth/components/LinkProblemPanel';
import { PasswordField } from '@/features/auth/components/PasswordField';
import { authErrorMessage, authMessage } from '@/features/auth/lib/auth-errors';
import { applyProblemToForm } from '@/features/auth/lib/form-errors';
import { newPasswordRule, PASSWORD_RULE_HINT } from '@/features/auth/schemas/password.schema';
import { isApiProblem } from '@/shared/api/problem';
import { FormField } from '@/shared/components/FormField';
import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';
import { AUTH_ROUTES } from '@/shared/config/auth-routes';

const trainerInviteRegistrationSchema = z
  .object({
    full_name: z
      .string()
      .trim()
      .min(1, 'Indica o teu nome completo.')
      .max(255, 'O nome deve conter no máximo 255 caracteres.')
      // O backend recusa U+0000 e controlos; aqui falha-se antes do pedido, com texto claro.
      // eslint-disable-next-line no-control-regex
      .refine((value) => !/[\u0000-\u001F\u007F]/.test(value), 'O nome tem caracteres inválidos.'),
    password: newPasswordRule,
    confirm_password: z.string().min(1, 'Confirma a palavra-passe.'),
  })
  .refine((values) => values.password === values.confirm_password, {
    message: 'As palavras-passe não coincidem.',
    path: ['confirm_password'],
  });

type TrainerInviteRegistrationValues = z.infer<typeof trainerInviteRegistrationSchema>;

const REGISTRATION_FIELDS = {
  fullname: 'full_name',
  password: 'password',
  confirmpassword: 'confirm_password',
} as const;

/** Códigos em que o link já não serve: nenhuma nova tentativa com ele vai funcionar. */
const DEAD_LINK_CODES: ReadonlySet<string> = new Set([
  'trainer_invitation_invalid',
  'trainer_invitation_expired',
  'trainer_invitation_used',
]);

/**
 * Formulário de criação da conta de um treinador convidado pelo superuser.
 *
 * O email vem do estado do convite e só se mostra (a conta fica ligada a ele no servidor).
 * Não há Google nesta página: a conta cria-se com email e palavra-passe. No sucesso não
 * há sessão: o painel «Conta criada» leva ao login.
 *
 * @param token Token do convite, lido da query string pela página.
 * @param email Email a que o convite se destina.
 */
export function TrainerInviteRegistration({ token, email }: { token: string; email: string }) {
  const register = useRegisterInvitedTrainerMutation();
  const [dead, setDead] = useState<string | null>(null);
  const [created, setCreated] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);

  const form = useForm<TrainerInviteRegistrationValues>({
    resolver: zodResolver(trainerInviteRegistrationSchema),
    defaultValues: { full_name: '', password: '', confirm_password: '' },
  });
  const { errors, isSubmitting } = form.formState;

  async function onSubmit(values: TrainerInviteRegistrationValues): Promise<void> {
    setFormError(null);

    try {
      await register.mutateAsync({ token, ...values });
      setCreated(true);
    } catch (failure) {
      if (isApiProblem(failure)) {
        if (DEAD_LINK_CODES.has(failure.code)) {
          setDead(authMessage(failure.code));
          return;
        }
        if (failure.status === 400) {
          // Sem campo identificável (ex.: `authentication_registration_rejected`), a
          // mensagem fica por baixo da palavra-passe.
          if (!applyProblemToForm(failure, form.setError, REGISTRATION_FIELDS))
            form.setError('password', { message: authErrorMessage(failure) });
          return;
        }
      }

      setFormError(authErrorMessage(failure));
    }
  }

  if (created)
    return (
      <div className="space-y-4">
        <AuthHeading
          autoFocus
          title="Conta criada"
          description="Já podes entrar com o teu email e a palavra-passe."
        />
        <Button asChild className="min-h-11 w-full">
          <Link to={AUTH_ROUTES.login}>Entrar</Link>
        </Button>
      </div>
    );

  if (dead !== null)
    return (
      <LinkProblemPanel
        title="Convite indisponível"
        message={dead}
        actions={[{ to: AUTH_ROUTES.login, label: 'Entrar' }]}
      />
    );

  return (
    <div className="space-y-6">
      <AuthHeading
        title="Criar conta de treinador"
        description="Foste convidado para o PT Manager. Define o teu nome e a palavra-passe."
      />

      <p className="text-sm">
        <span className="text-muted-foreground">Email do convite: </span>
        <span className="font-medium break-all">{email}</span>
      </p>

      <form
        onSubmit={(event) => void form.handleSubmit(onSubmit)(event)}
        className="space-y-4"
        noValidate
      >
        <FormField label="Nome" error={errors.full_name?.message}>
          {(control) => (
            <Input
              {...control}
              autoComplete="name"
              className="h-11"
              {...form.register('full_name')}
            />
          )}
        </FormField>
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
          {isSubmitting ? 'A criar conta…' : 'Criar conta'}
        </Button>
      </form>
    </div>
  );
}
