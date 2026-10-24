import { Ban } from 'lucide-react';

import { Badge } from '@/shared/components/ui/badge';
import { cn } from '@/shared/lib/utils';
import { platformEnforcementReasonLabel } from '@/shared/lib/platformEnforcement';

/**
 * Marca de um item do catálogo bloqueado pela moderação da plataforma: «Bloqueado», o motivo
 * e a nota do superuser, quando existe. O estado está sempre em texto (não depende só da cor)
 * e o motivo e a nota quebram de linha, para caberem numa linha de item a 375 px.
 *
 * @param enforcement Bloqueio devolvido pela API (`platform_enforcement`): código do motivo e nota.
 * @param className Classes extra para o contentor (margens, alinhamento).
 */
export function PlatformEnforcementBadge({
  enforcement,
  className,
}: {
  enforcement: { readonly reason: string; readonly note: string | null };
  className?: string;
}) {
  const reason = platformEnforcementReasonLabel(enforcement.reason);
  const note = enforcement.note?.trim() ?? '';
  const detail = [
    reason === '—' ? null : `Motivo: ${reason}`,
    note === '' ? null : `Nota: ${note}`,
  ].filter((part): part is string => part !== null);

  return (
    <span className={cn('flex flex-wrap items-center gap-x-2 gap-y-1 text-xs', className)}>
      <Badge variant="destructive">
        <Ban aria-hidden /> Bloqueado
      </Badge>
      {detail.length > 0 && (
        <span className="text-muted-foreground min-w-0 wrap-break-word">{detail.join(' · ')}</span>
      )}
    </span>
  );
}
