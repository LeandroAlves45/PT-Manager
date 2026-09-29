import { addDays, format, parseISO } from 'date-fns';
import { Controller, useForm } from 'react-hook-form';
import { toast } from 'sonner';
import { z } from 'zod';

import { ClientCombobox, type ClientChoice } from '@/features/clients';
import { useAssignPackMutation } from '@/features/packs/api/mutations';
import { useActivePackTypesQuery } from '@/features/packs/api/queries';
import { ASSIGN_PACK_ERRORS, priceLabel } from '@/features/packs/lib/labels';
import { isApiProblem } from '@/shared/api/problem';
import { FormField } from '@/shared/components/FormField';
import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';
import { NativeSelect } from '@/shared/components/ui/native-select';

/** Regras de `AssignClientSessionPackCommandValidator`, com mensagens em PT-PT. */
const assignSchema = z
  .object({
    client: z.custom<ClientChoice | null>().refine((value) => value !== null, 'Escolhe o cliente.'),
    pack_type_id: z.string().min(1, 'Escolhe o tipo de pack.'),
    purchase_date: z.string().min(1, 'Indica a data de compra.'),
    expected_end_date: z.string(),
  })
  .refine(
    (values) => values.expected_end_date === '' || values.expected_end_date >= values.purchase_date,
    {
      path: ['expected_end_date'],
      message: 'O fim previsto não pode ser antes da data de compra.',
    }
  );
type AssignValues = z.input<typeof assignSchema>;

/** Campos do backend para os campos do formulário. */
const SERVER_FIELDS: Record<string, keyof AssignValues> = {
  ClientId: 'client',
  PackTypeId: 'pack_type_id',
  PurchaseDate: 'purchase_date',
  ExpectedEndDate: 'expected_end_date',
};

/** Data de hoje no formato do `<input type="date">`. */
function today(): string {
  return format(new Date(), 'yyyy-MM-dd');
}

/**
 * Formulário "Vender pack": cliente, tipo de pack ativo, data de compra e fim previsto.
 *
 * Ao escolher um tipo com duração prevista, o fim previsto é sugerido (compra + dias) —
 * o trainer pode mudá-lo ou apagá-lo. A lista de tipos só é pedida com o formulário aberto.
 *
 * @param client Cliente fixo (tab Sessões do detalhe) ou `null` para escolher na pesquisa.
 * @param onSaved Chamado depois de vender.
 */
export function AssignPackForm({
  client,
  onSaved,
}: {
  client: ClientChoice | null;
  onSaved: () => void;
}) {
  const types = useActivePackTypesQuery(true);
  const mutation = useAssignPackMutation();
  const form = useForm<AssignValues>({
    defaultValues: {
      client,
      pack_type_id: '',
      purchase_date: today(),
      expected_end_date: '',
    },
  });
  const errors = form.formState.errors;

  /** Sugere o fim previsto a partir da duração do tipo e da data de compra. */
  function suggestEndDate(packTypeId: string, purchaseDate: string) {
    const days = types.data?.find((type) => type.id === packTypeId)?.expected_duration_days;
    if (days == null || purchaseDate === '') return;
    form.setValue('expected_end_date', format(addDays(parseISO(purchaseDate), days), 'yyyy-MM-dd'));
  }

  async function submit(values: AssignValues) {
    const parsed = assignSchema.safeParse(values);
    if (!parsed.success) {
      for (const issue of parsed.error.issues) {
        const field = issue.path[0];
        if (typeof field === 'string' && field in values)
          form.setError(field as keyof AssignValues, { message: issue.message });
      }
      return;
    }

    const data = parsed.data;
    const chosen = data.client;
    if (chosen === null) return;

    try {
      await mutation.mutateAsync({
        client_id: chosen.id,
        pack_type_id: data.pack_type_id,
        purchase_date: data.purchase_date,
        expected_end_date: data.expected_end_date === '' ? null : data.expected_end_date,
      });
      toast.success(`Pack vendido a ${chosen.name}.`);
      onSaved();
    } catch (error) {
      if (!isApiProblem(error)) {
        form.setError('root', { message: 'Não foi possível vender o pack. Tenta novamente.' });
        return;
      }
      const message = ASSIGN_PACK_ERRORS[error.code];
      if (message !== undefined) {
        form.setError('root', { message });
      } else if (error.hasFieldErrors) {
        for (const fieldError of error.fieldErrors) {
          const field = SERVER_FIELDS[fieldError.field];
          if (field !== undefined) form.setError(field, { message: fieldError.message });
        }
      } else {
        form.setError('root', { message: 'Não foi possível vender o pack. Tenta novamente.' });
      }
    }
  }

  return (
    <form
      noValidate
      onSubmit={form.handleSubmit((values) => void submit(values))}
      className="flex h-full flex-col gap-4 pb-4"
    >
      {client === null && (
        <FormField label="Cliente" error={errors.client?.message}>
          {(control) => (
            <Controller
              control={form.control}
              name="client"
              render={({ field }) => (
                <ClientCombobox {...control} value={field.value} onChange={field.onChange} />
              )}
            />
          )}
        </FormField>
      )}
      <FormField label="Tipo de pack" error={errors.pack_type_id?.message}>
        {(control) => (
          <NativeSelect
            {...control}
            disabled={types.isPending}
            {...form.register('pack_type_id', {
              onChange: (event: { target: { value: string } }) =>
                suggestEndDate(event.target.value, form.getValues('purchase_date')),
            })}
          >
            <option value="" disabled>
              {types.isPending ? 'A carregar…' : 'Escolhe…'}
            </option>
            {types.data?.map((type) => (
              <option key={type.id} value={type.id}>
                {type.name} · {type.session_count} sessões · {priceLabel(type)}
              </option>
            ))}
          </NativeSelect>
        )}
      </FormField>
      {types.isSuccess && types.data.length === 0 && (
        <p className="text-muted-foreground text-sm">
          Ainda não tens tipos de pack ativos. Cria um no separador "Tipos de pack".
        </p>
      )}
      <div className="grid gap-4 sm:grid-cols-2">
        <FormField label="Data de compra" error={errors.purchase_date?.message}>
          {(control) => (
            <Input
              {...control}
              type="date"
              {...form.register('purchase_date', {
                onChange: (event: { target: { value: string } }) =>
                  suggestEndDate(form.getValues('pack_type_id'), event.target.value),
              })}
            />
          )}
        </FormField>
        <FormField label="Fim previsto" error={errors.expected_end_date?.message}>
          {(control) => <Input {...control} type="date" {...form.register('expected_end_date')} />}
        </FormField>
      </div>
      {errors.root && (
        <p role="alert" className="text-destructive text-sm">
          {errors.root.message}
        </p>
      )}
      <div className="border-border mt-auto flex justify-end gap-2 border-t pt-4">
        <Button type="submit" disabled={mutation.isPending}>
          {mutation.isPending ? 'A guardar…' : 'Vender pack'}
        </Button>
      </div>
    </form>
  );
}
