import { Archive, ClipboardList, Copy, Mail, Pencil, RotateCcw } from 'lucide-react';
import { parseAsStringLiteral, useQueryState } from 'nuqs';
import { useState, type ReactNode } from 'react';
import { Link, useParams } from 'react-router';
import { toast } from 'sonner';

import {
  useClientActivityMutation,
  useInviteClientMutation,
} from '@/features/clients/api/mutations';
import {
  useClientInvitationQuery,
  useClientQuery,
  useClientSummaryQuery,
} from '@/features/clients/api/queries';
import { formatInviteMoment, invitationNote } from '@/features/clients/lib/invitation';
import { activityErrorMessage, ageFrom, initialsOf } from '@/features/clients/lib/labels';
import { ClientCheckInsTab } from '@/features/check-ins';
import { ClientSessionsTab } from '@/features/sessions';
import { useTrainerSettingsQuery } from '@/features/trainer-settings/api/settings';
import {
  MealPlansPage,
  SupplementAssignmentsPage,
  TrainingPlansPage,
} from '@/features/prescriptions';
import { isApiProblem } from '@/shared/api/problem';
import type { components } from '@/shared/api/schema';
import { ConfirmDialog } from '@/shared/components/ConfirmDialog';
import { ErrorState } from '@/shared/components/ErrorState';
import { Badge } from '@/shared/components/ui/badge';
import { Button } from '@/shared/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/shared/components/ui/dialog';
import { Input } from '@/shared/components/ui/input';
import { Skeleton } from '@/shared/components/ui/skeleton';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/shared/components/ui/tabs';
import { formatDate, formatNumber, formatPercent } from '@/shared/lib/format';

type ClientDetails = components['schemas']['ClientDetailsResponse'];
type Summary = components['schemas']['ClientSummaryOverviewResponse'];

/** Mensagens das recusas do convite (`AuthenticationErrors`). */
const INVITE_ERRORS: Readonly<Record<string, string>> = {
  authentication_invitation_email_mismatch:
    'O email do convite tem de ser o email de contacto do cliente.',
  authentication_relationship_conflict: 'Este cliente já tem conta no portal.',
  authentication_client_inactive: 'Reativa o cliente antes de o convidar.',
  authentication_email_delivery_unavailable:
    'O email de convite não pôde ser enviado agora. Tenta mais tarde.',
  rate_limit_exceeded: 'Foram enviados demasiados convites. Aguarda um momento.',
};

/** Convite acabado de enviar: o link só existe aqui, nunca fica escrito no ecrã. */
interface SentInvite {
  readonly email: string;
  readonly url: string;
  readonly expiresAt: string;
  /** `false` quando o email não saiu: o convite serve na mesma e o link tem de ir por outro canal. */
  readonly emailSent: boolean;
}
type Confirm = 'invite' | 'activity' | null;

/** Separadores do detalhe; pela ordem do layout com Sessões a seguir ao Resumo */
const TABS = ['summary', 'sessions', 'training', 'nutrition', 'supplements', 'check-ins'] as const;

/**
 * Detalhe do cliente — cabeçalho e separadores Resumo, Sessões, Treino, Nutrição,
 * Suplementos e Avaliações (`?tab=check-ins`).
 *
 * Orçamento de pedidos: `GET /clients/{id}` e `GET /clients/{id}/summary` em paralelo ao
 * montar; a ficha e a avaliação inicial editam-se em páginas próprias; o estado do convite
 * (`GET /clients/{id}/invitation`) só se pede enquanto o cliente não tem conta; cada
 * separador só pede os seus dados quando está aberto (o Radix não monta separadores
 * inativos).
 */
export function ClientDetailPage() {
  const { clientId = '' } = useParams();
  const client = useClientQuery(clientId);
  const summary = useClientSummaryQuery(clientId);
  const [sent, setSent] = useState<SentInvite | null>(null);
  const [confirm, setConfirm] = useState<Confirm>(null);
  const [tab, setTab] = useQueryState('tab', parseAsStringLiteral(TABS).withDefault('summary'));
  const settings = useTrainerSettingsQuery();
  const timeZone = settings.data?.timezone;
  const invite = useInviteClientMutation();
  const invitation = useClientInvitationQuery(clientId, client.data?.user_id === null);
  const activity = useClientActivityMutation();

  if (client.isPending)
    return (
      <section role="status" aria-label="A carregar cliente…" className="space-y-6">
        <Skeleton className="h-16 w-full" />
        <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
          {Array.from({ length: 4 }, (_, index) => (
            <Skeleton key={index} className="h-28" />
          ))}
        </div>
      </section>
    );
  if (client.isError)
    return (
      <ErrorState
        title={
          isApiProblem(client.error) && client.error.status === 404
            ? 'Cliente não encontrado.'
            : 'Não foi possível carregar o cliente.'
        }
        error={client.error}
        onRetry={() => void client.refetch()}
      />
    );

  const data = client.data;
  const canInvite = data.is_active && data.user_id === null && data.contact_email !== null;
  // Sem conta, o convite é sempre possível de alguma forma: em vez de esconder o botão quando
  // ainda não dá, mostra-se desativado com o que falta fazer.
  const inviteBlocked =
    data.user_id !== null
      ? null
      : !data.is_active
        ? 'Reativa o cliente para o convidar para o portal.'
        : data.contact_email === null
          ? 'Adiciona um email de contacto na ficha para o convidar para o portal.'
          : null;
  const inviteExpired = data.user_id === null && invitation.data?.state === 'expired';
  const inviteNote =
    data.user_id === null ? invitationNote(invitation.data, data.contact_email, timeZone) : null;

  async function sendInvite() {
    if (data.contact_email === null) return;

    try {
      const result = await invite.mutateAsync({ clientId: data.id, email: data.contact_email });
      setConfirm(null);
      setSent({
        email: data.contact_email,
        url: result.invite_url,
        expiresAt: result.expires_at,
        emailSent: result.email_sent,
      });
    } catch (error) {
      const message = isApiProblem(error) ? INVITE_ERRORS[error.code] : undefined;
      toast.error(message ?? 'Não foi possível enviar o convite. Tenta novamente.');
    }
  }

  async function toggleActivity() {
    try {
      await activity.mutateAsync({
        clientId: data.id,
        action: data.is_active ? 'archive' : 'reactivate',
      });
      toast.success(
        data.is_active ? 'Cliente arquivado com sucesso.' : 'Cliente reativado com sucesso.'
      );
      setConfirm(null);
    } catch (error) {
      toast.error(activityErrorMessage(error));
    }
  }

  return (
    <section className="space-y-6">
      <ClientHeader
        client={data}
        summary={summary.data}
        inviteNote={inviteNote}
        actions={
          <>
            <Button asChild variant="outline">
              <Link to={`/trainer/clients/${data.id}/edit`}>
                <Pencil aria-hidden /> Editar ficha
              </Link>
            </Button>
            <Button asChild variant="outline">
              <Link to={`/trainer/clients/${data.id}/initial-assessment`}>
                <ClipboardList aria-hidden /> Avaliação inicial
              </Link>
            </Button>
            {canInvite && (
              <Button onClick={() => setConfirm('invite')}>
                <Mail aria-hidden />{' '}
                {inviteExpired ? 'Enviar outro convite' : 'Convidar para o portal'}
              </Button>
            )}
            {inviteBlocked !== null && (
              <Button disabled aria-describedby="invite-blocked">
                <Mail aria-hidden /> Convidar para o portal
              </Button>
            )}
            <Button
              variant="ghost"
              aria-label={data.is_active ? 'Arquivar cliente' : 'Reativar cliente'}
              onClick={() => setConfirm('activity')}
            >
              {data.is_active ? <Archive aria-hidden /> : <RotateCcw aria-hidden />}
              {data.is_active ? 'Arquivar' : 'Reativar'}
            </Button>
            {inviteBlocked !== null && (
              <p id="invite-blocked" className="text-muted-foreground w-full text-sm">
                {inviteBlocked}{' '}
                {data.is_active && (
                  <Link
                    to={`/trainer/clients/${data.id}/edit`}
                    className="text-primary underline-offset-2 hover:underline"
                  >
                    Editar ficha
                  </Link>
                )}
              </p>
            )}
          </>
        }
      />

      <Tabs
        value={tab}
        onValueChange={(value) => {
          const next = TABS.find((item) => item === value) ?? 'summary';
          void setTab(next === 'summary' ? null : next);
        }}
      >
        <TabsList>
          <TabsTrigger value="summary">Resumo</TabsTrigger>
          <TabsTrigger value="sessions">Sessões</TabsTrigger>
          <TabsTrigger value="training">Treino</TabsTrigger>
          <TabsTrigger value="nutrition">Nutrição</TabsTrigger>
          <TabsTrigger value="supplements">Suplementos</TabsTrigger>
          <TabsTrigger value="check-ins">Avaliações</TabsTrigger>
        </TabsList>
        <TabsContent value="summary">
          {summary.isPending ? (
            <div
              role="status"
              aria-label="A carregar resumo…"
              className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4"
            >
              {Array.from({ length: 4 }, (_, index) => (
                <Skeleton key={index} className="h-28" />
              ))}
            </div>
          ) : summary.isError ? (
            <ErrorState error={summary.error} onRetry={() => void summary.refetch()} />
          ) : (
            <SummaryOverview summary={summary.data} client={data} />
          )}
        </TabsContent>
        <TabsContent value="sessions">
          <ClientSessionsTab
            client={{ id: data.id, name: data.name }}
            canSchedule={data.is_active}
          />
        </TabsContent>
        <TabsContent value="training">
          <TrainingPlansPage client={{ id: data.id, name: data.name }} />
        </TabsContent>
        <TabsContent value="nutrition">
          <MealPlansPage client={{ id: data.id, name: data.name }} />
        </TabsContent>
        <TabsContent value="supplements">
          <SupplementAssignmentsPage client={{ id: data.id, name: data.name }} />
        </TabsContent>
        <TabsContent value="check-ins">
          <ClientCheckInsTab
            client={{ id: data.id, name: data.name }}
            canSchedule={data.is_active}
          />
        </TabsContent>
      </Tabs>

      <ConfirmDialog
        open={confirm === 'invite'}
        onOpenChange={(open) => {
          if (!open) setConfirm(null);
        }}
        title="Convidar para o portal?"
        description={`Enviamos um convite para ${data.contact_email ?? ''}. O cliente cria a palavra-passe e passa a ver os planos no portal.`}
        confirmLabel={inviteExpired ? 'Enviar outro convite' : 'Enviar convite'}
        pendingLabel="A enviar…"
        pending={invite.isPending}
        onConfirm={() => void sendInvite()}
      />
      <InviteSentDialog sent={sent} timeZone={timeZone} onClose={() => setSent(null)} />
      <ConfirmDialog
        open={confirm === 'activity'}
        onOpenChange={(open) => {
          if (!open) setConfirm(null);
        }}
        title={data.is_active ? 'Arquivar cliente?' : 'Reativar cliente?'}
        description={
          data.is_active
            ? `${data.name} deixa de contar para o limite do plano e sai das listas ativas. Podes reativá-lo mais tarde.`
            : `${data.name} volta às listas ativas e conta para o limite do plano.`
        }
        confirmLabel={data.is_active ? 'Arquivar' : 'Reativar'}
        destructive={data.is_active}
        pending={activity.isPending}
        onConfirm={() => void toggleActivity()}
      />
    </section>
  );
}

/** Cabeçalho: avatar 56 px, nome, badges e linha de contexto. */
function ClientHeader({
  client,
  summary,
  inviteNote,
  actions,
}: {
  client: ClientDetails;
  summary: Summary | undefined;
  inviteNote: string | null;
  actions: ReactNode;
}) {
  const remaining = client.usable_packs.reduce((sum, pack) => sum + pack.sessions_remaining, 0);
  const context = [
    `${ageFrom(client.birth_date)} anos`,
    summary?.weight != null ? `${formatNumber(summary.weight.current_kg, 1)} kg` : null,
    summary?.height_cm != null ? `${summary.height_cm} cm` : null,
    `cliente desde ${formatDate(client.created_at)}`,
    client.objective !== null ? `objetivo: ${client.objective}` : null,
  ].filter((value): value is string => value !== null);

  return (
    <header className="flex flex-wrap items-start justify-between gap-4">
      <div className="flex min-w-0 items-center gap-4">
        <div
          aria-hidden
          className="bg-muted flex size-14 shrink-0 items-center justify-center rounded-full text-xl font-semibold"
        >
          {initialsOf(client.name)}
        </div>
        <div className="min-w-0 space-y-1">
          <div className="flex flex-wrap items-center gap-2">
            <h1 className="font-display truncate py-1 text-3xl leading-tight">{client.name}</h1>
            <Badge variant={client.is_active ? 'secondary' : 'outline'}>
              {client.is_active ? 'Ativo' : 'Arquivado'}
            </Badge>
            {client.usable_packs.length > 0 && (
              <Badge variant="outline">
                {client.usable_packs.length === 1
                  ? client.usable_packs[0]?.name
                  : `${client.usable_packs.length} packs`}{' '}
                · {remaining} {remaining === 1 ? 'restante' : 'restantes'}
              </Badge>
            )}
            {client.user_id !== null && <Badge variant="outline">Com acesso ao portal</Badge>}
          </div>
          <p className="text-muted-foreground font-mono text-xs">{context.join(' · ')}</p>
          {inviteNote !== null && <p className="text-muted-foreground text-xs">{inviteNote}</p>}
        </div>
      </div>
      <div className="flex flex-wrap gap-2">{actions}</div>
    </header>
  );
}

/**
 * Diálogo de convite enviado: email, validade no fuso do personal trainer e «Copiar link».
 *
 * O link vive só no estado desta página: ao fechar, `sent` volta a `null` e não se recupera
 * (outro link pede-se com outro convite, que anula este). Se a área de transferência
 * falhar, o link aparece num campo só de leitura para o personal trainer o copiar à mão.
 */
function InviteSentDialog({
  sent,
  timeZone,
  onClose,
}: {
  sent: SentInvite | null;
  timeZone: string | undefined;
  onClose: () => void;
}) {
  const [copy, setCopy] = useState<'idle' | 'copied' | 'failed'>('idle');

  async function copyLink() {
    if (sent === null) return;
    try {
      await navigator.clipboard.writeText(sent.url);
      setCopy('copied');
    } catch {
      setCopy('failed');
    }
  }

  function close() {
    setCopy('idle');
    onClose();
  }

  return (
    <Dialog
      open={sent !== null}
      onOpenChange={(open) => {
        if (!open) close();
      }}
    >
      <DialogContent showCloseButton={false}>
        <DialogHeader>
          <DialogTitle>
            {sent?.emailSent === false ? 'Convite criado' : 'Convite enviado'}
          </DialogTitle>
          <DialogDescription>
            {sent?.emailSent === false
              ? `O email para ${sent.email} não saiu agora.`
              : `Convite enviado para ${sent?.email ?? ''}.`}{' '}
            {sent !== null && `Válido até ${formatInviteMoment(sent.expiresAt, timeZone)}`}
          </DialogDescription>
        </DialogHeader>
        {sent?.emailSent === false ? (
          <p role="alert" className="text-warning text-sm">
            O convite está criado e o link funciona: copia-o e envia-o por outro canal (por exemplo,
            WhatsApp). Depois de fechares este aviso o link não volta a aparecer.
          </p>
        ) : (
          <p className="text-muted-foreground text-sm">
            Podes também copiar o link e enviá-lo por outro canal. Depois de fechares este aviso o
            link não volta a aparecer; para outro link, envia outro convite (anula este).
          </p>
        )}
        {copy === 'failed' && sent !== null && (
          <div className="space-y-2">
            <p role="alert" className="text-destructive text-sm">
              Não foi possível copiar o link. Copia-o daqui.
            </p>
            <Input
              readOnly
              aria-label="Link do convite"
              value={sent.url}
              onFocus={(event) => event.currentTarget.select()}
            />
          </div>
        )}
        <p role="status" aria-live="polite" className="sr-only">
          {copy === 'copied' ? 'Link copiado para a área de transferência.' : ''}
        </p>
        <DialogFooter>
          <Button variant="outline" onClick={close}>
            Fechar
          </Button>
          <Button onClick={() => void copyLink()}>
            <Copy aria-hidden /> {copy === 'copied' ? 'Copiado' : 'Copiar link'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

/** Um KPI do resumo: rótulo, valor display e linha de contexto. */
function Kpi({ label, value, detail }: { label: string; value: string; detail: string }) {
  return (
    <div className="bg-card border-border rounded-xl border p-4">
      <p className="text-muted-foreground font-mono text-xs uppercase">{label}</p>
      <p className="font-display mt-2 text-3xl tabular-nums">{value}</p>
      <p className="text-muted-foreground mt-1 text-xs">{detail}</p>
    </div>
  );
}

/**
 * Separador "Resumo": quatro KPIs (peso, adesão, kcal alvo, sessões restantes), plano de
 * treino ativo e dados de contacto. Cada `null` do resumo 6B tem texto próprio em vez de
 * um zero inventado.
 */
function SummaryOverview({ summary, client }: { summary: Summary; client: ClientDetails }) {
  const { weight, adherence, nutrition, packs, training_plan: plan } = summary;

  const weightDetail =
    weight === null
      ? 'Sem registos de peso'
      : weight.change_kg !== null && weight.change_since !== null
        ? `${weight.change_kg > 0 ? '+' : ''}${formatNumber(weight.change_kg, 1)} kg desde ${formatDate(weight.change_since)}`
        : weight.source === 'initial_assessment'
          ? 'da avaliação inicial'
          : `medido a ${formatDate(weight.measured_on)}`;

  return (
    <div className="space-y-4">
      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <Kpi
          label="Peso atual"
          value={weight === null ? '—' : `${formatNumber(weight.current_kg, 1)} kg`}
          detail={weightDetail}
        />
        <Kpi
          label="Adesão ao treino"
          value={formatPercent(adherence?.percentage ?? null)}
          detail={
            adherence === null
              ? 'Sem plano de treino ativo'
              : `${adherence.logged_sets} de ${adherence.planned_sets} séries (28 dias)`
          }
        />
        <Kpi
          label="Kcal alvo"
          value={nutrition === null ? '—' : formatNumber(nutrition.target_kcal)}
          detail={
            nutrition === null
              ? 'Sem plano alimentar ativo'
              : `${formatNumber(nutrition.carbs_target_grams)} g HC · ${formatNumber(nutrition.protein_target_grams)} g P · ${formatNumber(nutrition.fats_target_grams)} g G`
          }
        />
        <Kpi
          label="Sessões restantes"
          value={formatNumber(packs.sessions_remaining)}
          detail={
            packs.usable_pack_count === 0
              ? 'Sem packs ativos'
              : packs.next_expected_end_date !== null
                ? `fim previsto ${formatDate(packs.next_expected_end_date)}`
                : `${packs.usable_pack_count} ${packs.usable_pack_count === 1 ? 'pack ativo' : 'packs ativos'}`
          }
        />
      </div>
      <div className="grid gap-4 lg:grid-cols-[1.4fr_1fr]">
        <section className="bg-card border-border rounded-xl border p-4">
          <h3 className="font-medium">Plano de treino ativo</h3>
          {plan === null ? (
            <p className="text-muted-foreground mt-2 text-sm">Este cliente não tem plano ativo.</p>
          ) : (
            <dl className="mt-2 grid grid-cols-2 gap-2 text-sm">
              <dt className="text-muted-foreground">Nome</dt>
              <dd>{plan.name}</dd>
              <dt className="text-muted-foreground">Dias por semana</dt>
              <dd>{plan.days_per_week}</dd>
              <dt className="text-muted-foreground">Validade</dt>
              <dd>
                {plan.end_date === null
                  ? `desde ${formatDate(plan.start_date)}`
                  : `válido até ${formatDate(plan.end_date)}`}
              </dd>
            </dl>
          )}
        </section>
        <section className="bg-card border-border rounded-xl border p-4">
          <h3 className="font-medium">Contactos</h3>
          <dl className="mt-2 grid grid-cols-2 gap-2 text-sm">
            <dt className="text-muted-foreground">Telefone</dt>
            <dd>{client.phone}</dd>
            <dt className="text-muted-foreground">Email</dt>
            <dd className="break-all">{client.contact_email ?? '—'}</dd>
            <dt className="text-muted-foreground">Emergência</dt>
            <dd>
              {client.emergency_contact_name ?? '—'}
              {client.emergency_contact_phone !== null && ` · ${client.emergency_contact_phone}`}
            </dd>
          </dl>
          {client.notes !== null && (
            <p className="text-muted-foreground mt-3 text-sm whitespace-pre-line">{client.notes}</p>
          )}
        </section>
      </div>
    </div>
  );
}
