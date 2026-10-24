import { useState } from 'react';
import { toast } from 'sonner';

import { useAccessBlockMutation, type AdminTrainer } from '@/features/admin-trainers/api/trainers';
import { accessBlockFailureMessage } from '@/features/admin-trainers/lib/trainer-labels';
import { isApiProblem } from '@/shared/api/problem';
import { ConfirmDialog } from '@/shared/components/ConfirmDialog';
import { PlatformEnforcementFields } from '@/shared/components/PlatformEnforcementFields';
import { Button } from '@/shared/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/shared/components/ui/dialog';
import {
  platformEnforcementNoteOrNull,
  type PlatformEnforcementReason,
} from '@/shared/lib/platformEnforcement';

/** O que o superuser vai fazer a uma conta: bloqueá-la ou levantar-lhe o bloqueio. */
export interface AccessBlockTarget {
  trainer: AdminTrainer;
  /** `true` bloqueia; `false` levanta o bloqueio. Fixado ao abrir o diálogo. */
  block: boolean;
}

const BLOCK_TEXT =
  'O personal trainer deixa de entrar já. Os clientes com conta continuam a entrar durante 7 dias. Esta decisão fica registada na auditoria administrativa.';
const LIFT_TEXT =
  'O acesso volta já, para o personal trainer e para os clientes. Esta decisão fica registada na auditoria administrativa.';

/** Formulário de bloqueio; vive dentro do conteúdo do diálogo, por isso reinicia a cada abertura. */
function BlockForm({
  pending,
  onCancel,
  onSubmit,
}: {
  pending: boolean;
  onCancel: () => void;
  onSubmit: (reason: PlatformEnforcementReason, note: string | null) => void;
}) {
  const [reason, setReason] = useState<PlatformEnforcementReason | ''>('');
  const [note, setNote] = useState('');

  return (
    <>
      <PlatformEnforcementFields
        reason={reason}
        note={note}
        onReasonChange={setReason}
        onNoteChange={setNote}
        disabled={pending}
      />
      <DialogFooter>
        <Button variant="outline" disabled={pending} onClick={onCancel}>
          Cancelar
        </Button>
        <Button
          variant="destructive"
          disabled={pending || reason === ''}
          onClick={() => {
            if (reason !== '') onSubmit(reason, platformEnforcementNoteOrNull(note));
          }}
        >
          {pending ? 'A guardar…' : 'Bloquear conta'}
        </Button>
      </DialogFooter>
    </>
  );
}

/**
 * Diálogo de bloquear a conta de um treinador (motivo obrigatório e nota opcional) ou de
 * levantar o bloqueio (confirmação simples).
 *
 * Controlado de fora: `target` nasce do estado da página e `onClose` é o único caminho de
 * fecho. Fecha-se explicitamente no sucesso, num treinador que já não existe e ao cancelar;
 * nos outros erros fica aberto, para repetir.
 *
 * @param target Treinador e sentido da ação, ou `null` com o diálogo fechado.
 * @param onClose Fecha o diálogo.
 * @param onDone Recebe a ficha atualizada devolvida pelo servidor.
 */
export function AccessBlockDialog({
  target,
  onClose,
  onDone,
}: {
  target: AccessBlockTarget | null;
  onClose: () => void;
  onDone: (trainer: AdminTrainer) => void;
}) {
  const mutation = useAccessBlockMutation();

  async function run(block: { reasonCode: string; note: string | null } | null): Promise<void> {
    if (target === null) return;

    try {
      const updated = await mutation.mutateAsync({ trainerId: target.trainer.trainer_id, block });
      toast.success(block !== null ? 'Conta bloqueada.' : 'Bloqueio levantado.');
      onDone(updated);
      onClose();
    } catch (error) {
      toast.error(accessBlockFailureMessage(error));
      if (isApiProblem(error) && error.code === 'admin_trainer_not_found') onClose();
    }
  }

  const name = target?.trainer.full_name ?? target?.trainer.email ?? '';

  return (
    <>
      <ConfirmDialog
        open={target?.block === false}
        onOpenChange={(open) => {
          if (!open) onClose();
        }}
        title={`Levantar o bloqueio a ${name}?`}
        description={LIFT_TEXT}
        confirmLabel="Levantar bloqueio"
        pendingLabel="A guardar…"
        pending={mutation.isPending}
        onConfirm={() => void run(null)}
      />
      <Dialog
        open={target?.block === true}
        onOpenChange={(open) => {
          if (!open && !mutation.isPending) onClose();
        }}
      >
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{`Bloquear a conta de ${name}?`}</DialogTitle>
            <DialogDescription>{BLOCK_TEXT}</DialogDescription>
          </DialogHeader>
          <BlockForm
            pending={mutation.isPending}
            onCancel={onClose}
            onSubmit={(reason, note) => void run({ reasonCode: reason, note })}
          />
        </DialogContent>
      </Dialog>
    </>
  );
}
