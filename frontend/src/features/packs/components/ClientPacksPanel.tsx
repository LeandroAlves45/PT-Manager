import { CalendarCog, Package, Plus, SearchX, XCircle } from 'lucide-react';
import { useState } from 'react';
import { Link } from 'react-router';
import { toast } from 'sonner';

import type { ClientChoice } from '@/features/clients';
import type { ClientPackListFilters } from '@/features/packs/api/keys';
import { useCancelPackMutation } from '@/features/packs/api/mutations';
import { PACK_PAGE_SIZE, useClientPackListQuery } from '@/features/packs/api/queries';
import { AssignPackForm } from '@/features/packs/components/AssignPackForm';
import { PackEndDateDialog } from '@/features/packs/components/PackEndDateDialog';
import { balanceLabel, CANCEL_PACK_ERRORS, priceLabel } from '@/features/packs/lib/labels';
import { isApiProblem } from '@/shared/api/problem';
import type { components } from '@/shared/api/schema';
import { ConfirmDialog } from '@/shared/components/ConfirmDialog';
import { EmptyState } from '@/shared/components/EmptyState';
import { ErrorState } from '@/shared/components/ErrorState';
import { Pagination } from '@/shared/components/Pagination';
import { Button } from '@/shared/components/ui/button';
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetHeader,
  SheetTitle,
} from '@/shared/components/ui/sheet';
import { Skeleton } from '@/shared/components/ui/skeleton';
import { formatDate } from '@/shared/lib/format';

type ClientPack = components['schemas']['ClientSessionPackResponse'];

const ACTIVITY = ['usable', 'completed', 'all'] as const;
const ACTIVITY_LABELS = { usable: 'Com saldo', completed: 'Terminados', all: 'Todos' } as const;

/**
 * Packs vendidos aos clientes: saldo, preço da venda, fim previsto; vender, alterar o fim
 * previsto e cancelar um pack vendido por engano.
 *
 * Serve o separador "Packs dos clientes" (`client === null`, todos os clientes) e a tab
 * Sessões do detalhe (`client` fixo: sem coluna Cliente e a venda já sabe para quem é).
 *
 * @param client Cliente fixo, ou `null` para a lista de todos.
 */
export function ClientPacksPanel({ client }: { client: ClientChoice | null }) {
  const [activity, setActivity] = useState<ClientPackListFilters['activity']>('usable');
  const [page, setPage] = useState(1);
  const query = useClientPackListQuery({ clientId: client?.id ?? null, activity, page });
  const cancel = useCancelPackMutation();
  const [assigning, setAssigning] = useState(false);
  const [endDatePack, setEndDatePack] = useState<ClientPack | null>(null);
  const [cancelPack, setCancelPack] = useState<ClientPack | null>(null);

  const items = query.data?.items ?? [];
  const total = query.data?.total_count ?? 0;

  async function confirmCancel() {
    if (cancelPack === null) return;

    try {
      await cancel.mutateAsync({ packId: cancelPack.id, clientId: cancelPack.client_id });
      toast.success('Pack cancelado com sucesso.');
      setCancelPack(null);
    } catch (error) {
      const message = isApiProblem(error) ? CANCEL_PACK_ERRORS[error.code] : undefined;
      toast.error(message ?? 'Não foi possível cancelar o pack. Tenta novamente.');
    }
  }

  return (
    <section aria-label="Packs dos clientes" className="space-y-4">
      <div className="flex flex-wrap items-center gap-3">
        <div
          role="group"
          aria-label="Saldo dos packs"
          className="border-border flex rounded-lg border p-1"
        >
          {ACTIVITY.map((value) => (
            <Button
              key={value}
              variant={activity === value ? 'secondary' : 'ghost'}
              size="sm"
              className="min-h-11 md:min-h-8"
              aria-pressed={activity === value}
              onClick={() => {
                setActivity(value);
                setPage(1);
              }}
            >
              {ACTIVITY_LABELS[value]}
            </Button>
          ))}
        </div>
        <Button className="ml-auto" onClick={() => setAssigning(true)}>
          <Plus aria-hidden /> Vender pack
        </Button>
      </div>

      {query.isPending ? (
        <div role="status" aria-label="A carregar packs…" className="space-y-2">
          {Array.from({ length: 3 }, (_, index) => (
            <Skeleton key={index} className="h-12 w-full" />
          ))}
        </div>
      ) : query.isError ? (
        <ErrorState error={query.error} onRetry={() => void query.refetch()} />
      ) : items.length === 0 ? (
        activity !== 'usable' || page > 1 ? (
          <EmptyState
            icon={SearchX}
            title="Sem resultados"
            description="Nenhum pack corresponde a este filtro."
            action={
              <Button
                variant="outline"
                onClick={() => {
                  setActivity('usable');
                  setPage(1);
                }}
              >
                Ver packs com saldo
              </Button>
            }
          />
        ) : (
          <EmptyState
            icon={Package}
            title="Sem packs com saldo"
            description={
              client === null
                ? 'Vende um pack a um cliente para as sessões descontarem do saldo.'
                : `${client.name} não tem packs com saldo.`
            }
            action={<Button onClick={() => setAssigning(true)}>Vender pack</Button>}
          />
        )
      ) : (
        <div className="border-border bg-card overflow-x-auto rounded-xl border">
          <table className="w-full min-w-2xl text-sm">
            <thead className="bg-muted text-left">
              <tr>
                {client === null && (
                  <th scope="col" className="p-3">
                    Cliente
                  </th>
                )}
                <th scope="col" className="p-3">
                  Pack
                </th>
                <th scope="col" className="p-3">
                  Saldo
                </th>
                <th scope="col" className="p-3">
                  Preço
                </th>
                <th scope="col" className="p-3">
                  Compra
                </th>
                <th scope="col" className="p-3">
                  Fim previsto
                </th>
                <th scope="col" className="p-3">
                  Ações
                </th>
              </tr>
            </thead>
            <tbody>
              {items.map((pack) => {
                const unused = pack.sessions_remaining === pack.sessions_total;
                return (
                  <tr key={pack.id} className="border-border border-t">
                    {client === null && (
                      <td className="p-3 font-medium">
                        <Link to={`/trainer/clients/${pack.client_id}`} className="hover:underline">
                          {pack.client_name}
                        </Link>
                      </td>
                    )}
                    <th scope="row" className="p-3 text-left font-medium">
                      {pack.pack_name}
                    </th>
                    <td className="p-3 tabular-nums">{balanceLabel(pack)}</td>
                    <td className="p-3 tabular-nums">{priceLabel(pack)}</td>
                    <td className="text-muted-foreground p-3">{formatDate(pack.purchase_date)}</td>
                    <td className="text-muted-foreground p-3">
                      {pack.expected_end_date === null ? '—' : formatDate(pack.expected_end_date)}
                    </td>
                    <td className="flex gap-1 p-2">
                      <Button
                        size="icon-sm"
                        className="size-11 md:size-8"
                        variant="ghost"
                        aria-label={`Alterar fim previsto de ${pack.pack_name} (${pack.client_name})`}
                        onClick={() => setEndDatePack(pack)}
                      >
                        <CalendarCog aria-hidden />
                      </Button>
                      {unused && (
                        <Button
                          size="icon-sm"
                          className="size-11 md:size-8"
                          variant="ghost"
                          aria-label={`Cancelar ${pack.pack_name} (${pack.client_name})`}
                          onClick={() => setCancelPack(pack)}
                        >
                          <XCircle aria-hidden />
                        </Button>
                      )}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}

      <Pagination
        label="Páginas de packs"
        page={page}
        total={total}
        pageSize={PACK_PAGE_SIZE}
        onPageChange={setPage}
      />

      <Sheet open={assigning} onOpenChange={setAssigning}>
        <SheetContent className="w-full sm:max-w-130">
          <SheetHeader>
            <SheetTitle>Vender pack</SheetTitle>
            <SheetDescription>
              {client === null
                ? 'As sessões marcadas depois descontam deste pack.'
                : `Para ${client.name}. As sessões marcadas depois descontam deste pack.`}
            </SheetDescription>
          </SheetHeader>
          {assigning && (
            <div className="min-h-0 flex-1 overflow-y-auto px-4">
              <AssignPackForm client={client} onSaved={() => setAssigning(false)} />
            </div>
          )}
        </SheetContent>
      </Sheet>

      <PackEndDateDialog
        key={endDatePack?.id ?? 'closed'}
        pack={endDatePack}
        onClose={() => setEndDatePack(null)}
      />

      <ConfirmDialog
        open={cancelPack !== null}
        onOpenChange={(open) => {
          if (!open) setCancelPack(null);
        }}
        title="Cancelar pack?"
        description={
          cancelPack === null
            ? ''
            : `${cancelPack.pack_name} de ${cancelPack.client_name} deixa de existir. Usa isto só para uma venda feita por engano.`
        }
        confirmLabel="Cancelar pack"
        pendingLabel="A cancelar…"
        destructive
        pending={cancel.isPending}
        onConfirm={() => void confirmCancel()}
      />
    </section>
  );
}
