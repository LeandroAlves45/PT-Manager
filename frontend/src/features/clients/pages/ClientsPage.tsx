import { Archive, Plus, RotateCcw, SearchX, UserPlus } from 'lucide-react';
import { parseAsBoolean, parseAsInteger, parseAsString, useQueryStates } from 'nuqs';
import { useState } from 'react';
import { Link, useNavigate } from 'react-router';
import { toast } from 'sonner';

import type { ClientListFilters } from '@/features/clients/api/keys';
import { useClientActivityMutation } from '@/features/clients/api/mutations';
import { CLIENT_PAGE_SIZE, useClientListQuery } from '@/features/clients/api/queries';
import { ClientForm } from '@/features/clients/components/ClientForm';
import { activityErrorMessage, ageFrom } from '@/features/clients/lib/labels';
import type { components } from '@/shared/api/schema';
import { ConfirmDialog } from '@/shared/components/ConfirmDialog';
import { EmptyState } from '@/shared/components/EmptyState';
import { ErrorState } from '@/shared/components/ErrorState';
import { PageHeader } from '@/shared/components/PageHeader';
import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetHeader,
  SheetTitle,
} from '@/shared/components/ui/sheet';
import { Skeleton } from '@/shared/components/ui/skeleton';
import { useDebounce } from '@/shared/hooks/useDebounce';
import { formatNumber } from '@/shared/lib/format';

type ClientSummary = components['schemas']['ClientSummaryResponse'];

const ACTIVITY = ['active', 'archived', 'all'] as const;
const ACTIVITY_LABELS = { active: 'Ativos', archived: 'Arquivados', all: 'Todos' } as const;

/**
 * Lista de clientes do personal trainer.
 *
 * Só usa os campos de `ClientSummaryResponse` — o pack de cada cliente aparece no detalhe
 * (decisão do utilizador: nenhum pedido extra por linha nem alteração ao backend).
 * Pesquisa, estado e página vivem no URL; `?new=true` abre o formulário de criação (é o
 * destino do "Novo cliente" do painel).
 */
export function ClientsPage() {
  const navigate = useNavigate();
  const [url, setUrl] = useQueryStates({
    search: parseAsString.withDefault(''),
    activity: parseAsString.withDefault('active'),
    page: parseAsInteger.withDefault(1),
    new: parseAsBoolean.withDefault(false),
  });
  const search = useDebounce(url.search.trim(), 300);
  const activity = ACTIVITY.find((value) => value === url.activity) ?? 'active';
  const page = Math.max(1, url.page);
  const filters: ClientListFilters = { search, activity, page };
  const query = useClientListQuery(filters);
  const activityMutation = useClientActivityMutation();
  const [confirm, setConfirm] = useState<ClientSummary | null>(null);

  const items = query.data?.items ?? [];
  const total = query.data?.total_count ?? 0;
  const pageCount = Math.max(1, Math.ceil(total / CLIENT_PAGE_SIZE));
  // Uma página vazia além da primeira (último cliente arquivado, `?page=` antigo) não é
  // onboarding: mostra "Sem resultados" e "Limpar filtros" volta à página 1.
  const filtered = search !== '' || activity !== 'active' || page > 1;

  async function toggleActivity() {
    if (confirm === null) return;

    try {
      await activityMutation.mutateAsync({
        clientId: confirm.id,
        action: confirm.is_active ? 'archive' : 'reactivate',
      });
      toast.success(
        confirm.is_active ? 'Cliente arquivado com sucesso.' : 'Cliente reativado com sucesso.'
      );
      setConfirm(null);
    } catch (error) {
      toast.error(activityErrorMessage(error));
    }
  }

  return (
    <section className="space-y-6">
      <PageHeader
        title="Clientes"
        description={`${formatNumber(total)} ${total === 1 ? 'cliente' : 'clientes'}`}
        action={
          <Button onClick={() => void setUrl({ new: true })}>
            <Plus aria-hidden /> Novo cliente
          </Button>
        }
      />
      <div className="flex flex-wrap items-center gap-3">
        <Input
          aria-label="Pesquisar clientes"
          placeholder="Pesquisar por nome"
          value={url.search}
          onChange={(event) => void setUrl({ search: event.target.value, page: 1 })}
          className="max-w-xs"
        />
        <div role="group" aria-label="Estado" className="border-border flex rounded-lg border p-1">
          {ACTIVITY.map((value) => (
            <Button
              key={value}
              variant={activity === value ? 'secondary' : 'ghost'}
              size="sm"
              className="min-h-11 md:min-h-8"
              aria-pressed={activity === value}
              onClick={() => void setUrl({ activity: value, page: 1 })}
            >
              {ACTIVITY_LABELS[value]}
            </Button>
          ))}
        </div>
      </div>

      {query.isPending ? (
        <div role="status" aria-label="A carregar clientes…" className="space-y-2">
          {Array.from({ length: 5 }, (_, index) => (
            <Skeleton key={index} className="h-12 w-full" />
          ))}
        </div>
      ) : query.isError ? (
        <ErrorState error={query.error} onRetry={() => void query.refetch()} />
      ) : items.length === 0 ? (
        filtered ? (
          <EmptyState
            icon={SearchX}
            title="Sem resultados"
            description="Nenhum cliente corresponde a esta pesquisa e filtro."
            action={
              <Button
                variant="outline"
                onClick={() => void setUrl({ search: '', activity: 'active', page: 1 })}
              >
                Limpar filtros
              </Button>
            }
          />
        ) : (
          <EmptyState
            icon={UserPlus}
            title="Ainda não tens clientes"
            description="Cria a ficha do primeiro cliente. Depois podes convidá-lo para o portal."
            action={
              <Button onClick={() => void setUrl({ new: true })}>Criar primeiro cliente</Button>
            }
          />
        )
      ) : (
        <div className="border-border bg-card overflow-x-auto rounded-xl border">
          <table className="w-full min-w-2xl text-sm">
            <thead className="bg-muted text-left">
              <tr>
                <th scope="col" className="p-3">
                  Nome
                </th>
                <th scope="col" className="p-3">
                  Contacto
                </th>
                <th scope="col" className="p-3">
                  Idade
                </th>
                <th scope="col" className="p-3">
                  Objetivo
                </th>
                <th scope="col" className="p-3">
                  Estado
                </th>
                <th scope="col" className="p-3">
                  Ações
                </th>
              </tr>
            </thead>
            <tbody>
              {items.map((client) => (
                <tr key={client.id} className="border-border border-t">
                  <th scope="row" className="p-3 text-left font-medium">
                    <Link to={`/trainer/clients/${client.id}`} className="hover:underline">
                      {client.name}
                    </Link>
                  </th>
                  <td className="text-muted-foreground p-3">
                    <span className="block">{client.phone}</span>
                    {client.contact_email !== null && (
                      <span className="block text-xs">{client.contact_email}</span>
                    )}
                  </td>
                  <td className="p-3 tabular-nums">{ageFrom(client.birth_date)}</td>
                  <td className="text-muted-foreground p-3">{client.objective ?? '—'}</td>
                  <td className="p-3">{client.is_active ? 'Ativo' : 'Arquivado'}</td>
                  <td className="p-2">
                    <Button
                      size="icon-sm"
                      className="size-11 md:size-8"
                      variant="ghost"
                      aria-label={`${client.is_active ? 'Arquivar' : 'Reativar'} ${client.name}`}
                      onClick={() => setConfirm(client)}
                    >
                      {client.is_active ? <Archive aria-hidden /> : <RotateCcw aria-hidden />}
                    </Button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {total > CLIENT_PAGE_SIZE && (
        <nav aria-label="Páginas de clientes" className="flex items-center justify-end gap-3">
          <Button
            variant="outline"
            disabled={page <= 1}
            onClick={() => void setUrl({ page: page - 1 })}
          >
            Anterior
          </Button>
          <span>
            Página {page} de {pageCount}
          </span>
          <Button
            variant="outline"
            disabled={page >= pageCount}
            onClick={() => void setUrl({ page: page + 1 })}
          >
            Seguinte
          </Button>
        </nav>
      )}

      <Sheet
        open={url.new}
        onOpenChange={(open) => {
          if (!open) void setUrl({ new: null });
        }}
      >
        <SheetContent className="w-full sm:max-w-130">
          <SheetHeader>
            <SheetTitle>Novo cliente</SheetTitle>
            <SheetDescription>
              A ficha fica no teu espaço. O convite para o portal envia-se a seguir, no detalhe.
            </SheetDescription>
          </SheetHeader>
          {url.new && (
            <div className="min-h-0 flex-1 overflow-y-auto px-4">
              <ClientForm
                client={null}
                onSaved={(client) => void navigate(`/trainer/clients/${client.id}`)}
              />
            </div>
          )}
        </SheetContent>
      </Sheet>

      <ConfirmDialog
        open={confirm !== null}
        onOpenChange={(open) => {
          if (!open) setConfirm(null);
        }}
        title={confirm?.is_active ? 'Arquivar cliente?' : 'Reativar cliente?'}
        description={
          confirm?.is_active
            ? `${confirm.name} deixa de contar para o limite do plano e sai das listas ativas. Podes reativá-lo mais tarde.`
            : `${confirm?.name ?? ''} volta às listas ativas e conta para o limite do plano.`
        }
        confirmLabel={confirm?.is_active ? 'Arquivar' : 'Reativar'}
        destructive={confirm?.is_active === true}
        pending={activityMutation.isPending}
        onConfirm={() => void toggleActivity()}
      />
    </section>
  );
}
