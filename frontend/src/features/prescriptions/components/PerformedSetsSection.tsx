import { format, parseISO } from 'date-fns';
import { pt } from 'date-fns/locale';
import { CheckCircle2, Plus } from 'lucide-react';
import { useMemo, useState } from 'react';
import { Link } from 'react-router';

import {
  useSetLogsQuery,
  useWorkoutCompletionsQuery,
  SET_LOGS_PAGE_SIZE,
} from '@/features/prescriptions/api/training';
import { groupSetLogs, setLogLine } from '@/features/prescriptions/lib/training-logs';
import { useTrainerSettingsQuery } from '@/features/trainer-settings/api/settings';
import type { TrainingDetails } from '@/features/prescriptions/lib/training-draft';
import { formatDate } from '@/shared/lib/format';
import { ErrorState } from '@/shared/components/ErrorState';
import { Pagination } from '@/shared/components/Pagination';
import { Button } from '@/shared/components/ui/button';
import { Skeleton } from '@/shared/components/ui/skeleton';

/**
 * Séries realizadas de um plano, só para ler (U64): agrupadas por dia e por exercício, com o
 * planeado ao lado do feito, e a nota de conclusão do treino desse dia (U63).
 *
 * É o cliente quem regista no dia (RN-007/FL-003); o trainer vê e, se for preciso, corrige uma
 * série (`Corrigir`, uma página própria) ou regista uma que o cliente apagou. Plano
 * arquivado: tudo só de consulta, sem `Corrigir` nem `Registar série`.
 *
 * @param plan Detalhe do plano: dá o cliente, o planeado e se está arquivado.
 * @param clientSearch `?client_id=...` ou `''`, conservado nos links para o regresso à ficha.
 */
export function PerformedSetsSection({
  plan,
  clientSearch,
}: {
  plan: TrainingDetails;
  clientSearch: string;
}) {
  const [page, setPage] = useState(1);
  const logs = useSetLogsQuery(plan.client_id, plan.id, page);
  const completions = useWorkoutCompletionsQuery(plan.client_id, plan.id);
  // O dia das séries é o do fuso do trainer; Lisboa enquanto as definições não chegam.
  const timeZone = useTrainerSettingsQuery().data?.timezone ?? 'Europe/Lisbon';
  const days = useMemo(
    () =>
      logs.data === undefined
        ? []
        : groupSetLogs(logs.data.items, plan, completions.data?.items ?? [], timeZone, page > 1),
    [logs.data, completions.data, plan, timeZone, page]
  );
  const base = `/trainer/training-plans/${plan.id}/set-logs`;
  const pageParam = clientSearch === '' ? `?page=${page}` : `${clientSearch}&page=${page}`;

  return (
    <section aria-labelledby="performed-sets" className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h2 id="performed-sets" className="font-display text-xl">
          Séries realizadas
        </h2>
        {!plan.is_archived && (
          <Button asChild variant="outline" className="min-h-11 md:min-h-9">
            <Link to={`${base}/new${clientSearch}`}>
              <Plus aria-hidden /> Registar série
            </Link>
          </Button>
        )}
      </div>
      {completions.isError && (
        <p role="alert" className="text-destructive text-sm">
          Não foi possível carregar as notas de conclusão do treino.{' '}
          <button
            type="button"
            className="underline underline-offset-2"
            onClick={() => void completions.refetch()}
          >
            Tentar novamente
          </button>
        </p>
      )}
      {logs.isPending ? (
        <Skeleton role="status" aria-label="A carregar séries…" className="h-32 w-full" />
      ) : logs.isError ? (
        <ErrorState error={logs.error} onRetry={() => void logs.refetch()} />
      ) : days.length === 0 ? (
        <p className="text-muted-foreground">Nenhuma série registada neste plano.</p>
      ) : (
        <>
          <div className="space-y-4">
            {days.map((day) => (
              <article key={day.date} className="border-border space-y-3 rounded-xl border p-4">
                <h3 className="text-primary font-medium capitalize">
                  {format(parseISO(day.date), 'EEEE, dd/MM/yyyy', { locale: pt })}
                </h3>
                {day.completions.map((completion) => (
                  <div
                    key={completion.id}
                    className="bg-primary/10 flex items-start gap-2 rounded-lg p-3 text-sm"
                  >
                    <CheckCircle2 aria-hidden className="text-primary mt-0.5 size-4 shrink-0" />
                    <div className="space-y-1">
                      <p className="font-medium">
                        Treino concluído a {formatDate(completion.local_date)}
                      </p>
                      {completion.notes !== null && completion.notes.trim() !== '' && (
                        <p className="text-muted-foreground whitespace-pre-line">
                          Nota do cliente: {completion.notes}
                        </p>
                      )}
                    </div>
                  </div>
                ))}
                {day.exercises.map((exercise) => (
                  <div key={exercise.id} className="space-y-2">
                    <h4 className="font-medium">{exercise.name}</h4>
                    <ul className="space-y-2">
                      {exercise.entries.map((entry) => (
                        <li
                          key={entry.log.id}
                          className="bg-muted/30 flex flex-wrap items-center justify-between gap-2 rounded-lg px-3 py-2"
                        >
                          <span className="text-sm">{setLogLine(entry)}</span>
                          {!plan.is_archived && (
                            <Button
                              asChild
                              variant="outline"
                              size="sm"
                              className="min-h-11 md:min-h-8"
                            >
                              <Link
                                to={`${base}/${entry.log.id}${pageParam}`}
                                aria-label={`Corrigir ${exercise.name}, série ${entry.log.set_number}`}
                              >
                                Corrigir
                              </Link>
                            </Button>
                          )}
                        </li>
                      ))}
                    </ul>
                  </div>
                ))}
              </article>
            ))}
          </div>
          <Pagination
            label="Páginas de séries"
            page={page}
            total={logs.data.total_count}
            pageSize={SET_LOGS_PAGE_SIZE}
            onPageChange={setPage}
          />
        </>
      )}
    </section>
  );
}
