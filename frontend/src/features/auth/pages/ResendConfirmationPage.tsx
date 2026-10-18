import { useLocation } from 'react-router';

import { useRequestEmailConfirmationMutation } from '@/features/auth/api/auth';
import { EmailRequestForm } from '@/features/auth/components/EmailRequestForm';
import { readEmail } from '@/features/auth/lib/location-state';

/**
 * Reenvio do email de confirmação (`/reenviar-confirmacao`).
 *
 * O email pode vir pré-preenchido do login ou do ecrã "Verifica o teu email" (em
 * `location.state`, nunca na query string). O servidor responde `204` sempre, por isso a
 * mensagem é condicional e igual para qualquer email. Um 429 mostra o pedido de espera e o
 * formulário continua disponível.
 */
export function ResendConfirmationPage() {
  const location = useLocation();
  const { mutateAsync } = useRequestEmailConfirmationMutation();

  return (
    <EmailRequestForm
      title="Reenviar confirmação"
      description="Indica o email com que criaste a conta e enviamos-te um novo link."
      submitLabel="Reenviar email"
      pendingLabel="A enviar…"
      successDescription="Se existir uma conta por confirmar com esse email, enviámos um novo link."
      defaultEmail={readEmail(location.state)}
      request={mutateAsync}
    />
  );
}
