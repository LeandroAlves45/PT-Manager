import { useFoodListQuery } from '@/features/library/api/queries';
import { FoodForm } from '@/features/library/components/FoodForm';
import { LibraryItemDetails } from '@/features/library/components/LibraryItemDetails';
import { LibraryPanel, type LibraryColumn } from '@/features/library/components/LibraryPanel';
import { gramsLabel } from '@/features/library/lib/labels';
import { useLibraryFilters, type LibraryControls } from '@/features/library/lib/useLibraryFilters';
import type { components } from '@/shared/api/schema';

type Food = components['schemas']['FoodResponse'];

/** "31 g P · 0 g HC · 3,6 g G" — a linha secundária do combobox. */
function macrosLabel(food: Food): string {
  return `${gramsLabel(food.protein)} g P · ${gramsLabel(food.carbs)} g HC · ${gramsLabel(food.fats)} g G`;
}

const COLUMNS: readonly LibraryColumn<Food>[] = [
  { header: 'kcal / 100 g', cell: (food) => gramsLabel(food.kcal) },
  { header: 'Macros / 100 g', cell: macrosLabel },
  {
    header: 'Porção',
    cell: (food) =>
      food.default_serving_grams === null ? '—' : `${gramsLabel(food.default_serving_grams)} g`,
  },
];

/** Separador "Alimentos": globais e privados, macros por 100 g (kcal calculadas no servidor). */
export function FoodsPanel({ controls }: { controls: LibraryControls }) {
  const { filters, filtered } = useLibraryFilters(controls);
  const query = useFoodListQuery(filters);

  return (
    <LibraryPanel
      kind="foods"
      query={query}
      controls={controls}
      filtered={filtered}
      columns={COLUMNS}
      renderForm={(food, onSaved) => <FoodForm food={food} onSaved={onSaved} />}
      renderDetails={(food) => (
        <LibraryItemDetails
          item={food}
          rows={[
            { label: 'kcal / 100 g', value: gramsLabel(food.kcal) },
            { label: 'Macros / 100 g', value: macrosLabel(food) },
            {
              label: 'Fibra / 100 g',
              value: food.fiber === null ? null : `${gramsLabel(food.fiber)} g`,
            },
            {
              label: 'Porção habitual',
              value:
                food.default_serving_grams === null
                  ? null
                  : `${gramsLabel(food.default_serving_grams)} g`,
            },
            { label: 'Descrição', value: food.description },
          ]}
        />
      )}
    />
  );
}
