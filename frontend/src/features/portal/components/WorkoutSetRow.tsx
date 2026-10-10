import { Check, Loader2 } from 'lucide-react';
import { useId, useState } from 'react';

import { useSaveSetMutation, useUnlogSetMutation } from '@/features/portal/api/portal';
import {
  formatDecimal,
  MAX_REPS,
  MAX_WEIGHT_KG,
  parseReps,
  parseWeight,
  workoutErrorMessage,
} from '@/features/portal/lib/workout';
import type { components } from '@/shared/api/schema';
import { isApiProblem } from '@/shared/api/problem';
import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';
import { formatNumber } from '@/shared/lib/format';
import { cn } from '@/shared/lib/utils';

type WorkoutSet = components['schemas']['MyWorkoutSetResponse'];

/** Valor inicial de um campo: o registado, senão o planeado, senão vazio. */
function initialText(logged: number | undefined, planned: number | null): string {
  const value = logged ?? planned;
  return value === null ? '' : formatDecimal(value);
}

/**
 * Uma série do treino de hoje: kg, reps e o botão de estado, numa linha de 44 px.
 *
 * - Sem registo: o botão regista (`POST`). Os campos vêm pré-preenchidos com o planeado.
 * - Com registo e os mesmos valores: o botão desmarca (`DELETE`), exceto depois de concluir.
 * - Com registo e valores alterados: o botão passa a "Guardar" e corrige (`PATCH`).
 *
 * O texto escrito vive no estado da linha e nunca é reposto por um erro ou por um refetch:
 * em falha, os valores ficam e o cliente pode repetir.
 *
 * @param prescriptionId `id` do exercício prescrito (`training_plan_day_exercise_id`).
 * @param exerciseName Nome do exercício: os rótulos repetem-se de cartão para cartão sem ele.
 * @param completed O treino de hoje já foi concluído: desmarcar fica bloqueado.
 */
export function WorkoutSetRow({
  prescriptionId,
  exerciseName,
  set,
  completed,
}: {
  prescriptionId: string;
  exerciseName: string;
  set: WorkoutSet;
  completed: boolean;
}) {
  const id = useId();
  const [weight, setWeight] = useState(() =>
    initialText(set.logged?.weight_kg, set.planned_weight_kg)
  );
  const [reps, setReps] = useState(() => initialText(set.logged?.reps_done, set.planned_reps));
  const [error, setError] = useState<string | null>(null);
  const save = useSaveSetMutation();
  const unlog = useUnlogSetMutation();

  const logged = set.logged;
  const weightKg = parseWeight(weight);
  const repsDone = parseReps(reps);
  const dirty = logged !== null && (weightKg !== logged.weight_kg || repsDone !== logged.reps_done);
  const pending = save.isPending || unlog.isPending;
  const label = `série ${formatNumber(set.set_number)} de ${exerciseName}`;

  function submit() {
    setError(null);

    if (logged !== null && !dirty) {
      unlog.mutate(logged.log_id, {
        onError: (failure) =>
          setError(workoutErrorMessage(isApiProblem(failure) ? failure.code : null)),
      });
      return;
    }

    if (weightKg === null || repsDone === null) {
      setError(
        `Indica o peso (0 a ${formatNumber(MAX_WEIGHT_KG)} kg, até 2 casas decimais) e as repetições (0 a ${formatNumber(MAX_REPS)}).`
      );
      return;
    }

    save.mutate(
      { prescriptionId, setNumber: set.set_number, logged, weightKg, repsDone },
      {
        onError: (failure) =>
          setError(workoutErrorMessage(isApiProblem(failure) ? failure.code : null)),
      }
    );
  }

  const action =
    logged === null
      ? { text: `Registar ${label}`, visible: null }
      : dirty
        ? { text: `Guardar ${label}`, visible: 'Guardar' }
        : { text: `Desmarcar ${label}`, visible: null };

  // Desmarcar depois de concluir dá 409 `workout_already_completed`; corrigir continua a ser
  // permitido pelo backend.
  const blocked = logged !== null && !dirty && completed;

  return (
    <li className="space-y-1">
      <div className="grid min-h-11 grid-cols-[2rem_1fr_1fr_auto] items-center gap-2">
        <span className="text-muted-foreground font-mono text-sm tabular-nums">
          {formatNumber(set.set_number)}
        </span>
        <Input
          aria-label={`Peso da ${label} (kg)`}
          aria-invalid={error !== null}
          aria-describedby={error === null ? undefined : `${id}-error`}
          inputMode="decimal"
          autoComplete="off"
          className="h-11 tabular-nums"
          value={weight}
          disabled={pending}
          onChange={(event) => setWeight(event.target.value)}
        />
        <Input
          aria-label={`Repetições da ${label}`}
          aria-invalid={error !== null}
          aria-describedby={error === null ? undefined : `${id}-error`}
          inputMode="numeric"
          autoComplete="off"
          className="h-11 tabular-nums"
          value={reps}
          disabled={pending}
          onChange={(event) => setReps(event.target.value)}
        />
        <Button
          type="button"
          size={action.visible === null ? 'icon' : 'default'}
          variant={logged !== null && !dirty ? 'default' : 'outline'}
          className={cn('h-11', action.visible === null && 'w-11')}
          aria-label={action.text}
          aria-pressed={dirty ? undefined : logged !== null}
          title={blocked ? 'O treino já foi concluído' : undefined}
          disabled={pending || blocked}
          onClick={submit}
        >
          {pending ? (
            <Loader2 aria-hidden className="animate-spin" />
          ) : action.visible === null ? (
            <Check aria-hidden />
          ) : (
            action.visible
          )}
        </Button>
      </div>
      {error !== null && (
        <p id={`${id}-error`} role="alert" className="text-destructive text-sm">
          {error}
        </p>
      )}
    </li>
  );
}
