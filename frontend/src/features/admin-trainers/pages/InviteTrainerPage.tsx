import { zodResolver } from '@hookform/resolvers/zod';
import { ArrowLeft } from 'lucide-react';
import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { Link, useLocation, useNavigate } from 'react-router';
import { toast } from 'sonner';
import { z } from 'zod';

import { useSendTrainerInvitationMutation } from '@/features/admin-trainers/api/trainers';
import {
  INVALID_EMAIL_MESSAGE,
  sendInvitationMessage,
} from '@/features/admin-trainers/lib/trainer-labels';
import { isApiProblem } from '@/shared/api/problem';
import { FormField } from '@/shared/components/FormField';
import { PageHeader } from '@/shared/components/PageHeader';
import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';
import { formatLisbonDateTime } from '@/shared/lib/format';

const inviteSchema = z.object({
  email: z.string().trim().pipe(z.email(INVALID_EMAIL_MESSAGE).max(255, INVALID_EMAIL_MESSAGE)),
});

type InviteValues = z.infer<typeof inviteSchema>;

/** Email vindo da procura («Enviar convite isento»); só chega por `location.state`, nunca pelo URL. */
function emailFromState(state: unknown): string {
  if (typeof state !== 'object' || state === null || !('email' in state)) return '';
  return typeof state.email === 'string' ? state.email : '';
}

/**
 * Convidar um treinador (superuser).
 *
 * O convite é de uso único, vale 7 dias e cria a conta já no plano Pro, sem pagamento. O
 * email pode vir preenchido da procura. Um email que já tem conta (409) manda procurar a
 * conta e isentá-la; reenviar para o mesmo email revoga o link anterior.
 */
export function InviteTrainerPage() {
  const location = useLocation();
  const navigate = useNavigate();
  const send = useSendTrainerInvitationMutation();
  const [formError, setFormError] = useState<string | null>(null);

  const form = useForm<InviteValues>({
    resolver: zodResolver(inviteSchema),
    defaultValues: { email: emailFromState(location.state) },
  });
  const { errors, isSubmitting } = form.formState;

  async function onSubmit(values: InviteValues): Promise<void> {
    setFormError(null);

    try {
      const invitation = await send.mutateAsync(values.email);
      toast.success(`Convite enviado. Válido até ${formatLisbonDateTime(invitation.expires_at)}.`);
      void navigate('/admin/trainers');
    } catch (error) {
      if (isApiProblem(error) && error.status === 400)
        form.setError('email', { message: INVALID_EMAIL_MESSAGE });
      else setFormError(sendInvitationMessage(error));
    }
  }

  return (
    <section className="max-w-xl space-y-6">
      <Link
        to="/admin/trainers"
        className="text-muted-foreground hover:text-foreground inline-flex min-h-11 items-center gap-1 text-sm underline-offset-4 hover:underline"
      >
        <ArrowLeft aria-hidden className="size-4" /> Voltar a Treinadores
      </Link>

      <PageHeader
        title="Convidar treinador"
        description="O convite é de uso único, vale 7 dias e cria a conta já no plano Pro, sem pagamento. A conta cria-se com email e palavra-passe."
      />

      <form
        onSubmit={(event) => void form.handleSubmit(onSubmit)(event)}
        noValidate
        className="space-y-4"
      >
        <FormField label="Email do treinador" error={errors.email?.message}>
          {(control) => (
            <Input
              {...control}
              type="email"
              autoComplete="off"
              className="h-11"
              {...form.register('email')}
            />
          )}
        </FormField>

        {formError !== null && (
          <p role="alert" className="text-destructive text-sm">
            {formError}
          </p>
        )}

        <Button type="submit" className="min-h-11" disabled={isSubmitting}>
          {isSubmitting ? 'A enviar…' : 'Enviar convite'}
        </Button>
      </form>
    </section>
  );
}
