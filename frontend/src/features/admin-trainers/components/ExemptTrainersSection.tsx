import { useState } from 'react';

import {
  EXEMPT_PAGE_SIZE,
  useExemptTrainersQuery,
  type AdminTrainer,
} from '@/features/admin-trainers/api/trainers';
import { ErrorState } from '@/shared/components/ErrorState';
import { Pagination } from '@/shared/components/Pagination';
import { Button } from '@/shared/components/ui/button';
import { Skeleton } from '@/shared/components/ui/skeleton';
import { formatLisbonDateTime } from '@/shared/lib/format';

/**
 * Lista paginada dos treinadores isentos de pagamento, com a ação de tirar a isenção.
 *
 * A página vive em estado local (a lista é secundária e não se partilha por link). Quando
 * tirar uma isenção esvazia a última página, volta-se à última que ainda existe.
 *
 * @param onRemoveExemption Abre a confirmação de tirar a isenção a um treinador.
 */
export function ExemptTrainersSection({
  onRemoveExemption,
}: {
  onRemoveExemption: (trainer: AdminTrainer) => void;
}) {
  const [page, setPage] = useState(1);
  const query = useExemptTrainersQuery(page);
  const items = query.data?.items ?? [];
  const total = query.data?.total_count ?? 0;

  // Esvaziar a última página: ajustar o estado durante a renderização (padrão do React).
  if (query.isSuccess && !query.isPlaceholderData && items.length === 0 && page > 1)
    setPage(Math.max(1, Math.ceil(total / EXEMPT_PAGE_SIZE)));

  return (
    <section aria-labelledby="exempt-trainers-title" className="space-y-3">
      <h2 id="exempt-trainers-title" className="font-display text-xl">
        Isentos
      </h2>

      {query.isPending ? (
        <div role="status" aria-label="A carregar isentos…" className="space-y-2">
          <Skeleton className="h-10 w-full" />
          <Skeleton className="h-10 w-full" />
        </div>
      ) : query.isError ? (
        <ErrorState error={query.error} onRetry={() => void query.refetch()} />
      ) : items.length === 0 ? (
        <p className="text-muted-foreground text-sm">Ainda não há treinadores isentos.</p>
      ) : (
        <div className="border-border bg-card overflow-x-auto rounded-xl border">
          <table className="w-full min-w-2xl text-sm">
            <thead className="bg-muted text-left">
              <tr>
                <th scope="col" className="p-3">
                  Nome
                </th>
                <th scope="col" className="p-3">
                  Email
                </th>
                <th scope="col" className="p-3">
                  Isento desde
                </th>
                <th scope="col" className="p-3">
                  Ação
                </th>
              </tr>
            </thead>
            <tbody>
              {items.map((trainer) => (
                <tr key={trainer.trainer_id} className="border-border border-t">
                  <th scope="row" className="p-3 text-left font-medium">
                    {trainer.full_name ?? 'Sem nome'}
                  </th>
                  <td className="p-3 break-all">{trainer.email}</td>
                  <td className="p-3">
                    {trainer.billing_exempt_since === null
                      ? '—'
                      : formatLisbonDateTime(trainer.billing_exempt_since)}
                  </td>
                  <td className="p-2">
                    <Button
                      size="sm"
                      variant="outline"
                      className="min-h-11 md:min-h-8"
                      aria-label={`Tirar isenção a ${trainer.full_name ?? trainer.email}`}
                      onClick={() => onRemoveExemption(trainer)}
                    >
                      Tirar isenção
                    </Button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      <Pagination
        label="Páginas de isentos"
        page={page}
        total={total}
        pageSize={EXEMPT_PAGE_SIZE}
        onPageChange={setPage}
      />
    </section>
  );
}
