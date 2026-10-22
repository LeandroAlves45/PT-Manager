import { usePortalWorkoutDaysQuery } from '@/features/portal/api/portal';
import { pastDayOptionLabel } from '@/features/portal/lib/schedule';
import { Label } from '@/shared/components/ui/label';
import { NativeSelect } from '@/shared/components/ui/native-select';

/**
 * Seletor do dia do treino: «Hoje» e os dias passados do plano ativo (`GET /portal/my-workout/days`).
 *
 * Só aparece quando há dias passados para escolher; sem eles, ou enquanto carregam ou se a
 * lista falha, o ecrã fica como está (o treino de hoje não depende deste filtro). É um
 * `<select>` nativo, com 44 px de altura e o seletor do telemóvel.
 *
 * @param selected Dia passado escolhido (`AAAA-MM-DD`) ou `null` para hoje.
 * @param onSelect Chamado com o dia escolhido, ou `null` para voltar a hoje.
 */
export function WorkoutDayPicker({
  selected,
  onSelect,
}: {
  selected: string | null;
  onSelect: (date: string | null) => void;
}) {
  const query = usePortalWorkoutDaysQuery();
  const days = query.data?.items ?? [];
  if (days.length === 0) return null;

  // Uma data que a lista não conhece (escrita à mão na barra de endereço) mostra «Hoje».
  const value = days.some((day) => day.date === selected) ? selected : null;

  return (
    <div className="space-y-1.5">
      <Label htmlFor="workout-day">Ver treino de</Label>
      <NativeSelect
        id="workout-day"
        value={value ?? ''}
        onChange={(event) => onSelect(event.target.value === '' ? null : event.target.value)}
      >
        <option value="">Hoje</option>
        {days.map((day) => (
          <option key={day.date} value={day.date}>
            {pastDayOptionLabel(day.date, day.week_number)}
          </option>
        ))}
      </NativeSelect>
    </div>
  );
}
