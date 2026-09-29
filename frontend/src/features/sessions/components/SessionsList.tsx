import { formatISO, parseISO } from 'date-fns';
import { CalendarX, SearchX } from 'lucide-react';
import { useState } from 'react';

import { SESSION_PAGE_SIZE, useSessionListQuery } from '@/features/sessions/api/queries';
import { SessionsTable } from '@/features/sessions/components/SessionsTable';
import { todayKey } from '@/features/sessions/lib/dates';
import {
  SESSION_STATUS_LABELS,
  SESSION_STATUSES,
  type SessionStatus,
} from '@/features/sessions/lib/sessionStatus';
import { EmptyState } from '@/shared/components/EmptyState';
import { ErrorState } from '@/shared/components/ErrorState';
import { Pagination } from '@/shared/components/Pagination';
import { Button } from '@/shared/components/ui/button';
import { NativeSelect } from '@/shared/components/ui/native-select';
import { Skeleton } from '@/shared/components/ui/skeleton';

const PERIODS = ['upcoming', 'past'] as const;
type Period = (typeof PERIODS)[number];
const PERIOD_LABELS: Record<Period, string> = { upcoming: 'A partir de hoje', past: 'Anteriores' };

/**
 * Lista paginada de sessões com filtros de período e estado.
 *
 * "A partir de hoje" começa à meia-noite local de hoje e "Anteriores" acaba aí — o limite é
 * o dia e não o instante atual, para a chave da query não mudar a cada render. A API ordena
 * sempre por `starts_at` crescente (também nas anteriores: a mais antiga primeiro).
 *
 * @param clientId Cliente fixo (tab do detalhe), ou `null` para todos.
 */
export function SessionsList({ clientId }: { clientId: string | null }) {
  const [period, setPeriod] = useState<Period>('upcoming');
  const [status, setStatus] = useState<SessionStatus | null>(null);
  const [page, setPage] = useState(1);
  const boundary = formatISO(parseISO(todayKey()));
  const query = useSessionListQuery({
    clientId,
    status,
    startsFrom: period === 'upcoming' ? boundary : null,
    startsBefore: period === 'past' ? boundary : null,
    page,
    pageSize: SESSION_PAGE_SIZE,
  });

  const items = query.data?.items ?? [];
  const total = query.data?.total_count ?? 0;

  return (
    <section aria-label="Lista de sessões" className="space-y-4">
      <div className="flex flex-wrap items-center gap-3">
        <div role="group" aria-label="Período" className="border-border flex rounded-lg border p-1">
          {PERIODS.map((value) => (
            <Button
              key={value}
              variant={period === value ? 'secondary' : 'ghost'}
              size="sm"
              className="min-h-11 md:min-h-8"
              aria-pressed={period === value}
              onClick={() => {
                setPeriod(value);
                setPage(1);
              }}
            >
              {PERIOD_LABELS[value]}
            </Button>
          ))}
        </div>
        <NativeSelect
          aria-label="Estado"
          className="w-auto"
          value={status ?? ''}
          onChange={(event) => {
            const value = event.target.value;
            setStatus(SESSION_STATUSES.find((item) => item === value) ?? null);
            setPage(1);
          }}
        >
          <option value="">Todos os estados</option>
          {SESSION_STATUSES.map((value) => (
            <option key={value} value={value}>
              {SESSION_STATUS_LABELS[value]}
            </option>
          ))}
        </NativeSelect>
      </div>

      {query.isPending ? (
        <div role="status" aria-label="A carregar sessões…" className="space-y-2">
          {Array.from({ length: 4 }, (_, index) => (
            <Skeleton key={index} className="h-12 w-full" />
          ))}
        </div>
      ) : query.isError ? (
        <ErrorState error={query.error} onRetry={() => void query.refetch()} />
      ) : items.length === 0 ? (
        status !== null || page > 1 ? (
          <EmptyState
            icon={SearchX}
            title="Sem resultados"
            description="Nenhuma sessão corresponde a este filtro."
            action={
              <Button
                variant="outline"
                onClick={() => {
                  setStatus(null);
                  setPage(1);
                }}
              >
                Limpar filtros
              </Button>
            }
          />
        ) : (
          <EmptyState
            icon={CalendarX}
            title={period === 'upcoming' ? 'Sem sessões marcadas' : 'Sem sessões anteriores'}
            description={
              period === 'upcoming'
                ? 'Marca uma sessão para ela aparecer aqui.'
                : 'As sessões passadas aparecem aqui.'
            }
          />
        )
      ) : (
        <SessionsTable sessions={items} showClient={clientId === null} showDate />
      )}

      <Pagination
        label="Páginas de sessões"
        page={page}
        total={total}
        pageSize={SESSION_PAGE_SIZE}
        onPageChange={setPage}
      />
    </section>
  );
}
