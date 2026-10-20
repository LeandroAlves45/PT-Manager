import { ShieldCheck } from 'lucide-react';
import { parseAsInteger, parseAsString, useQueryStates } from 'nuqs';
import { useState } from 'react';
import { toast } from 'sonner';

import {
  useModerationAction,
  useModerationQuery,
  type ModerationKind,
  type ModerationStatus,
} from '@/features/admin-moderation/api/moderation';
import type { components } from '@/shared/api/schema';
import { EmptyState } from '@/shared/components/EmptyState';
import { ErrorState } from '@/shared/components/ErrorState';
import { PageHeader } from '@/shared/components/PageHeader';
import { Button } from '@/shared/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/shared/components/ui/dialog';
import { Input } from '@/shared/components/ui/input';
import { Label } from '@/shared/components/ui/label';
import { NativeSelect } from '@/shared/components/ui/native-select';
import { Skeleton } from '@/shared/components/ui/skeleton';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/shared/components/ui/tabs';
import { useDebounce } from '@/shared/hooks/useDebounce';
import { formatCount, formatDateTime } from '@/shared/lib/format';
import {
  PLATFORM_ENFORCEMENT_REASONS,
  platformEnforcementReasonLabel,
  type PlatformEnforcementReason,
} from '@/shared/lib/platformEnforcement';

type QueueItem = components['schemas']['ModerationQueueItemResponse'];
const STATUS = ['all', 'allowed', 'blocked'] as const;
const STATUS_LABEL = { all: 'Todos', allowed: 'Permitidos', blocked: 'Bloqueados' } as const;

/** Fila administrativa de conteúdos privados, sem expor dados de outro tenant fora desta rota. */
export function ModerationPage() {
  const [url, setUrl] = useQueryStates({
    kind: parseAsString.withDefault('foods'),
    status: parseAsString.withDefault('all'),
    search: parseAsString.withDefault(''),
    page: parseAsInteger.withDefault(1),
  });
  const [selected, setSelected] = useState<QueueItem | null>(null);
  const [reason, setReason] = useState<PlatformEnforcementReason | ''>('');
  const kind: ModerationKind = url.kind === 'exercises' ? 'exercises' : 'foods';
  const status: ModerationStatus = STATUS.find((value) => value === url.status) ?? 'all';
  const page = Math.max(1, url.page);
  const query = useModerationQuery(kind, status, useDebounce(url.search.trim(), 300), page);
  const action = useModerationAction();
  const total = query.data?.total_count ?? 0;

  function close() {
    setSelected(null);
    setReason('');
  }

  async function submit() {
    if (selected === null) return;

    const isBlocked = selected.platform_enforcement_status === 'blocked';
    if (!isBlocked && reason === '') return;

    try {
      await action.mutateAsync({
        kind,
        id: selected.id,
        action: isBlocked ? 'unblock' : 'block',
        reason: isBlocked ? undefined : reason,
      });
      toast.success(
        isBlocked ? 'Conteúdo desbloqueado com sucesso.' : 'Conteúdo bloqueado com sucesso.'
      );
      close();
    } catch {
      toast.error('Não foi possível guardar a decisão. Tenta novamente.');
    }
  }

  return (
    <section className="space-y-6">
      <PageHeader
        title="Moderação"
        description={formatCount(total, 'conteúdo privado', 'conteúdos privados')}
      />
      <Tabs
        value={kind}
        onValueChange={(value) => void setUrl({ kind: value, page: 1 })}
        className="gap-3"
      >
        <TabsList aria-label="Tipo de conteúdo">
          <TabsTrigger value="foods">Alimentos</TabsTrigger>
          <TabsTrigger value="exercises">Exercícios</TabsTrigger>
        </TabsList>
        {/* O painel do separador ativo envolve filtros, tabela e diálogo: sem ele o aria-controls
            dos separadores apontava para um id inexistente. */}
        <TabsContent value={kind} className="space-y-6">
          <div className="flex flex-wrap items-end gap-3">
            <div className="space-y-1.5">
              <Label htmlFor="moderation-status">Estado</Label>
              <NativeSelect
                id="moderation-status"
                value={status}
                onChange={(event) => void setUrl({ status: event.target.value, page: 1 })}
                className="min-h-11 w-full sm:w-44 md:min-h-9"
              >
                {STATUS.map((value) => (
                  <option key={value} value={value}>
                    {STATUS_LABEL[value]}
                  </option>
                ))}
              </NativeSelect>
            </div>
            <Input
              aria-label="Pesquisar conteúdo"
              placeholder="Pesquisar por nome"
              value={url.search}
              onChange={(event) => void setUrl({ search: event.target.value, page: 1 })}
              className="min-h-11 w-full sm:max-w-xs md:min-h-9"
            />
          </div>
          {query.isPending ? (
            <div aria-label="A carregar moderação" className="space-y-2">
              {Array.from({ length: 5 }, (_, index) => (
                <Skeleton key={index} className="h-12 w-full" />
              ))}
            </div>
          ) : query.isError ? (
            <ErrorState error={query.error} onRetry={() => void query.refetch()} />
          ) : query.data.items.length === 0 ? (
            <EmptyState
              icon={ShieldCheck}
              title="Sem conteúdos"
              description="Não há conteúdos para este filtro."
            />
          ) : (
            <div className="border-border bg-card overflow-x-auto rounded-xl border">
              <table className="w-full min-w-2xl text-sm">
                <thead className="bg-muted text-left">
                  <tr>
                    {['Nome', 'Personal trainer', 'Estado', 'Motivo', 'Decisão', 'Ação'].map(
                      (heading) => (
                        <th key={heading} scope="col" className="p-3">
                          {heading}
                        </th>
                      )
                    )}
                  </tr>
                </thead>
                <tbody>
                  {query.data.items.map((item) => (
                    <tr key={item.id} className="border-border border-t">
                      <th scope="row" className="p-3 text-left font-medium">
                        {item.name}
                      </th>
                      <td className="p-3">{item.owner_trainer_name ?? 'Nome indisponível'}</td>
                      <td className="p-3">
                        {item.platform_enforcement_status === 'blocked' ? 'Bloqueado' : 'Permitido'}
                      </td>
                      <td className="p-3">
                        {platformEnforcementReasonLabel(item.platform_enforcement_reason)}
                      </td>
                      <td className="p-3">
                        {item.platform_enforced_at
                          ? formatDateTime(item.platform_enforced_at)
                          : '—'}
                      </td>
                      <td className="p-3">
                        <Button size="sm" variant="outline" onClick={() => setSelected(item)}>
                          {item.platform_enforcement_status === 'blocked'
                            ? 'Desbloquear'
                            : 'Bloquear'}
                        </Button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
          {total > 25 && (
            <nav aria-label="Páginas da moderação" className="flex items-center justify-end gap-3">
              <Button
                variant="outline"
                disabled={page <= 1}
                onClick={() => void setUrl({ page: page - 1 })}
              >
                Anterior
              </Button>
              <span>
                Página {page} de {Math.ceil(total / 25)}
              </span>
              <Button
                variant="outline"
                disabled={page * 25 >= total}
                onClick={() => void setUrl({ page: page + 1 })}
              >
                Seguinte
              </Button>
            </nav>
          )}
          <Dialog
            open={selected !== null}
            onOpenChange={(open) => {
              if (!open) close();
            }}
          >
            <DialogContent>
              <DialogHeader>
                <DialogTitle>
                  {selected?.platform_enforcement_status === 'blocked'
                    ? 'Desbloquear conteúdo?'
                    : 'Bloquear conteúdo?'}
                </DialogTitle>
                <DialogDescription>
                  Esta decisão fica registada na auditoria administrativa.
                </DialogDescription>
              </DialogHeader>
              {selected?.platform_enforcement_status !== 'blocked' && (
                <fieldset className="space-y-2">
                  <legend className="text-sm font-medium">Motivo obrigatório</legend>
                  {Object.entries(PLATFORM_ENFORCEMENT_REASONS).map(([code, label]) => (
                    <label key={code} className="flex items-center gap-2 text-sm">
                      <input
                        type="radio"
                        name="reason"
                        value={code}
                        checked={reason === code}
                        onChange={() => setReason(code as PlatformEnforcementReason)}
                      />
                      {label}
                    </label>
                  ))}
                </fieldset>
              )}
              <DialogFooter>
                <Button variant="outline" onClick={close}>
                  Cancelar
                </Button>
                <Button
                  disabled={
                    action.isPending ||
                    (selected?.platform_enforcement_status !== 'blocked' && reason === '')
                  }
                  onClick={() => void submit()}
                >
                  {action.isPending ? 'A guardar…' : 'Confirmar'}
                </Button>
              </DialogFooter>
            </DialogContent>
          </Dialog>
        </TabsContent>
      </Tabs>
    </section>
  );
}
