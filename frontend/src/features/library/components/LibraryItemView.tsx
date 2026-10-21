import { ExerciseVideoPanel } from '@/features/exercise-video';
import { libraryKeys } from '@/features/library/api/keys';
import type { useLibraryItemQuery } from '@/features/library/api/queries';
import { LibraryItemDetails } from '@/features/library/components/LibraryItemDetails';
import { gramsLabel, macrosLabel } from '@/features/library/lib/labels';
import { formatMuscleGroups } from '@/shared/lib/muscleGroups';

type LibraryItemData = NonNullable<ReturnType<typeof useLibraryItemQuery>['data']>;

/**
 * Leitura de um item que não se edita: global (só da plataforma), arquivado (reativa-se na
 * lista) ou bloqueado pela moderação (com o motivo).
 *
 * @param data Tipo e item, como os devolve `useLibraryItemQuery`.
 */
export function LibraryItemView({ data }: { data: LibraryItemData }) {
  if (data.kind === 'exercises') {
    const exercise = data.item;
    return (
      <LibraryItemDetails
        item={exercise}
        rows={[
          { label: 'Grupos musculares', value: formatMuscleGroups(exercise.muscle_groups) },
          { label: 'Equipamento', value: exercise.equipment },
          { label: 'Dificuldade', value: exercise.difficulty_level },
          { label: 'Descrição', value: exercise.description },
        ]}
      >
        <ExerciseVideoPanel
          exercise={exercise}
          audience="trainer"
          listKey={libraryKeys.kind('exercises')}
          mode="view"
        />
      </LibraryItemDetails>
    );
  }

  if (data.kind === 'foods') {
    const food = data.item;
    return (
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
    );
  }

  const supplement = data.item;
  return (
    <LibraryItemDetails
      item={supplement}
      rows={[
        { label: 'Dose', value: `${supplement.serving_size} ${supplement.unit_of_measure}` },
        { label: 'Quando tomar', value: supplement.timing },
        { label: 'Descrição', value: supplement.description },
        { label: 'Notas', value: supplement.trainer_notes },
      ]}
    />
  );
}
