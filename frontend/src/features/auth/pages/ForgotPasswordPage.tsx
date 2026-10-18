import { useRequestPasswordResetMutation } from '@/features/auth/api/auth';
import { EmailRequestForm } from '@/features/auth/components/EmailRequestForm';

/**
 * Pedido de recuperação de palavra-passe (`/recuperar-password`).
 *
 * O servidor responde `204` exista ou não conta com aquele email, e o ecrã de sucesso diz
 * sempre a mesma coisa em condicional ("Se existir uma conta..."): a resposta não pode servir
 * para descobrir que emails estão registados.
 */
export function ForgotPasswordPage() {
  const { mutateAsync } = useRequestPasswordResetMutation();

  return (
    <EmailRequestForm
      title="Recuperar palavra-passe"
      description="Indica o email da tua conta e enviamos-te um link para escolheres uma nova."
      submitLabel="Enviar link"
      pendingLabel="A enviar…"
      successDescription="Se existir uma conta com esse email, enviámos instruções para repor a palavra-passe."
      request={mutateAsync}
    />
  );
}
