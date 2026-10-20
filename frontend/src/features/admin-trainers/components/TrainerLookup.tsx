import { useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router';

import {
  useTrainerLookupMutation,
  type AdminTrainer,
  type LookupOutcome,
  type PendingInvitation,
} from '@/features/admin-trainers/api/trainers';
import {
  INVALID_EMAIL_MESSAGE,
  invitationValidityText,
  subscriptionStatusLabel,
  subscriptionTierLabel,
} from '@/features/admin-trainers/lib/trainer-labels';
import { isApiProblem } from '@/shared/api/problem';
import { ErrorState } from '@/shared/components/ErrorState';
import { FormField } from '@/shared/components/FormField';
import { Badge } from '@/shared/components/ui/badge';
import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';
import { formatCount, formatDate, formatLisbonDateTime } from '@/shared/lib/format';

/** Ficha de um treinador: só dados da conta, nunca clientes, planos ou sessões (RN-018). */
function TrainerCard({
  trainer,
  onToggleExemption,
}: {
  trainer: AdminTrainer;
  onToggleExemption: (trainer: AdminTrainer) => void;
}) {
  const exempt = trainer.is_exempt_from_billing;

  return (
    <article
      aria-label={trainer.full_name ?? trainer.email}
      className="border-border bg-card space-y-4 rounded-xl border p-4"
    >
      <header className="flex flex-wrap items-start justify-between gap-2">
        <div className="min-w-0">
          <h3 className="font-medium">{trainer.full_name ?? 'Sem nome'}</h3>
          <p className="text-muted-foreground text-sm break-all">{trainer.email}</p>
        </div>
        {exempt && <Badge variant="secondary">Isento</Badge>}
      </header>

      <dl className="grid gap-x-6 gap-y-2 text-sm sm:grid-cols-2">
        <div>
          <dt className="text-muted-foreground">Subscrição</dt>
          <dd>{subscriptionStatusLabel(trainer.subscription_status)}</dd>
        </div>
        <div>
          <dt className="text-muted-foreground">Plano</dt>
          <dd>{subscriptionTierLabel(trainer.subscription_tier)}</dd>
        </div>
        <div>
          <dt className="text-muted-foreground">Clientes</dt>
          <dd>{formatCount(trainer.active_client_count, 'cliente ativo', 'clientes ativos')}</dd>
        </div>
        <div>
          <dt className="text-muted-foreground">Conta</dt>
          <dd>Conta criada a {formatDate(trainer.created_at)}</dd>
        </div>
        {exempt && trainer.billing_exempt_since !== null && (
          <div>
            <dt className="text-muted-foreground">Isenção</dt>
            <dd>Isento desde {formatLisbonDateTime(trainer.billing_exempt_since)}</dd>
          </div>
        )}
      </dl>

      <Button
        variant={exempt ? 'outline' : 'default'}
        className="min-h-11"
        onClick={() => onToggleExemption(trainer)}
      >
        {exempt ? 'Tirar isenção' : 'Isentar de pagamento'}
      </Button>
    </article>
  );
}

function PendingInvitationResult({
  invitation,
  busy,
  onResend,
  onRevoke,
}: {
  invitation: PendingInvitation;
  busy: boolean;
  onResend: (invitation: PendingInvitation) => void;
  onRevoke: (invitation: PendingInvitation) => void;
}) {
  return (
    <div className="border-border bg-card space-y-3 rounded-xl border p-4">
      <p className="break-all">
        {`Convite enviado a ${invitation.email}. ${invitationValidityText(invitation.expires_at)}.`}
      </p>
      <div className="flex flex-wrap gap-2">
        <Button
          variant="outline"
          className="min-h-11"
          disabled={busy}
          onClick={() => onResend(invitation)}
        >
          Enviar outro
        </Button>
        <Button variant="outline" className="min-h-11" onClick={() => onRevoke(invitation)}>
          Anular convite
        </Button>
      </div>
    </div>
  );
}

/**
 * Procura de um treinador pelo email completo e o resultado, com as ações possíveis.
 *
 * A procura só corre ao submeter (nunca enquanto se escreve) e exige o email inteiro: o
 * superuser não pode listar contas por prefixo. O resultado vive na página, que o atualiza
 * com as respostas das ações (isentar, reenviar, anular).
 *
 * @param outcome Último resultado da procura, ou `null`.
 * @param onOutcome Guarda o resultado de uma nova procura.
 * @param onToggleExemption Abre a confirmação de isentar / tirar a isenção.
 * @param onResend Reenvia o convite pendente.
 * @param onRevoke Abre a confirmação de anular o convite.
 * @param resendBusy Há um reenvio a decorrer.
 */
const LOOKUP_ANNOUNCEMENTS = {
  found: 'Treinador encontrado.',
  not_found: 'Nenhum treinador com este email.',
  not_trainer: 'O email não é de um treinador ativo.',
  invite_pending: 'Há um convite pendente para este email.',
} as const;

export function TrainerLookup({
  outcome,
  onOutcome,
  onToggleExemption,
  onResend,
  onRevoke,
  resendBusy,
}: {
  outcome: LookupOutcome | null;
  onOutcome: (outcome: LookupOutcome | null) => void;
  onToggleExemption: (trainer: AdminTrainer) => void;
  onResend: (invitation: PendingInvitation) => void;
  onRevoke: (invitation: PendingInvitation) => void;
  resendBusy: boolean;
}) {
  const navigate = useNavigate();
  const lookup = useTrainerLookupMutation();
  const [email, setEmail] = useState('');
  const [searched, setSearched] = useState('');
  const [fieldError, setFieldError] = useState<string | null>(null);

  async function search(): Promise<void> {
    const value = email.trim();
    setFieldError(null);
    onOutcome(null);

    if (value === '') {
      setFieldError(INVALID_EMAIL_MESSAGE);
      return;
    }

    try {
      const result = await lookup.mutateAsync(value);
      setSearched(value);
      onOutcome(result);
    } catch (error) {
      // 400 de validação do email: mensagem no campo. O resto mostra-se como erro de bloco.
      if (isApiProblem(error) && error.status === 400) setFieldError(INVALID_EMAIL_MESSAGE);
    }
  }

  function onSubmit(event: FormEvent<HTMLFormElement>): void {
    event.preventDefault();
    void search();
  }

  const failed = lookup.isError && !(isApiProblem(lookup.error) && lookup.error.status === 400);

  return (
    <section aria-labelledby="trainer-lookup-title" className="space-y-4">
      <h2 id="trainer-lookup-title" className="font-display text-xl">
        Procurar treinador
      </h2>

      <form onSubmit={onSubmit} noValidate className="flex flex-wrap items-start gap-3">
        <FormField
          label="Email do treinador"
          error={fieldError ?? undefined}
          className="w-full max-w-sm"
        >
          {(control) => (
            <Input
              {...control}
              type="email"
              autoComplete="off"
              className="h-11"
              value={email}
              onChange={(event) => setEmail(event.target.value)}
            />
          )}
        </FormField>
        <Button type="submit" className="mt-6 min-h-11" disabled={lookup.isPending}>
          {lookup.isPending ? 'A procurar…' : 'Procurar'}
        </Button>
      </form>

      {/* Só uma frase curta é anunciada; o cartão tem botões e não deve ser relido inteiro. */}
      <p role="status" className="sr-only">
        {outcome === null ? '' : LOOKUP_ANNOUNCEMENTS[outcome.status]}
      </p>
      <div className="space-y-3">
        {failed && <ErrorState error={lookup.error} onRetry={() => void search()} />}

        {outcome?.status === 'found' && (
          <TrainerCard trainer={outcome.trainer} onToggleExemption={onToggleExemption} />
        )}

        {outcome?.status === 'not_found' && (
          <div className="border-border bg-card space-y-3 rounded-xl border p-4">
            <p>Não encontrámos um treinador com este email.</p>
            <Button
              className="min-h-11"
              onClick={() =>
                void navigate('/admin/trainers/invite', { state: { email: searched } })
              }
            >
              Enviar convite isento
            </Button>
          </div>
        )}

        {outcome?.status === 'not_trainer' && (
          <p className="border-border bg-card rounded-xl border p-4">
            Este email já tem uma conta que não é de treinador ativa (cliente, administrador ou
            conta apagada). Não pode ser isentada nem convidada.
          </p>
        )}

        {outcome?.status === 'invite_pending' && (
          <PendingInvitationResult
            invitation={outcome.invitation}
            busy={resendBusy}
            onResend={onResend}
            onRevoke={onRevoke}
          />
        )}
      </div>
    </section>
  );
}
