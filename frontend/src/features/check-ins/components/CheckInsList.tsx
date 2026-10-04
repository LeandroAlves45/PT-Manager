import { ClipboardCheck, MoreHorizontal } from 'lucide-react';
import { useEffect, useState } from 'react';
import { Link } from 'react-router';
import { toast } from 'sonner';

import { useCheckInActionMutation } from '@/features/check-ins/api/mutations';
import { CHECK_IN_PAGE_SIZE, useCheckInListQuery } from '@/features/check-ins/api/queries';
import { CheckInDetailSheet } from '@/features/check-ins/components/CheckInDetailSheet';
import { ScheduleCheckInSheet } from '@/features/check-ins/components/ScheduleCheckInSheet';
import {
  CHECK_IN_FILTER_LABELS,
  CHECK_IN_FILTERS,
  CHECK_IN_STATUS_LABELS,
  checkInErrorMessage,
  isAwaitingReview,
  isOpenFuture,
  type CheckInStatusFilter,
} from '@/features/check-ins/lib/checkInStatus';
import { todayKey } from '@/features/check-ins/lib/dates';
import type { ClientChoice } from '@/features/clients';
import { isApiProblem } from '@/shared/api/problem';
import type { components } from '@/shared/api/schema';
import { ConfirmDialog } from '@/shared/components/ConfirmDialog';
import { EmptyState } from '@/shared/components/EmptyState';
import { ErrorState } from '@/shared/components/ErrorState';
import { Pagination } from '@/shared/components/Pagination';
import { Badge } from '@/shared/components/ui/badge';
import { Button } from '@/shared/components/ui/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/shared/components/ui/dropdown-menu';
import { Input } from '@/shared/components/ui/input';
import { NativeSelect } from '@/shared/components/ui/native-select';
import { Skeleton } from '@/shared/components/ui/skeleton';
import { formatDate, formatNumber } from '@/shared/lib/format';

type CheckIn = components['schemas']['CheckInResponse'];

/**
 * Tabela de check-ins com filtros de estado e de datas, usada pela página global e pela tab
 * do cliente.
 *
 * Ações por linha, só quando o servidor as aceitaria:
 * - respondido: "Ver resposta" (leitura e correção) e, se por rever, "Marcar revisto";
 * - agendado e futuro: "Reagendar" e "Cancelar" (com confirmação).
 *
 * O estado do filtro pertence a quem chama: a página guarda-o no URL (o painel liga a
 * `?status=unreviewed`), a tab do cliente em memória.
 *
 * @param client Cliente fixo (tab) ou `null` (página global, mostra a coluna Cliente).
 * @param status Filtro de estado ativo, ou `null` para todos.
 * @param onStatusChange Muda o filtro; quem chama volta à página 1.
 * @param page Página atual (1-based).
 * @param onPageChange Muda de página.
 */
export function CheckInsList({
  client,
  status,
  onStatusChange,
  page,
  onPageChange,
}: {
  client: ClientChoice | null;
  status: CheckInStatusFilter | null;
  onStatusChange: (status: CheckInStatusFilter | null) => void;
  page: number;
  onPageChange: (page: number) => void;
}) {
  const [fromDate, setFromDate] = useState('');
  const [toDate, setToDate] = useState('');
  const [viewing, setViewing] = useState<CheckIn | null>(null);
  const [rescheduling, setRescheduling] = useState<CheckIn | null>(null);
  const [cancelling, setCancelling] = useState<CheckIn | null>(null);

  const action = useCheckInActionMutation();
  const invalidRange = fromDate !== '' && toDate !== '' && fromDate > toDate;
  const list = useCheckInListQuery({
    clientId: client?.id ?? null,
    status,
    // Um intervalo invertido daria 400: não se envia.
    fromDate: invalidRange || fromDate === '' ? null : fromDate,
    toDate: invalidRange || toDate === '' ? null : toDate,
    page,
    pageSize: CHECK_IN_PAGE_SIZE,
  });

  // Rever ou cancelar o último check-in de uma página tira-o do filtro e a página fica vazia
  // e sem paginação: volta-se à última página que ainda existe.
  const total = list.data?.total_count;
  useEffect(() => {
    if (total === undefined || list.isPlaceholderData) return;
    const lastPage = Math.max(1, Math.ceil(total / CHECK_IN_PAGE_SIZE));
    if (page > lastPage) onPageChange(lastPage);
  }, [total, list.isPlaceholderData, page, onPageChange]);

  const today = todayKey();
  const showClient = client === null;
  const filtered = status !== null || fromDate !== '' || toDate !== '' || page > 1;

  async function review(checkIn: CheckIn) {
    try {
      await action.mutateAsync({ checkInId: checkIn.id, action: 'review' });
      toast.success(`Check-in de ${checkIn.client_name} marcado como revisto.`);
    } catch (error) {
      toast.error(
        checkInErrorMessage(
          isApiProblem(error) ? error.code : '',
          'Não foi possível marcar o check-in como revisto. Tenta novamente.'
        )
      );
    }
  }

  async function cancel(checkIn: CheckIn) {
    try {
      await action.mutateAsync({ checkInId: checkIn.id, action: 'cancel' });
      toast.success(`Check-in de ${checkIn.client_name} cancelado.`);
      setCancelling(null);
    } catch (error) {
      toast.error(
        checkInErrorMessage(
          isApiProblem(error) ? error.code : '',
          'Não foi possível cancelar o check-in. Tenta novamente.'
        )
      );
    }
  }

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-end gap-3">
        <label className="flex flex-col gap-1 text-sm">
          Estado
          <NativeSelect
            className="w-44"
            value={status ?? ''}
            onChange={(event) => {
              const next = CHECK_IN_FILTERS.find((value) => value === event.target.value);
              onStatusChange(next ?? null);
            }}
          >
            <option value="">Todos</option>
            {CHECK_IN_FILTERS.map((value) => (
              <option key={value} value={value}>
                {CHECK_IN_FILTER_LABELS[value]}
              </option>
            ))}
          </NativeSelect>
        </label>
        <label className="flex flex-col gap-1 text-sm">
          De
          <Input
            type="date"
            className="w-40"
            value={fromDate}
            onChange={(event) => {
              setFromDate(event.target.value);
              onPageChange(1);
            }}
          />
        </label>
        <label className="flex flex-col gap-1 text-sm">
          Até
          <Input
            type="date"
            className="w-40"
            value={toDate}
            onChange={(event) => {
              setToDate(event.target.value);
              onPageChange(1);
            }}
          />
        </label>
      </div>
      {invalidRange && (
        <p role="alert" className="text-destructive text-sm">
          A data inicial não pode ser depois da final.
        </p>
      )}

      {list.isPending ? (
        <Skeleton role="status" aria-label="A carregar check-ins…" className="h-48 w-full" />
      ) : list.isError ? (
        <ErrorState error={list.error} onRetry={() => void list.refetch()} />
      ) : list.data.items.length === 0 ? (
        <EmptyState
          icon={ClipboardCheck}
          title="Sem check-ins"
          description={
            filtered
              ? 'Não há check-ins para estes filtros.'
              : 'Agenda um check-in para acompanhar o progresso.'
          }
        />
      ) : (
        <>
          <div className="border-border bg-card overflow-x-auto rounded-xl border">
            <table className="w-full min-w-2xl text-sm">
              <thead className="bg-muted text-left">
                <tr>
                  <th scope="col" className="p-3">
                    Dia
                  </th>
                  {showClient && (
                    <th scope="col" className="p-3">
                      Cliente
                    </th>
                  )}
                  <th scope="col" className="p-3">
                    Estado
                  </th>
                  <th scope="col" className="p-3">
                    Peso
                  </th>
                  <th scope="col" className="p-3">
                    Ações
                  </th>
                </tr>
              </thead>
              <tbody>
                {list.data.items.map((checkIn) => {
                  const pending = action.isPending && action.variables?.checkInId === checkIn.id;
                  const awaiting = isAwaitingReview(checkIn);
                  const open = isOpenFuture(checkIn, today);
                  const label = `${checkIn.client_name} de ${formatDate(checkIn.check_in_date)}`;

                  return (
                    <tr key={checkIn.id} className="border-border border-t">
                      <th scope="row" className="p-3 text-left font-medium tabular-nums">
                        {formatDate(checkIn.check_in_date)}
                      </th>
                      {showClient && (
                        <td className="p-3">
                          <Link
                            to={`/trainer/clients/${checkIn.client_id}?tab=check-ins`}
                            className="hover:underline"
                          >
                            {checkIn.client_name}
                          </Link>
                        </td>
                      )}
                      <td className="p-3">
                        <div className="flex flex-wrap gap-1">
                          <Badge variant={checkIn.status === 'scheduled' ? 'secondary' : 'outline'}>
                            {CHECK_IN_STATUS_LABELS[checkIn.status] ?? checkIn.status}
                          </Badge>
                          {awaiting && <Badge>Por rever</Badge>}
                        </div>
                      </td>
                      <td className="p-3 tabular-nums">
                        {checkIn.weight_kg === null
                          ? '—'
                          : `${formatNumber(checkIn.weight_kg, 1)} kg`}
                      </td>
                      <td className="flex items-center gap-1 p-2">
                        {checkIn.status === 'answered' && (
                          <Button
                            size="sm"
                            variant="outline"
                            className="min-h-11 md:min-h-8"
                            aria-label={`Ver resposta do check-in de ${label}`}
                            onClick={() => setViewing(checkIn)}
                          >
                            Ver resposta
                          </Button>
                        )}
                        {awaiting && (
                          <Button
                            size="sm"
                            className="min-h-11 md:min-h-8"
                            disabled={pending}
                            aria-label={`Marcar como revisto o check-in de ${label}`}
                            onClick={() => void review(checkIn)}
                          >
                            {pending ? 'A marcar…' : 'Marcar revisto'}
                          </Button>
                        )}
                        {open && (
                          <DropdownMenu>
                            <DropdownMenuTrigger asChild>
                              <Button
                                size="icon-sm"
                                variant="ghost"
                                className="size-11 md:size-8"
                                disabled={pending}
                                aria-label={`Mais ações do check-in de ${label}`}
                              >
                                <MoreHorizontal aria-hidden />
                              </Button>
                            </DropdownMenuTrigger>
                            <DropdownMenuContent align="end">
                              <DropdownMenuItem onSelect={() => setRescheduling(checkIn)}>
                                Reagendar
                              </DropdownMenuItem>
                              <DropdownMenuItem onSelect={() => setCancelling(checkIn)}>
                                Cancelar
                              </DropdownMenuItem>
                            </DropdownMenuContent>
                          </DropdownMenu>
                        )}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
          <Pagination
            label="Páginas de check-ins"
            page={page}
            total={list.data.total_count}
            pageSize={CHECK_IN_PAGE_SIZE}
            onPageChange={onPageChange}
          />
        </>
      )}

      <CheckInDetailSheet
        // A linha guardada fica desatualizada depois de corrigir: lê-se sempre da lista.
        checkIn={
          viewing === null
            ? null
            : (list.data?.items.find((item) => item.id === viewing.id) ?? viewing)
        }
        onClose={() => setViewing(null)}
      />
      <ScheduleCheckInSheet
        open={rescheduling !== null}
        onOpenChange={(open) => {
          if (!open) setRescheduling(null);
        }}
        client={client}
        {...(rescheduling === null ? {} : { checkIn: rescheduling })}
      />
      <ConfirmDialog
        open={cancelling !== null}
        onOpenChange={(open) => {
          if (!open) setCancelling(null);
        }}
        title="Cancelar check-in?"
        description={
          cancelling === null
            ? ''
            : `O check-in de ${cancelling.client_name} a ${formatDate(cancelling.check_in_date)} deixa de estar agendado.`
        }
        confirmLabel="Cancelar check-in"
        pendingLabel="A cancelar…"
        destructive
        pending={action.isPending}
        onConfirm={() => {
          if (cancelling !== null) void cancel(cancelling);
        }}
      />
    </div>
  );
}
