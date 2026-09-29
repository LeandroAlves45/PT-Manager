import { useState } from 'react';
import { toast } from 'sonner';

import { useUpdatePackEndDateMutation } from '@/features/packs/api/mutations';
import { isApiProblem } from '@/shared/api/problem';
import type { components } from '@/shared/api/schema';
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
import { Input } from '@/shared/components/ui/input';
import { formatDate } from '@/shared/lib/format';

type ClientPack = components['schemas']['ClientSessionPackResponse'];

/**
 * Diálogo "Alterar fim previsto" de um pack.
 *
 * Vazio remove a data (o pack deixa de aparecer nos "a terminar" por data). O estado do
 * campo vive dentro do diálogo e nasce de novo em cada abertura (`key` de quem chama), por
 * isso cancelar nunca deixa um valor antigo para a próxima vez.
 *
 * @param pack Pack a alterar, ou `null` com o diálogo fechado.
 */
export function PackEndDateDialog({
  pack,
  onClose,
}: {
  pack: ClientPack | null;
  onClose: () => void;
}) {
  const [value, setValue] = useState(pack?.expected_end_date ?? '');
  const [error, setError] = useState<string | undefined>(undefined);
  const mutation = useUpdatePackEndDateMutation();

  async function save() {
    if (pack === null) return;
    if (value !== '' && value < pack.purchase_date) {
      setError('O fim previsto não pode ser antes da data de compra.');
      return;
    }

    try {
      await mutation.mutateAsync({ packId: pack.id, expectedEndDate: value === '' ? null : value });
      toast.success(value === '' ? 'Fim previsto removido.' : 'Fim previsto alterado.');
      onClose();
    } catch (failure) {
      setError(
        isApiProblem(failure) && failure.code === 'expected_end_date_before_purchase'
          ? 'O fim previsto não pode ser antes da data de compra.'
          : 'Não foi possível guardar. Tenta novamente.'
      );
    }
  }

  return (
    <Dialog
      open={pack !== null}
      onOpenChange={(open) => {
        if (!open) onClose();
      }}
    >
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Alterar fim previsto</DialogTitle>
          <DialogDescription>
            {pack === null
              ? ''
              : `${pack.pack_name} de ${pack.client_name}, comprado a ${formatDate(pack.purchase_date)}. Deixa vazio para remover a data.`}
          </DialogDescription>
        </DialogHeader>
        <FormField label="Fim previsto" error={error}>
          {(control) => (
            <Input
              {...control}
              type="date"
              value={value}
              onChange={(event) => {
                setValue(event.target.value);
                setError(undefined);
              }}
            />
          )}
        </FormField>
        <DialogFooter>
          <Button variant="outline" disabled={mutation.isPending} onClick={onClose}>
            Cancelar
          </Button>
          <Button disabled={mutation.isPending} onClick={() => void save()}>
            {mutation.isPending ? 'A guardar…' : 'Guardar'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
