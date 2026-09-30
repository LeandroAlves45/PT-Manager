import type { ReactNode } from 'react';

import { LibraryItemBadges } from '@/features/library/components/LibraryPanel';
import type { LibraryItem } from '@/features/library/lib/items';
import { platformEnforcementReasonLabel } from '@/shared/lib/platformEnforcement';

/**
 * Leitura de um item que não se edita: global (só da plataforma), arquivado (reativa-se na
 * lista) ou bloqueado pela moderação (com o motivo).
 *
 * @param rows Campos do item, por ordem; um valor vazio aparece como "—".
 * @param children Conteúdo extra no fim (o vídeo do exercício, em modo de leitura).
 */
export function LibraryItemDetails({
  item,
  rows,
  children,
}: {
  item: LibraryItem & { readonly platform_enforcement_reason?: string | null };
  rows: readonly { readonly label: string; readonly value: ReactNode }[];
  children?: ReactNode;
}) {
  return (
    <div className="space-y-4">
      <LibraryItemBadges item={item} />
      {item.scope === 'global' ? (
        <p className="text-muted-foreground text-sm">
          Item global da plataforma: podes usá-lo nos planos, mas não o podes editar.
        </p>
      ) : item.platform_enforcement_status === 'blocked' ? (
        <p role="note" className="text-destructive text-sm">
          {`Bloqueado pela moderação da plataforma. Motivo: ${platformEnforcementReasonLabel(
            item.platform_enforcement_reason ?? null
          )}. Enquanto estiver bloqueado, não o podes editar.`}
        </p>
      ) : (
        !item.is_active && (
          <p className="text-muted-foreground text-sm">
            Arquivado: reativa-o na lista para o voltares a editar e a usar em planos novos.
          </p>
        )
      )}
      <dl className="grid grid-cols-[minmax(0,10rem)_1fr] gap-x-4 gap-y-2 text-sm">
        {rows.map((row) => (
          <div key={row.label} className="contents">
            <dt className="text-muted-foreground">{row.label}</dt>
            <dd className="wrap-break-word whitespace-pre-line">
              {row.value === null || row.value === '' ? '—' : row.value}
            </dd>
          </div>
        ))}
      </dl>
      {children}
    </div>
  );
}
