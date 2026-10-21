import { useFoodListQuery } from '@/features/library/api/queries';
import { LibraryPanel, type LibraryColumn } from '@/features/library/components/LibraryPanel';
import { gramsLabel, macrosLabel } from '@/features/library/lib/labels';
import { useLibraryFilters, type LibraryControls } from '@/features/library/lib/useLibraryFilters';
import type { components } from '@/shared/api/schema';

type Food = components['schemas']['FoodResponse'];

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
    />
  );
}
