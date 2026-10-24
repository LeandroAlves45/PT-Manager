import { ClipboardList } from 'lucide-react';
import { parseAsInteger, useQueryState } from 'nuqs';
import { useEffect } from 'react';
import { Link } from 'react-router';

import {
  PENDING_REVIEW_PAGE_SIZE,
  usePendingInitialAssessmentsQuery,
} from '@/features/initial-assessment/api/pendingReview';
import { EmptyState } from '@/shared/components/EmptyState';
import { ErrorState } from '@/shared/components/ErrorState';
import { PageHeader } from '@/shared/components/PageHeader';
import { Pagination } from '@/shared/components/Pagination';
import { Button } from '@/shared/components/ui/button';
import { Skeleton } from '@/shared/components/ui/skeleton';
import { formatDate } from '@/shared/lib/format';

/**
 * Avaliações iniciais que os clientes preencheram no portal e ainda não foram revistas
 * (`/trainer/initial-assessments`), das mais antigas para as mais recentes.
 *
 * A página vive no URL (`?page=`); o pedido leva `page_number`. Cada linha abre a avaliação
 * do cliente, onde se corrige e se marca como revista.
 */
export function PendingInitialAssessmentsPage() {
  const [urlPage, setPage] = useQueryState('page', parseAsInteger.withDefault(1));
  const page = Math.max(1, urlPage);
  const list = usePendingInitialAssessmentsQuery(page);
  const lastPage = list.data
    ? Math.max(1, Math.ceil(list.data.total_count / PENDING_REVIEW_PAGE_SIZE))
    : 1;
  const pastTheEnd = list.data !== undefined && list.data.items.length === 0 && page > lastPage;

  // Rever a última avaliação de uma página esvazia-a: volta à última página que ainda existe.
  useEffect(() => {
    if (pastTheEnd) void setPage(lastPage === 1 ? null : lastPage);
  }, [pastTheEnd, lastPage, setPage]);

  return (
    <section className="space-y-6">
      <PageHeader
        title="Avaliações iniciais por rever"
        description="Avaliações que os clientes preencheram no portal, das mais antigas para as mais recentes."
      />
      {list.isPending || pastTheEnd ? (
        <Skeleton
          role="status"
          aria-label="A carregar avaliações iniciais…"
          className="h-48 w-full"
        />
      ) : list.isError ? (
        <ErrorState error={list.error} onRetry={() => void list.refetch()} />
      ) : list.data.items.length === 0 ? (
        <EmptyState
          icon={ClipboardList}
          title="Não há avaliações iniciais por rever."
          description="Quando um cliente preencher a avaliação inicial no portal, aparece aqui."
        />
      ) : (
        <>
          <ul className="border-border bg-card divide-border divide-y rounded-xl border">
            {list.data.items.map((item) => (
              <li
                key={item.assessment_id}
                className="flex flex-wrap items-center justify-between gap-3 p-3"
              >
                <div className="min-w-0">
                  <p className="truncate text-sm font-medium">{item.client_name}</p>
                  <p className="text-muted-foreground text-xs tabular-nums">
                    Preenchida a {formatDate(item.submitted_at)}
                  </p>
                </div>
                <Button asChild variant="outline" size="sm" className="min-h-11 md:min-h-8">
                  <Link
                    to={`/trainer/clients/${item.client_id}/initial-assessment`}
                    aria-label={`Rever a avaliação inicial de ${item.client_name}`}
                  >
                    Rever
                  </Link>
                </Button>
              </li>
            ))}
          </ul>
          <Pagination
            label="Páginas de avaliações iniciais"
            page={page}
            total={list.data.total_count}
            pageSize={PENDING_REVIEW_PAGE_SIZE}
            onPageChange={(next) => void setPage(next === 1 ? null : next)}
          />
        </>
      )}
    </section>
  );
}
