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
import { Skeleton } from '@/shared/components/ui/skeleton';
import { useDebounce } from '@/shared/hooks/useDebounce';
import { formatDateTime, formatNumber } from '@/shared/lib/format';

type QueueItem = components['schemas']['ModerationQueueItemResponse'];
const REASONS = {
  malicious_content: 'Conteúdo malicioso',
  dangerous_information: 'Informação perigosa',
  deliberately_false_information: 'Informação deliberadamente falsa',
  prohibited_content: 'Conteúdo proibido',
} as const;
const STATUS = ['all', 'allowed', 'blocked'] as const;

/** Fila administrativa de conteúdos privados, sem expor dados de outro tenant fora desta rota. */
export function ModerationPage() {
  const [url, setUrl] = useQueryStates({
    kind: parseAsString.withDefault('foods'),
    status: parseAsString.withDefault('all'),
    search: parseAsString.withDefault(''),
    page: parseAsInteger.withDefault(1),
  });
  const [selected, setSelected] = useState<QueueItem | null>(null);
  const [reason, setReason] = useState<keyof typeof REASONS | ''>('');
  const kind: ModerationKind = url.kind === 'exercises' ? 'exercises' : 'foods';
  const status: ModerationStatus = STATUS.find((value) => value === url.status) ?? 'all';
  const page = Math.max(1, url.page);
  const query = useModerationQuery(kind, status, useDebounce(url.search.trim(), 300), page);
  const action = useModerationAction();
  const total = query.data?.total_count ?? 0;

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
      setSelected(null);
      setReason('');
    } catch {
      toast.error('Não foi possível guardar a decisão. Tenta novamente.');
    }
  }

  return (
    <section className="space-y-6">
      <PageHeader
        title="Moderação"
        description={`${formatNumber(total)} ${total === 1 ? 'conteúdo privado' : 'conteúdos privados'}`}
      />
      <div className="flex flex-wrap gap-3">
        <div role="group" aria-label="Tipo de conteúdo" className="flex gap-1">
          {(['foods', 'exercises'] as const).map((value) => (
            <Button
              key={value}
              size="sm"
              className="min-h-11 md:min-h-8"
              variant={kind === value ? 'secondary' : 'ghost'}
              aria-pressed={kind === value}
              onClick={() => void setUrl({ kind: value, page: 1 })}
            >
              {value === 'foods' ? 'Alimentos' : 'Exercícios'}
            </Button>
          ))}
        </div>
        <div role="group" aria-label="Estado" className="flex gap-1">
          {STATUS.map((value) => (
            <Button
              key={value}
              size="sm"
              className="min-h-11 md:min-h-8"
              variant={status === value ? 'secondary' : 'ghost'}
              aria-pressed={status === value}
              onClick={() => void setUrl({ status: value, page: 1 })}
            >
              {{ all: 'Todos', allowed: 'Permitidos', blocked: 'Bloqueados' }[value]}
            </Button>
          ))}
        </div>
        <Input
          aria-label="Pesquisar conteúdo"
          placeholder="Pesquisar por nome"
          value={url.search}
          onChange={(event) => void setUrl({ search: event.target.value, page: 1 })}
          className="max-w-xs"
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
                    {REASONS[item.platform_enforcement_reason as keyof typeof REASONS] ?? '—'}
                  </td>
                  <td className="p-3">
                    {item.platform_enforced_at ? formatDateTime(item.platform_enforced_at) : '—'}
                  </td>
                  <td className="p-3">
                    <Button size="sm" variant="outline" onClick={() => setSelected(item)}>
                      {item.platform_enforcement_status === 'blocked' ? 'Desbloquear' : 'Bloquear'}
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
          if (!open) {
            setSelected(null);
            setReason('');
          }
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
              {Object.entries(REASONS).map(([code, label]) => (
                <label key={code} className="flex items-center gap-2 text-sm">
                  <input
                    type="radio"
                    name="reason"
                    value={code}
                    checked={reason === code}
                    onChange={() => setReason(code as keyof typeof REASONS)}
                  />
                  {label}
                </label>
              ))}
            </fieldset>
          )}
          <DialogFooter>
            <Button variant="outline" onClick={() => setSelected(null)}>
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
    </section>
  );
}
