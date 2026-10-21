import { useSupplementListQuery } from '@/features/library/api/queries';
import { LibraryPanel, type LibraryColumn } from '@/features/library/components/LibraryPanel';
import { useLibraryFilters, type LibraryControls } from '@/features/library/lib/useLibraryFilters';
import type { components } from '@/shared/api/schema';

type Supplement = components['schemas']['SupplementResponse'];

const COLUMNS: readonly LibraryColumn<Supplement>[] = [
  {
    header: 'Dose',
    cell: (supplement) => `${supplement.serving_size} ${supplement.unit_of_measure}`,
  },
  { header: 'Quando tomar', cell: (supplement) => supplement.timing ?? '—' },
];

/** Separador "Suplementos": globais e privados (sem moderação de plataforma). */
export function SupplementsPanel({ controls }: { controls: LibraryControls }) {
  const { filters, filtered } = useLibraryFilters(controls);
  const query = useSupplementListQuery(filters);

  return (
    <LibraryPanel
      kind="supplements"
      query={query}
      controls={controls}
      filtered={filtered}
      columns={COLUMNS}
    />
  );
}
