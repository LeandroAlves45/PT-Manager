import { MailCheck } from 'lucide-react';
import { Link } from 'react-router';

import { AuthHeading } from '@/features/auth/components/AuthHeading';
import { AUTH_ROUTES } from '@/shared/config/auth-routes';

/**
 * Ecrã "Verifica o teu email" depois de criar conta.
 *
 * A mensagem é a mesma para uma conta nova e para um email que já tinha conta: dizer qual dos
 * dois aconteceu revelaria que o email está registado. O email viaja em `state`, nunca no URL.
 *
 * @param email Email escrito no formulário, para o reenvio vir pré-preenchido; ausente quando
 *   a conta foi criada pelo Google (o email não passa pelo formulário).
 */
export function CheckEmailPanel({ email }: { email?: string }) {
  return (
    <div className="space-y-4">
      <MailCheck aria-hidden className="text-primary size-8" />
      <AuthHeading
        autoFocus
        title="Verifica o teu email"
        description="Se os dados estiverem corretos, enviámos um link de confirmação. Abre-o para ativar a conta."
      />
      <p className="text-muted-foreground text-sm">
        Não recebeste nada? Vê a pasta de spam ou{' '}
        <Link
          to={AUTH_ROUTES.resendConfirmation}
          state={email === undefined ? null : { email }}
          className="text-foreground underline underline-offset-4"
        >
          pede um novo email
        </Link>
        .
      </p>
      <Link
        to={AUTH_ROUTES.login}
        className="text-muted-foreground block text-sm underline underline-offset-4"
      >
        Voltar a entrar
      </Link>
    </div>
  );
}
