import { useForm } from 'react-hook-form';
import { toast } from 'sonner';
import { z } from 'zod';

import { useSavePackTypeMutation } from '@/features/packs/api/mutations';
import { isApiProblem } from '@/shared/api/problem';
import type { components } from '@/shared/api/schema';
import { FormField } from '@/shared/components/FormField';
import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';

type PackType = components['schemas']['PackTypeResponse'];

/** Inteiro positivo escrito numa caixa de texto numérica. */
const positiveInt = (required: string, invalid: string) =>
  z
    .string()
    .trim()
    .min(1, required)
    .refine((value) => /^\d+$/.test(value) && Number(value) > 0, invalid);

/**
 * Regras iguais a `CreatePackTypeCommandValidator`/`UpdatePackTypeCommandValidator`: nome
 * 1–255, sessões > 0, preço ≥ 0, moeda de 3 letras, duração > 0 quando indicada. O preço
 * escreve-se em euros (vírgula ou ponto) e vai em cêntimos para a API.
 */
const packTypeSchema = z.object({
  name: z
    .string()
    .trim()
    .min(1, 'Indica o nome do pack.')
    .max(255, 'O nome não pode exceder 255 caracteres.'),
  session_count: positiveInt(
    'Indica o número de sessões.',
    'O número de sessões tem de ser um inteiro maior que zero.'
  ),
  price: z
    .string()
    .trim()
    .min(1, 'Indica o preço.')
    .refine(
      (value) => /^\d+([.,]\d{1,2})?$/.test(value),
      'Indica um preço válido, com até duas casas decimais.'
    ),
  currency: z
    .string()
    .trim()
    .regex(/^[A-Za-z]{3}$/, 'A moeda tem de ter 3 letras (ex.: EUR).'),
  expected_duration_days: z
    .string()
    .trim()
    .refine(
      (value) => value === '' || (/^\d+$/.test(value) && Number(value) > 0),
      'A duração tem de ser um inteiro maior que zero.'
    ),
});
type PackTypeValues = z.input<typeof packTypeSchema>;

/** Campos do backend para os campos do formulário. */
const SERVER_FIELDS: Record<string, keyof PackTypeValues> = {
  Name: 'name',
  SessionCount: 'session_count',
  PriceCents: 'price',
  Currency: 'currency',
  ExpectedDurationDays: 'expected_duration_days',
};

/** "300" ou "300,05" em euros para cêntimos, sem erros de vírgula flutuante. */
function toCents(price: string): number {
  const [units = '0', decimals = ''] = price.replace(',', '.').split('.');
  return Number(units) * 100 + Number(decimals.padEnd(2, '0'));
}

/** Cêntimos para o texto da caixa: 30000 -> "300,00". */
function fromCents(cents: number): string {
  return (cents / 100).toFixed(2).replace('.', ',');
}

/**
 * Formulário do tipo de pack (criar e editar).
 *
 * @param packType Tipo a editar, ou `null` para criar.
 * @param onSaved Chamado depois de guardar.
 */
export function PackTypeForm({
  packType,
  onSaved,
}: {
  packType: PackType | null;
  onSaved: () => void;
}) {
  const mutation = useSavePackTypeMutation(packType?.id ?? null);
  const form = useForm<PackTypeValues>({
    defaultValues: {
      name: packType?.name ?? '',
      session_count: packType === null ? '' : String(packType.session_count),
      price: packType === null ? '' : fromCents(packType.price_cents),
      currency: packType?.currency ?? 'EUR',
      expected_duration_days:
        packType?.expected_duration_days == null ? '' : String(packType.expected_duration_days),
    },
  });
  const errors = form.formState.errors;

  async function submit(values: PackTypeValues) {
    const parsed = packTypeSchema.safeParse(values);
    if (!parsed.success) {
      for (const issue of parsed.error.issues) {
        const field = issue.path[0];
        if (typeof field === 'string' && field in values)
          form.setError(field as keyof PackTypeValues, { message: issue.message });
      }
      return;
    }

    const data = parsed.data;
    try {
      await mutation.mutateAsync({
        name: data.name,
        session_count: Number(data.session_count),
        price_cents: toCents(data.price),
        currency: data.currency.toUpperCase(),
        expected_duration_days:
          data.expected_duration_days === '' ? null : Number(data.expected_duration_days),
      });
      toast.success(
        packType === null
          ? 'Tipo de pack criado com sucesso.'
          : 'Tipo de pack atualizado com sucesso.'
      );
      onSaved();
    } catch (error) {
      if (isApiProblem(error) && error.hasFieldErrors) {
        for (const fieldError of error.fieldErrors) {
          const field = SERVER_FIELDS[fieldError.field];
          if (field !== undefined) form.setError(field, { message: fieldError.message });
        }
        return;
      }
      form.setError('root', {
        message:
          isApiProblem(error) && error.code === 'pack_type_not_found'
            ? 'Este tipo de pack já não existe.'
            : 'Não foi possível guardar. Tenta novamente.',
      });
    }
  }

  return (
    <form
      noValidate
      onSubmit={form.handleSubmit((values) => void submit(values))}
      className="flex h-full flex-col gap-4 pb-4"
    >
      <FormField label="Nome" error={errors.name?.message}>
        {(control) => <Input {...control} autoComplete="off" {...form.register('name')} />}
      </FormField>
      <div className="grid gap-4 sm:grid-cols-2">
        <FormField label="Número de sessões" error={errors.session_count?.message}>
          {(control) => (
            <Input {...control} inputMode="numeric" {...form.register('session_count')} />
          )}
        </FormField>
        <FormField label="Duração prevista (dias)" error={errors.expected_duration_days?.message}>
          {(control) => (
            <Input
              {...control}
              inputMode="numeric"
              placeholder="Opcional"
              {...form.register('expected_duration_days')}
            />
          )}
        </FormField>
        <FormField label="Preço" error={errors.price?.message}>
          {(control) => (
            <Input
              {...control}
              inputMode="decimal"
              placeholder="300,00"
              {...form.register('price')}
            />
          )}
        </FormField>
        <FormField label="Moeda" error={errors.currency?.message}>
          {(control) => (
            <Input
              {...control}
              maxLength={3}
              placeholder="EUR"
              className="uppercase"
              {...form.register('currency')}
            />
          )}
        </FormField>
      </div>
      {packType !== null && (
        <p className="text-muted-foreground text-sm">
          Os packs já vendidos mantêm o nome, as sessões e o preço da altura da venda.
        </p>
      )}
      {errors.root && (
        <p role="alert" className="text-destructive text-sm">
          {errors.root.message}
        </p>
      )}
      <div className="border-border mt-auto flex justify-end gap-2 border-t pt-4">
        <Button type="submit" disabled={mutation.isPending}>
          {mutation.isPending
            ? 'A guardar…'
            : packType === null
              ? 'Criar tipo de pack'
              : 'Guardar alterações'}
        </Button>
      </div>
    </form>
  );
}
