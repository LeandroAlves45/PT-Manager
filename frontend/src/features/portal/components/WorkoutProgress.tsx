import { progressPercent } from '@/features/portal/lib/workout';
import { Progress } from '@/shared/components/ui/progress';
import { formatNumber } from '@/shared/lib/format';

/**
 * Progresso do treino: «5 de 12 séries · 42 %» e uma barra na cor da marca.
 *
 * O texto diz o mesmo que a barra, por isso a cor nunca é a única pista. A barra é uma
 * `progressbar` com o valor em percentagem e um texto de valor legível por leitores de ecrã.
 *
 * @param logged Séries registadas (`progress.logged_sets`).
 * @param planned Séries planeadas (`progress.planned_sets`).
 */
export function WorkoutProgress({ logged, planned }: { logged: number; planned: number }) {
  const percent = progressPercent(logged, planned);

  return (
    <div className="space-y-1.5">
      <Progress
        value={percent}
        aria-label="Séries registadas"
        aria-valuetext={`${formatNumber(logged)} de ${formatNumber(planned)} séries`}
      />
      <p className="text-muted-foreground font-mono text-xs tabular-nums">
        {formatNumber(logged)} de {formatNumber(planned)} séries · {formatNumber(percent)} %
      </p>
    </div>
  );
}
