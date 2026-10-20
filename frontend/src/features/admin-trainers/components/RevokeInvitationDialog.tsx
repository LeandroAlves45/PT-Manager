import { toast } from 'sonner';

import {
  useRevokeTrainerInvitationMutation,
  type PendingInvitation,
} from '@/features/admin-trainers/api/trainers';
import { GENERIC_ERROR_MESSAGE } from '@/features/admin-trainers/lib/trainer-labels';
import { isApiProblem } from '@/shared/api/problem';
import { ConfirmDialog } from '@/shared/components/ConfirmDialog';

/**
 * Confirmação de anular um convite pendente: o link enviado deixa de servir.
 *
 * Controlado de fora (`invitation` nasce do estado da página). Fecha-se em cada saída bem
 * sucedida; um 404 (o convite já não existe) também conta, porque o resultado pedido é o
 * mesmo.
 *
 * @param invitation Convite a anular, ou `null` com o diálogo fechado.
 * @param onClose Fecha o diálogo.
 * @param onDone Chamado depois de o convite ter sido anulado.
 */
export function RevokeInvitationDialog({
  invitation,
  onClose,
  onDone,
}: {
  invitation: PendingInvitation | null;
  onClose: () => void;
  onDone: (invitation: PendingInvitation) => void;
}) {
  const mutation = useRevokeTrainerInvitationMutation();

  async function confirm(): Promise<void> {
    if (invitation === null) return;

    try {
      await mutation.mutateAsync(invitation.invitation_id);
      toast.success('Convite anulado.');
      onDone(invitation);
      onClose();
    } catch (error) {
      if (isApiProblem(error) && error.code === 'trainer_invitation_not_found') {
        toast.error('O convite já não existe.');
        onDone(invitation);
        onClose();
      } else {
        toast.error(GENERIC_ERROR_MESSAGE);
      }
    }
  }

  return (
    <ConfirmDialog
      open={invitation !== null}
      onOpenChange={(open) => {
        if (!open) onClose();
      }}
      title="Anular convite?"
      description={`O link enviado a ${invitation?.email ?? ''} deixa de funcionar.`}
      confirmLabel="Anular convite"
      pendingLabel="A anular…"
      destructive
      pending={mutation.isPending}
      onConfirm={() => void confirm()}
    />
  );
}
