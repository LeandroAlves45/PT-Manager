import { useState } from 'react';

import {
  CatalogPicker,
  type CatalogChoice,
} from '@/features/prescriptions/components/CatalogPicker';
import { FormField } from '@/shared/components/FormField';
import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';
import type { components } from '@/shared/api/schema';

type Structure = components['schemas']['MealPlanStructureRequest'];
type Meal = components['schemas']['MealRequest'];
type Item = components['schemas']['MealItemRequest'];
type Supplement = components['schemas']['MealSupplementRequest'];

/**
 * Editor controlado das refeições, alimentos e suplementos por refeição. Devolve sempre a
 * árvore inteira, que o `PUT` reconcilia por ID (nós novos com `id: null`).
 *
 * @param value Estrutura atual do rascunho.
 * @param onChange Recebe uma cópia nova da estrutura a cada alteração.
 * @param disabled Plano arquivado: só consulta, sem botões de adicionar ou remover.
 * @param initialNames Nomes de alimentos e suplementos do detalhe, por ID do catálogo.
 */
export function MealStructureEditor({
  value,
  onChange,
  disabled,
  initialNames,
}: {
  value: Structure;
  onChange: (value: Structure) => void;
  disabled: boolean;
  initialNames: Readonly<Record<string, string>>;
}) {
  const [names, setNames] = useState<Record<string, string>>({});

  function update(change: (draft: Structure) => void) {
    const next = structuredClone(value);
    change(next);
    onChange(next);
  }

  function label(id: string) {
    return names[id] ?? initialNames[id] ?? 'Item selecionado';
  }

  return (
    <section className="space-y-4">
      <div className="flex items-center justify-between">
        <h3 className="font-display text-xl">Refeições</h3>
        {!disabled && (
          <Button
            type="button"
            variant="outline"
            onClick={() =>
              update((draft) =>
                draft.meals.push({
                  id: null,
                  meal_type: '',
                  order_number: draft.meals.length + 1,
                  items: [],
                  supplements: [],
                })
              )
            }
          >
            Adicionar refeição
          </Button>
        )}
      </div>
      {value.meals.map((meal: Meal, mealIndex: number) => (
        <div
          key={meal.id ?? 'new-meal-' + mealIndex}
          className="border-border space-y-4 rounded-xl border p-4"
        >
          <div className="flex flex-wrap gap-3">
            <FormField label="Tipo de refeição">
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
            <FormField label="Ordem">
              {(control) => (
                <Input
                  {...control}
                  type="number"
                  min={1}
                  value={meal.order_number}
                  disabled={disabled}
                  onChange={(event) =>
                    update((draft) => {
                      draft.meals[mealIndex]!.order_number = Number(event.target.value);
                    })
                  }
                />
              )}
            </FormField>
            {!disabled && (
              <Button
                type="button"
                variant="outline"
                onClick={() =>
                  update((draft) => {
                    draft.meals.splice(mealIndex, 1);
                  })
                }
              >
                Remover refeição
              </Button>
            )}
          </div>
          <h4 className="font-medium">Alimentos</h4>
          {meal.items.map((item: Item, itemIndex: number) => (
            <div key={item.id ?? 'new-item-' + itemIndex} className="grid gap-2 sm:grid-cols-4">
              <FormField label="Alimento">
                {(control) => (
                  <CatalogPicker
                    id={control.id}
                    kind="foods"
                    value={
                      item.food_id === ''
                        ? null
                        : {
                            value: item.food_id,
                            label: label(item.food_id),
                          }
                    }
                    disabled={disabled}
                    onChange={(choice: CatalogChoice) => {
                      setNames((current) => ({ ...current, [choice.value]: choice.label }));
                      update((draft) => {
                        const current = draft.meals[mealIndex]!.items[itemIndex]!;
                        current.food_id = choice.value;
                        if (
                          item.food_id === '' &&
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
              <FormField label="Quantidade (g)">
                {(control) => (
                  <Input
                    {...control}
                    type="number"
                    min={0.01}
                    step={0.01}
                    value={item.quantity_in_grams || ''}
                    disabled={disabled}
                    onChange={(event) =>
                      update((draft) => {
                        draft.meals[mealIndex]!.items[itemIndex]!.quantity_in_grams = Number(
                          event.target.value
                        );
                      })
                    }
                  />
                )}
              </FormField>
              <FormField label="Ordem">
                {(control) => (
                  <Input
                    {...control}
                    type="number"
                    min={1}
                    value={item.order_number}
                    disabled={disabled}
                    onChange={(event) =>
                      update((draft) => {
                        draft.meals[mealIndex]!.items[itemIndex]!.order_number = Number(
                          event.target.value
                        );
                      })
                    }
                  />
                )}
              </FormField>
              {!disabled && (
                <Button
                  type="button"
                  variant="outline"
                  onClick={() =>
                    update((draft) => {
                      draft.meals[mealIndex]!.items.splice(itemIndex, 1);
                    })
                  }
                >
                  Remover alimento
                </Button>
              )}
            </div>
          ))}
          {!disabled && meal.items.length < 50 && (
            <Button
              type="button"
              variant="outline"
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
              Adicionar alimento
            </Button>
          )}
          <h4 className="font-medium">Suplementos nesta refeição</h4>
          {meal.supplements.map((item: Supplement, itemIndex: number) => (
            <div
              key={item.id ?? 'new-supplement-' + itemIndex}
              className="grid gap-2 sm:grid-cols-4"
            >
              <FormField label="Suplemento">
                {(control) => (
                  <CatalogPicker
                    id={control.id}
                    kind="supplements"
                    value={
                      item.supplement_id === ''
                        ? null
                        : {
                            value: item.supplement_id,
                            label: label(item.supplement_id),
                          }
                    }
                    disabled={disabled}
                    onChange={(choice) => {
                      setNames((current) => ({ ...current, [choice.value]: choice.label }));
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
                  <Input
                    {...control}
                    type="number"
                    min={0.01}
                    step={0.01}
                    value={item.quantity || ''}
                    disabled={disabled}
                    onChange={(event) =>
                      update((draft) => {
                        draft.meals[mealIndex]!.supplements[itemIndex]!.quantity = Number(
                          event.target.value
                        );
                      })
                    }
                  />
                )}
              </FormField>
              <FormField label="Ordem">
                {(control) => (
                  <Input
                    {...control}
                    type="number"
                    min={1}
                    value={item.order_number}
                    disabled={disabled}
                    onChange={(event) =>
                      update((draft) => {
                        draft.meals[mealIndex]!.supplements[itemIndex]!.order_number = Number(
                          event.target.value
                        );
                      })
                    }
                  />
                )}
              </FormField>
              <FormField label="Notas">
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
                  variant="outline"
                  onClick={() =>
                    update((draft) => {
                      draft.meals[mealIndex]!.supplements.splice(itemIndex, 1);
                    })
                  }
                >
                  Remover suplemento
                </Button>
              )}
            </div>
          ))}
          {!disabled && meal.supplements.length < 20 && (
            <Button
              type="button"
              variant="outline"
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
              Adicionar suplemento
            </Button>
          )}
        </div>
      ))}
    </section>
  );
}
