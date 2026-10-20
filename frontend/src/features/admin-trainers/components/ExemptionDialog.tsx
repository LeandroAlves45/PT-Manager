import { toast } from 'sonner';

import {
  useBillingExemptionMutation,
  type AdminTrainer,
} from '@/features/admin-trainers/api/trainers';
import {
  GENERIC_ERROR_MESSAGE,
  STRIPE_UNAVAILABLE_MESSAGE,
} from '@/features/admin-trainers/lib/trainer-labels';
import { isApiProblem } from '@/shared/api/problem';
import { ConfirmDialog } from '@/shared/components/ConfirmDialog';

/** O que o superuser vai fazer a um treinador: isentá-lo ou tirar-lhe a isenção. */
export interface ExemptionTarget {
  trainer: AdminTrainer;
  /** `true` isenta; `false` tira a isenção. Fixado ao abrir o diálogo. */
  exempt: boolean;
}

const EXEMPT_TEXT =
  'Passa ao plano Pro, sem pagamento e sem limite de clientes. Não é enviado nenhum email.';
const PAID_TEXT = 'A subscrição paga no Stripe é cancelada já, sem reembolso.';
const REMOVE_TEXT =
  'Passa ao plano Free, inativo. Não cria nem reativa clientes até pagar uma subscrição.';

function description(target: ExemptionTarget | null): string {
  if (target?.exempt === false) return REMOVE_TEXT;
  return target?.trainer.has_paid_subscription === true
    ? `${EXEMPT_TEXT} ${PAID_TEXT}`
    : EXEMPT_TEXT;
}

/**
 * Confirmação de isentar um treinador de pagamento, ou de lhe tirar a isenção.
 *
 * Controlado de fora: `target` nasce do estado da página e `onClose` é o único caminho de
 * fecho. Fecha-se explicitamente em cada saída: sucesso, treinador que já não existe e
 * cancelar. Num 503 (Stripe) fica aberto, para repetir.
 *
 * @param target Treinador e sentido da ação, ou `null` com o diálogo fechado.
 * @param onClose Fecha o diálogo.
 * @param onDone Recebe a ficha atualizada devolvida pelo servidor.
 */
export function ExemptionDialog({
  target,
  onClose,
  onDone,
}: {
  target: ExemptionTarget | null;
  onClose: () => void;
  onDone: (trainer: AdminTrainer) => void;
}) {
  const mutation = useBillingExemptionMutation();

  async function confirm(): Promise<void> {
    if (target === null) return;

    try {
      const updated = await mutation.mutateAsync({
        trainerId: target.trainer.trainer_id,
        exempt: target.exempt,
      });
      toast.success(target.exempt ? 'Treinador isento de pagamento.' : 'Isenção retirada.');
      onDone(updated);
      onClose();
    } catch (error) {
      if (isApiProblem(error) && error.code === 'admin_trainer_payment_provider_unavailable') {
        toast.error(STRIPE_UNAVAILABLE_MESSAGE);
      } else if (isApiProblem(error) && error.code === 'admin_trainer_not_found') {
        toast.error('Este treinador já não existe.');
        onClose();
      } else {
        toast.error(GENERIC_ERROR_MESSAGE);
      }
    }
  }

  const name = target?.trainer.full_name ?? target?.trainer.email ?? '';

  return (
    <ConfirmDialog
      open={target !== null}
      onOpenChange={(open) => {
        if (!open) onClose();
      }}
      title={
        target?.exempt === false ? `Tirar a isenção a ${name}?` : `Isentar ${name} de pagamento?`
      }
      description={description(target)}
      confirmLabel={target?.exempt === false ? 'Tirar isenção' : 'Isentar'}
      pendingLabel="A guardar…"
      pending={mutation.isPending}
      destructive={target?.exempt === true && target.trainer.has_paid_subscription}
      onConfirm={() => void confirm()}
    />
  );
}
