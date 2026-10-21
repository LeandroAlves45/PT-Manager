import { Plus, Trash2 } from 'lucide-react';
import { MealTotalsLine } from '@/features/prescriptions/components/NutritionBalance';
import {
  CatalogPicker,
  type CatalogChoice,
} from '@/features/prescriptions/components/CatalogPicker';
import type { MealStructure } from '@/features/prescriptions/lib/meal-plan-draft';
import {
  foodContribution,
  sumFoods,
  unitLabel,
  type FoodNutrients,
} from '@/features/prescriptions/lib/nutrition';
import type { components } from '@/shared/api/schema';
import { FormField } from '@/shared/components/FormField';
import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';

type Meal = components['schemas']['MealRequest'];
type Item = components['schemas']['MealItemRequest'];
type Supplement = components['schemas']['MealSupplementRequest'];

const MAX_ITEMS = 50;
const MAX_SUPPLEMENTS = 20;

/** A ordem de refeições, alimentos e suplementos é a posição na lista: renumera sempre. */
function renumber(draft: MealStructure) {
  draft.meals.forEach((meal, mealIndex) => {
    meal.order_number = mealIndex + 1;
    meal.items.forEach((item, index) => (item.order_number = index + 1));
    meal.supplements.forEach((item, index) => (item.order_number = index + 1));
  });
}

/** Campo numérico com o sufixo (unidade) dentro do campo, à direita. */
function QuantityInput({
  id,
  value,
  suffix,
  disabled,
  onChange,
  ...aria
}: {
  id: string;
  value: number;
  suffix: string | null;
  disabled: boolean;
  onChange: (value: number) => void;
  'aria-invalid': boolean;
  'aria-describedby': string | undefined;
}) {
  return (
    <div className="relative">
      <Input
        {...aria}
        id={id}
        type="number"
        inputMode="decimal"
        min={0.01}
        step={0.01}
        value={value || ''}
        disabled={disabled}
        className={suffix === null ? undefined : 'pr-10'}
        onChange={(event) => onChange(Number(event.target.value))}
      />
      {suffix !== null && (
        <span
          data-testid="quantity-suffix"
          className="text-muted-foreground pointer-events-none absolute inset-y-0 right-3 flex items-center text-sm"
        >
          {suffix}
        </span>
      )}
    </div>
  );
}

/**
 * Editor controlado das refeições, alimentos e suplementos por refeição. Devolve sempre a
 * árvore inteira, que o `PUT` reconcilia por ID (nós novos com `id: null`). A ordem é a
 * posição na lista (sem campos de ordem). Mostra os totais de cada refeição e de cada
 * alimento, calculados com os valores por 100 g × gramas / 100.
 *
 * @param value Estrutura atual do rascunho.
 * @param onChange Recebe uma cópia nova da estrutura a cada alteração.
 * @param disabled Plano arquivado: só consulta, sem botões de adicionar ou remover.
 * @param names Nomes de alimentos e suplementos, por ID do catálogo (vivem no rascunho, para sobreviverem à troca de passo).
 * @param onNamed Regista o nome de um item acabado de escolher no catálogo.
 * @param foods Valores por 100 g dos alimentos, por ID do catálogo.
 * @param units Unidade de medida dos suplementos, por ID do catálogo.
 */
export function MealStructureEditor({
  value,
  onChange,
  disabled,
  names,
  onNamed,
  foods,
  units,
}: {
  value: MealStructure;
  onChange: (value: MealStructure) => void;
  disabled: boolean;
  names: Readonly<Record<string, string>>;
  onNamed: (id: string, name: string) => void;
  foods: Readonly<Record<string, FoodNutrients>>;
  units: Readonly<Record<string, string>>;
}) {
  function update(change: (draft: MealStructure) => void) {
    const next = structuredClone(value);
    change(next);
    renumber(next);
    onChange(next);
  }

  function label(id: string) {
    return names[id] ?? 'Item selecionado';
  }

  return (
    <section className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h2 className="font-display text-xl">Refeições</h2>
        {!disabled && (
          <Button
            type="button"
            variant="outline"
            className="min-h-11 md:min-h-8"
            onClick={() =>
              update((draft) =>
                draft.meals.push({
                  id: null,
                  meal_type: `Refeição ${draft.meals.length + 1}`,
                  order_number: draft.meals.length + 1,
                  items: [],
                  supplements: [],
                })
              )
            }
          >
            <Plus aria-hidden /> Adicionar refeição
          </Button>
        )}
      </div>
      {value.meals.length === 0 && (
        <p className="text-muted-foreground border-border rounded-xl border border-dashed p-6 text-center text-sm">
          Ainda não há refeições. Adiciona a primeira para começar a distribuir os alimentos.
        </p>
      )}
      {value.meals.map((meal: Meal, mealIndex: number) => (
        <article
          key={meal.id ?? 'new-meal-' + mealIndex}
          aria-label={`Refeição ${mealIndex + 1}`}
          className="border-border bg-card space-y-4 rounded-xl border p-4"
        >
          <div className="flex flex-wrap items-end gap-3">
            <FormField label="Tipo de refeição" className="min-w-0 flex-1 basis-48">
              {(control) => (
                <Input
                  {...control}
                  value={meal.meal_type}
                  maxLength={50}
                  disabled={disabled}
                  onChange={(event) =>
                    update((draft) => {
                      draft.meals[mealIndex]!.meal_type = event.target.value;
                    })
                  }
                />
              )}
            </FormField>
            {!disabled && (
              <Button
                type="button"
                variant="outline"
                className="min-h-11 md:min-h-8"
                onClick={() =>
                  update((draft) => {
                    draft.meals.splice(mealIndex, 1);
                  })
                }
              >
                <Trash2 aria-hidden /> Remover refeição
              </Button>
            )}
          </div>
          <div className="space-y-1">
            <p className="text-muted-foreground text-xs">Total da refeição</p>
            <MealTotalsLine totals={sumFoods(meal.items, foods)} />
          </div>

          <div className="space-y-3">
            <h3 className="text-sm font-medium">Alimentos</h3>
            {meal.items.map((item: Item, itemIndex: number) => {
              const nutrients = foods[item.food_id];
              const contribution =
                nutrients !== undefined && item.quantity_in_grams > 0
                  ? foodContribution(nutrients, item.quantity_in_grams)
                  : null;
              return (
                <div
                  key={item.id ?? 'new-item-' + itemIndex}
                  role="group"
                  aria-label={`Alimento ${itemIndex + 1} da refeição ${mealIndex + 1}`}
                  className="space-y-1"
                >
                  <div className="grid grid-cols-[1fr_auto] items-end gap-2 sm:grid-cols-[1fr_9rem_auto]">
                    <FormField label="Alimento" className="col-span-2 min-w-0 sm:col-span-1">
                      {(control) => (
                        <CatalogPicker
                          id={control.id}
                          kind="foods"
                          value={
                            item.food_id === ''
                              ? null
                              : { value: item.food_id, label: label(item.food_id) }
                          }
                          disabled={disabled}
                          onChange={(choice: CatalogChoice) => {
                            onNamed(choice.value, choice.label);
                            update((draft) => {
                              const current = draft.meals[mealIndex]!.items[itemIndex]!;
                              const firstChoice = current.food_id === '';
                              current.food_id = choice.value;
                              if (
                                firstChoice &&
                                choice.defaultServingGrams !== null &&
                                choice.defaultServingGrams !== undefined
                              ) {
                                current.quantity_in_grams = choice.defaultServingGrams;
                              }
                            });
                          }}
                        />
                      )}
                    </FormField>
                    <FormField label="Quantidade">
                      {(control) => (
                        <QuantityInput
                          {...control}
                          value={item.quantity_in_grams}
                          suffix="g"
                          disabled={disabled}
                          onChange={(grams) =>
                            update((draft) => {
                              draft.meals[mealIndex]!.items[itemIndex]!.quantity_in_grams = grams;
                            })
                          }
                        />
                      )}
                    </FormField>
                    {!disabled && (
                      <Button
                        type="button"
                        variant="ghost"
                        size="icon"
                        className="min-h-11 min-w-11 md:min-h-9 md:min-w-9"
                        aria-label={`Remover alimento ${itemIndex + 1} da refeição ${mealIndex + 1}`}
                        onClick={() =>
                          update((draft) => {
                            draft.meals[mealIndex]!.items.splice(itemIndex, 1);
                          })
                        }
                      >
                        <Trash2 aria-hidden />
                      </Button>
                    )}
                  </div>
                  {contribution !== null && (
                    <p className="text-muted-foreground text-xs tabular-nums">
                      Este alimento: {formatMacro(contribution.kcal)} kcal · P{' '}
                      {formatMacro(contribution.protein)} g · H {formatMacro(contribution.carbs)} g
                      · G {formatMacro(contribution.fats)} g
                    </p>
                  )}
                </div>
              );
            })}
            {!disabled && meal.items.length < MAX_ITEMS && (
              <Button
                type="button"
                variant="outline"
                size="sm"
                className="min-h-11 md:min-h-8"
                onClick={() =>
                  update((draft) =>
                    draft.meals[mealIndex]!.items.push({
                      id: null,
                      food_id: '',
                      quantity_in_grams: 0,
                      order_number: draft.meals[mealIndex]!.items.length + 1,
                    })
                  )
                }
              >
                <Plus aria-hidden /> Adicionar alimento
              </Button>
            )}
          </div>

          <div className="space-y-3">
            <h3 className="text-sm font-medium">Suplementos nesta refeição</h3>
            {meal.supplements.map((item: Supplement, itemIndex: number) => (
              <div
                key={item.id ?? 'new-supplement-' + itemIndex}
                role="group"
                aria-label={`Suplemento ${itemIndex + 1} da refeição ${mealIndex + 1}`}
                className="grid grid-cols-[1fr_auto] items-end gap-2 sm:grid-cols-[1fr_9rem_1fr_auto]"
              >
                <FormField label="Suplemento" className="col-span-2 min-w-0 sm:col-span-1">
                  {(control) => (
                    <CatalogPicker
                      id={control.id}
                      kind="supplements"
                      value={
                        item.supplement_id === ''
                          ? null
                          : { value: item.supplement_id, label: label(item.supplement_id) }
                      }
                      disabled={disabled}
                      onChange={(choice) => {
                        onNamed(choice.value, choice.label);
                        update((draft) => {
                          draft.meals[mealIndex]!.supplements[itemIndex]!.supplement_id =
                            choice.value;
                        });
                      }}
                    />
                  )}
                </FormField>
                <FormField label="Quantidade">
                  {(control) => (
                    <QuantityInput
                      {...control}
                      value={item.quantity}
                      suffix={unitLabel(units[item.supplement_id])}
                      disabled={disabled}
                      onChange={(quantity) =>
                        update((draft) => {
                          draft.meals[mealIndex]!.supplements[itemIndex]!.quantity = quantity;
                        })
                      }
                    />
                  )}
                </FormField>
                <FormField label="Notas" className="col-span-2 min-w-0 sm:col-span-1">
                  {(control) => (
                    <Input
                      {...control}
                      value={item.notes ?? ''}
                      disabled={disabled}
                      onChange={(event) =>
                        update((draft) => {
                          draft.meals[mealIndex]!.supplements[itemIndex]!.notes =
                            event.target.value || null;
                        })
                      }
                    />
                  )}
                </FormField>
                {!disabled && (
                  <Button
                    type="button"
                    variant="ghost"
                    size="icon"
                    className="min-h-11 min-w-11 md:min-h-9 md:min-w-9"
                    aria-label={`Remover suplemento ${itemIndex + 1} da refeição ${mealIndex + 1}`}
                    onClick={() =>
                      update((draft) => {
                        draft.meals[mealIndex]!.supplements.splice(itemIndex, 1);
                      })
                    }
                  >
                    <Trash2 aria-hidden />
                  </Button>
                )}
              </div>
            ))}
            {!disabled && meal.supplements.length < MAX_SUPPLEMENTS && (
              <Button
                type="button"
                variant="outline"
                size="sm"
                className="min-h-11 md:min-h-8"
                onClick={() =>
                  update((draft) =>
                    draft.meals[mealIndex]!.supplements.push({
                      id: null,
                      supplement_id: '',
                      quantity: 0,
                      notes: null,
                      order_number: draft.meals[mealIndex]!.supplements.length + 1,
                    })
                  )
                }
              >
                <Plus aria-hidden /> Adicionar suplemento
              </Button>
            )}
          </div>
        </article>
      ))}
    </section>
  );
}

/** Macro de um alimento com até 1 casa decimal, em PT-PT. */
function formatMacro(value: number): string {
  return new Intl.NumberFormat('pt-PT', { maximumFractionDigits: 1 }).format(value);
}
