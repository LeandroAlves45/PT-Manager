import { useSupplementListQuery } from '@/features/library/api/queries';
import { LibraryItemDetails } from '@/features/library/components/LibraryItemDetails';
import { LibraryPanel, type LibraryColumn } from '@/features/library/components/LibraryPanel';
import { SupplementForm } from '@/features/library/components/SupplementForm';
import { useLibraryFilters, type LibraryControls } from '@/features/library/lib/useLibraryFilters';
import type { components } from '@/shared/api/schema';

type Supplement = components['schemas']['SupplementResponse'];

const COLUMNS: readonly LibraryColumn<Supplement>[] = [
  {
    header: 'Dose',
    cell: (supplement) => `${supplement.serving_size} ${supplement.unit_of_measure}`,
  },
  { header: 'Quando tomar', cell: (supplement) => supplement.timing },
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
      renderForm={(supplement, onSaved) => (
        <SupplementForm supplement={supplement} onSaved={onSaved} />
      )}
      renderDetails={(supplement) => (
        <LibraryItemDetails
          item={supplement}
          rows={[
            { label: 'Dose', value: `${supplement.serving_size} ${supplement.unit_of_measure}` },
            { label: 'Quando tomar', value: supplement.timing },
            { label: 'Descrição', value: supplement.description },
            { label: 'Notas', value: supplement.trainer_notes },
          ]}
        />
      )}
    />
  );
}
