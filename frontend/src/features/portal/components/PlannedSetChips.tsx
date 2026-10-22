import { plannedSetChips, type PlannedSet } from '@/features/portal/lib/workout';
import { cn } from '@/shared/lib/utils';

/**
 * Prescrição de uma série em chips: repetições («12-10 reps», o maior primeiro), carga, RPE e
 * descanso. Um campo que a série não tem não aparece.
 *
 * As repetições levam o destaque da marca no contorno; o texto fica sempre na cor do
 * primeiro plano para manter o contraste nos dois temas.
 */
export function PlannedSetChips({ set, className }: { set: PlannedSet; className?: string }) {
  const chips = plannedSetChips(set);
  if (chips.length === 0) return null;

  return (
    <ul className={cn('flex min-w-0 flex-wrap gap-1.5', className)}>
      {chips.map((chip, index) => (
        <li
          key={chip}
          className={cn(
            'text-foreground rounded-full border px-2.5 py-0.5 text-xs whitespace-nowrap tabular-nums',
            index === 0 && set.planned_reps !== null
              ? 'border-primary/50 bg-primary/10 font-medium'
              : 'border-border bg-muted'
          )}
        >
          {chip}
        </li>
      ))}
    </ul>
  );
}
