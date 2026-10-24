import { useState } from 'react';
import { Link } from 'react-router';
import { toast } from 'sonner';

import {
  useSendTrainerInvitationMutation,
  type AdminTrainer,
  type LookupOutcome,
  type PendingInvitation,
} from '@/features/admin-trainers/api/trainers';
import {
  AccessBlockDialog,
  type AccessBlockTarget,
} from '@/features/admin-trainers/components/AccessBlockDialog';
import {
  ExemptionDialog,
  type ExemptionTarget,
} from '@/features/admin-trainers/components/ExemptionDialog';
import { ExemptTrainersSection } from '@/features/admin-trainers/components/ExemptTrainersSection';
import { PendingInvitationsSection } from '@/features/admin-trainers/components/PendingInvitationsSection';
import { RevokeInvitationDialog } from '@/features/admin-trainers/components/RevokeInvitationDialog';
import { TrainerLookup } from '@/features/admin-trainers/components/TrainerLookup';
import { sendInvitationMessage } from '@/features/admin-trainers/lib/trainer-labels';
import { PageHeader } from '@/shared/components/PageHeader';
import { Button } from '@/shared/components/ui/button';

/**
 * Treinadores (superuser): procurar por email, isentar de pagamento, bloquear e convidar.
 *
 * A página é dona do resultado da procura e dos dois diálogos de confirmação, porque a mesma
 * ação (tirar a isenção, anular um convite) se pede de três sítios: o resultado da procura e
 * as duas listas. Quem pede só abre o diálogo; a resposta do servidor atualiza o resultado.
 */
export function AdminTrainersPage() {
  const [outcome, setOutcome] = useState<LookupOutcome | null>(null);
  const [exemption, setExemption] = useState<ExemptionTarget | null>(null);
  const [accessBlock, setAccessBlock] = useState<AccessBlockTarget | null>(null);
  const [revoking, setRevoking] = useState<PendingInvitation | null>(null);
  const send = useSendTrainerInvitationMutation();

  function openExemption(trainer: AdminTrainer): void {
    setExemption({ trainer, exempt: !trainer.is_exempt_from_billing });
  }

  function openAccessBlock(trainer: AdminTrainer): void {
    setAccessBlock({ trainer, block: trainer.access_block === null });
  }

  /** Troca a ficha mostrada pela devolvida pelo servidor, se ainda for a do mesmo treinador. */
  function replaceFound(trainer: AdminTrainer): void {
    setOutcome((current) =>
      current?.status === 'found' && current.trainer.trainer_id === trainer.trainer_id
        ? { status: 'found', trainer }
        : current
    );
  }

  async function resend(invitation: PendingInvitation): Promise<void> {
    try {
      const sent = await send.mutateAsync(invitation.email);
      toast.success('Convite reenviado. O link anterior deixou de servir.');
      setOutcome((current) =>
        current?.status === 'invite_pending' && current.invitation.email === sent.email
          ? { status: 'invite_pending', invitation: sent }
          : current
      );
    } catch (error) {
      toast.error(sendInvitationMessage(error));
    }
  }

  return (
    <section className="space-y-8">
      <PageHeader
        title="Treinadores"
        description="Procura um treinador pelo email completo. Isenta de pagamento ou envia um convite isento."
        action={
          <Button asChild className="min-h-11">
            <Link to="/admin/trainers/invite">Convidar treinador</Link>
          </Button>
        }
      />

      <TrainerLookup
        outcome={outcome}
        onOutcome={setOutcome}
        onToggleExemption={openExemption}
        onToggleBlock={openAccessBlock}
        onResend={(invitation) => void resend(invitation)}
        onRevoke={setRevoking}
        resendBusy={send.isPending}
      />

      <ExemptTrainersSection onRemoveExemption={openExemption} />

      <PendingInvitationsSection
        busy={send.isPending}
        onResend={(invitation) => void resend(invitation)}
        onRevoke={setRevoking}
      />

      <ExemptionDialog
        target={exemption}
        onClose={() => setExemption(null)}
        onDone={replaceFound}
      />

      <AccessBlockDialog
        target={accessBlock}
        onClose={() => setAccessBlock(null)}
        onDone={replaceFound}
      />

      <RevokeInvitationDialog
        invitation={revoking}
        onClose={() => setRevoking(null)}
        onDone={(invitation) =>
          setOutcome((current) =>
            current?.status === 'invite_pending' &&
            current.invitation.invitation_id === invitation.invitation_id
              ? { status: 'not_found' }
              : current
          )
        }
      />
    </section>
  );
}
