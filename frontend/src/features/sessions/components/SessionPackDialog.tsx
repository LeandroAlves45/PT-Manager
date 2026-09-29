import { useState } from 'react';
import { toast } from 'sonner';

import { balanceLabel, useUsablePacksQuery } from '@/features/packs';
import { useChangeSessionPackMutation } from '@/features/sessions/api/mutations';
import { SESSION_ERRORS } from '@/features/sessions/lib/sessionStatus';
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
import { NativeSelect } from '@/shared/components/ui/native-select';

type Session = components['schemas']['TrainingSessionResponse'];

const NO_PACK = 'none';

/**
 * Diálogo "Trocar pack" de uma sessão agendada: outro pack com saldo do mesmo cliente, ou
 * "Sem pack". Um pack já sem saldo não aparece na lista (`/usable`); se a sessão aponta para
 * um desses, a opção atual passa a "Sem pack" até o personal trainer escolher.
 *
 * O estado nasce em cada abertura (`key` de quem chama) — cancelar não deixa lixo.
 *
 * @param session Sessão a alterar, ou `null` com o diálogo fechado.
 */
export function SessionPackDialog({
  session,
  onClose,
}: {
  session: Session | null;
  onClose: () => void;
}) {
  const packs = useUsablePacksQuery(session?.client_id ?? null);
  const mutation = useChangeSessionPackMutation();
  const [value, setValue] = useState(session?.client_session_pack_id ?? NO_PACK);
  const [error, setError] = useState<string | undefined>(undefined);
  const known = packs.data?.some((pack) => pack.id === value) ?? false;
  const selected = value === NO_PACK || known ? value : NO_PACK;

  async function save() {
    if (session === null) return;

    try {
      await mutation.mutateAsync({
        sessionId: session.id,
        packId: selected === NO_PACK ? null : selected,
      });
      toast.success('Pack da sessão atualizado.');
      onClose();
    } catch (failure) {
      setError(
        (isApiProblem(failure) ? SESSION_ERRORS[failure.code] : undefined) ??
          'Não foi possível guardar o pack da sessão. Tenta novamente.'
      );
    }
  }

  return (
    <Dialog
      open={session !== null}
      onOpenChange={(open) => {
        if (!open) onClose();
      }}
    >
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Trocar pack</DialogTitle>
          <DialogDescription>
            A sessão desconta do pack escolhido quando for realizada ou marcada como falta.
          </DialogDescription>
        </DialogHeader>
        <FormField label="Pack" error={error}>
          {(control) => (
            <NativeSelect
              {...control}
              disabled={packs.isPending}
              value={selected}
              onChange={(event) => {
                setValue(event.target.value);
                setError(undefined);
              }}
            >
              {packs.data?.map((pack) => (
                <option key={pack.id} value={pack.id}>
                  {pack.pack_name} · {balanceLabel(pack)}
                </option>
              ))}
              <option value={NO_PACK}>Sem pack</option>
            </NativeSelect>
          )}
        </FormField>
        <DialogFooter>
          <Button variant="outline" disabled={mutation.isPending} onClick={onClose}>
            Cancelar
          </Button>
          <Button disabled={mutation.isPending || packs.isPending} onClick={() => void save()}>
            {mutation.isPending ? 'A guardar…' : 'Guardar'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
