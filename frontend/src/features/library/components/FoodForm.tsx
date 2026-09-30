import { useForm, useWatch } from 'react-hook-form';
import { toast } from 'sonner';
import { z } from 'zod';

import { useSaveFoodMutation } from '@/features/library/api/mutations';
import { applyServerErrors, applyZodIssues } from '@/features/library/lib/errors';
import { gramsLabel } from '@/features/library/lib/labels';
import type { components } from '@/shared/api/schema';
import { FormField } from '@/shared/components/FormField';
import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';
import { Textarea } from '@/shared/components/ui/textarea';

type Food = components['schemas']['FoodResponse'];

/** Decimal escrito à portuguesa ("2,5" ou "2.5"), com até duas casas (colunas `numeric(10,2)`). */
const DECIMAL = /^\d+([.,]\d{1,2})?$/;

function toNumber(value: string): number {
  return Number(value.replace(',', '.'));
}

/** Gramas por 100 g de alimento: obrigatório, 0–100. */
const macro = (required: string, invalid: string) =>
  z
    .string()
    .trim()
    .min(1, required)
    .refine((value) => DECIMAL.test(value) && toNumber(value) <= 100, invalid);

/**
 * Regras iguais a `CreateFoodCommandValidator`: nome 1–255; proteína, hidratos e gordura 0–100
 * com soma ≤ 100 (por 100 g); fibra 0–100 opcional; porção habitual > 0 e ≤ 1000 g com duas
 * casas, opcional. As kcal são calculadas pelo servidor e nunca se enviam.
 */
const foodSchema = z
  .object({
    name: z
      .string()
      .trim()
      .min(1, 'Indica o nome do alimento.')
      .max(255, 'O nome não pode exceder 255 caracteres.'),
    description: z.string(),
    protein: macro('Indica a proteína.', 'A proteína tem de estar entre 0 e 100 g.'),
    carbs: macro('Indica os hidratos.', 'Os hidratos têm de estar entre 0 e 100 g.'),
    fats: macro('Indica a gordura.', 'A gordura tem de estar entre 0 e 100 g.'),
    fiber: z
      .string()
      .trim()
      .refine(
        (value) => value === '' || (DECIMAL.test(value) && toNumber(value) <= 100),
        'A fibra tem de estar entre 0 e 100 g.'
      ),
    default_serving_grams: z
      .string()
      .trim()
      .refine(
        (value) =>
          value === '' || (DECIMAL.test(value) && toNumber(value) > 0 && toNumber(value) <= 1000),
        'A porção tem de ser maior que 0 e até 1000 g, com até duas casas decimais.'
      ),
  })
  .refine(
    (values) => {
      const macros = [values.protein, values.carbs, values.fats].map((value) => value.trim());
      // Um macro inválido já tem o seu erro; a soma só se verifica com os três válidos.
      if (macros.some((value) => !DECIMAL.test(value))) return true;
      return macros.map(toNumber).reduce((total, value) => total + value, 0) <= 100;
    },
    {
      path: ['protein'],
      message: 'Proteína, hidratos e gordura não podem somar mais de 100 g.',
    }
  );
type FoodValues = z.input<typeof foodSchema>;

const FIELDS = [
  'name',
  'description',
  'protein',
  'carbs',
  'fats',
  'fiber',
  'default_serving_grams',
] as const;

const SERVER_FIELDS: Record<string, (typeof FIELDS)[number]> = {
  Name: 'name',
  Protein: 'protein',
  Carbs: 'carbs',
  Fats: 'fats',
  Macros: 'protein',
  Fiber: 'fiber',
  DefaultServingGrams: 'default_serving_grams',
};

/** Número vindo da API para o texto da caixa: 2.5 -> "2,5". */
function toText(value: number | null | undefined): string {
  return value == null ? '' : String(value).replace('.', ',');
}

/** Mesma fórmula da coluna gerada 'kcal' (4/4/9), só para pré-visualização. */
function kcalPreview(macros: readonly string[]): number | null {
  const parts = macros.map((value) => value.trim());
  if (parts.some((value) => !DECIMAL.test(value)))
    return null;

  const [protein = 0, carbs = 0, fats = 0] = parts.map(toNumber);
  return protein * 4 + carbs * 4 + fats * 9;
}

/**
 * Formulário do alimento privado (criar e editar). Macros por 100 g, como o combobox da 6E-4
 * os mostra.
 *
 * @param food Alimento a editar, ou `null` para criar.
 */
export function FoodForm({
  food,
  onSaved,
}: {
  food: Food | null;
  onSaved: () => void;
}) {
  const mutation = useSaveFoodMutation(food?.id ?? null);
  const form = useForm<FoodValues>({
    defaultValues: {
      name: food?.name ?? '',
      description: food?.description ?? '',
      protein: toText(food?.protein),
      carbs: toText(food?.carbs),
      fats: toText(food?.fats),
      fiber: toText(food?.fiber),
      default_serving_grams: toText(food?.default_serving_grams),
    },
  });
  const errors = form.formState.errors;
  const kcal = kcalPreview(useWatch({ control: form.control, name: ['protein', 'carbs', 'fats'] }));

  async function submit(values: FoodValues) {
    const parsed = foodSchema.safeParse(values);
    if (!parsed.success) {
      applyZodIssues(parsed.error.issues, FIELDS, form.setError);
      return;
    }

    const data = parsed.data;
    try {
      await mutation.mutateAsync({
        name: data.name,
        description: data.description.trim() || null,
        protein: toNumber(data.protein),
        carbs: toNumber(data.carbs),
        fats: toNumber(data.fats),
        fiber: data.fiber === '' ? null : toNumber(data.fiber),
        default_serving_grams:
          data.default_serving_grams === '' ? null : toNumber(data.default_serving_grams),
      });
      toast.success(
        food === null ? 'Alimento criado com sucesso.' : 'Alimento atualizado com sucesso.',
      );
      onSaved();
    } catch (error) {
      applyServerErrors(error, SERVER_FIELDS, form.setError);
    }
  }

  return (
    <form
      noValidate
      onSubmit={form.handleSubmit((values) => void submit(values))}
      className="flex flex-col gap-4"
    >
      <FormField label="Nome" error={errors.name?.message}>
        {(control) => <Input {...control} autoComplete="off" {...form.register('name')} />}
      </FormField>
      <fieldset className="space-y-3">
        <legend className="text-sm font-medium">Por 100 g</legend>
        <div className="grid gap-4 sm:grid-cols-2">
          <FormField label="Proteína (g)" error={errors.protein?.message}>
            {(control) => <Input {...control} inputMode="decimal" {...form.register('protein')} />}
          </FormField>
          <FormField label="Hidratos (g)" error={errors.carbs?.message}>
            {(control) => <Input {...control} inputMode="decimal" {...form.register('carbs')} />}
          </FormField>
          <FormField label="Gordura (g)" error={errors.fats?.message}>
            {(control) => <Input {...control} inputMode="decimal" {...form.register('fats')} />}
          </FormField>
          <FormField label="Fibra (g)" error={errors.fiber?.message}>
            {(control) => (
              <Input
                {...control}
                inputMode="decimal"
                placeholder="Opcional"
                {...form.register('fiber')}
              />
            )}
          </FormField>
        </div>
        <p aria-live="polite" className="text-muted-foreground text-sm">
          {kcal === null
            ? 'As kcal são calculadas a partir dos macros.'
            : `≈ ${gramsLabel(kcal)} kcal por 100 g`}
        </p>
      </fieldset>
      <FormField label="Porção habitual (g)" error={errors.default_serving_grams?.message}>
        {(control) => (
          <Input
            {...control}
            inputMode="decimal"
            placeholder="Opcional (pré-preenche a quantidade nos planos)"
            {...form.register('default_serving_grams')}
          />
        )}
      </FormField>
      <FormField label="Descrição" error={errors.description?.message}>
        {(control) => <Textarea {...control} rows={3} {...form.register('description')} />}
      </FormField>
      {errors.root && (
        <p role="alert" className="text-destructive text-sm">
          {errors.root.message}
        </p>
      )}
      <div className="flex justify-end">
        <Button type="submit" disabled={mutation.isPending}>
          {mutation.isPending
            ? 'A guardar…'
            : food === null
              ? 'Criar alimento'
              : 'Guardar alterações'}
        </Button>
      </div>
    </form>
  );
}

