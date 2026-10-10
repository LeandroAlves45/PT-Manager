import { useState } from 'react';
import { toast } from 'sonner';

import { useCompleteWorkoutMutation } from '@/features/portal/api/portal';
import { countLabel, workoutFailureMessage } from '@/features/portal/lib/workout';
import { FormField } from '@/shared/components/FormField';
import { Button } from '@/shared/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/shared/components/ui/dialog';
import { Textarea } from '@/shared/components/ui/textarea';

/** Limite de `CompleteMyWorkoutRequest.notes` no backend. */
const NOTES_MAX_LENGTH = 500;

/**
 * Confirmação de "Concluir treino": pede sempre confirmação e aceita notas para
 * o personal trainer.
 *
 * Com séries por registar, diz quantas faltam (o treino parcial é permitido). Avisa que depois
 * de concluir já não se desmarcam séries. Fica aberto enquanto o pedido corre e se falhar, com
 * as notas intactas.
 *
 * @param dayId `day.id` do treino de hoje (`training_plan_day_id`).
 * @param remainingSets Séries planeadas ainda sem registo.
 */
export function CompleteWorkoutDialog({
  open,
  onOpenChange,
  dayId,
  remainingSets,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  dayId: string;
  remainingSets: number;
}) {
  const [notes, setNotes] = useState('');
  const [error, setError] = useState<string | null>(null);
  const complete = useCompleteWorkoutMutation();

  /** Fecha sem perder as notas; o erro de uma tentativa anterior não volta a aparecer. */
  function close() {
    setError(null);
    onOpenChange(false);
  }

  function confirm() {
    setError(null);
    complete.mutate(
      { dayId, notes: notes.trim() === '' ? null : notes.trim() },
      {
        onSuccess: () => {
          toast.success('Treino concluído. Bom trabalho!');
          onOpenChange(false);
        },
        onError: (failure) => setError(workoutFailureMessage(failure)),
      }
    );
  }

  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        if (complete.isPending) return;
        if (next) onOpenChange(true);
        else close();
      }}
    >
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Concluir treino</DialogTitle>
          <DialogDescription>
            {remainingSets > 0
              ? `Faltam ${countLabel(remainingSets, 'série', 'séries')} por registar. Podes concluir na mesma.`
              : 'Registaste todas as séries.'}{' '}
            Depois de concluir, já não podes desmarcar séries.
          </DialogDescription>
        </DialogHeader>

        <FormField label="Notas para o teu personal trainer (opcional)">
          {(control) => (
            <Textarea
              {...control}
              value={notes}
              maxLength={NOTES_MAX_LENGTH}
              rows={3}
              disabled={complete.isPending}
              onChange={(event) => setNotes(event.target.value)}
            />
          )}
        </FormField>

        {error !== null && (
          <p role="alert" className="text-destructive text-sm">
            {error}
          </p>
        )}

        <DialogFooter>
          <Button type="button" variant="outline" disabled={complete.isPending} onClick={close}>
            Cancelar
          </Button>
          <Button type="button" disabled={complete.isPending} onClick={confirm}>
            {complete.isPending ? 'A concluir…' : 'Concluir treino'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
