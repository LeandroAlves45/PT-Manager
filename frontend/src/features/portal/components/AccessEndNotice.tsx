import { Clock } from 'lucide-react';

import { accessEndLabel } from '@/features/portal/lib/schedule';
import type { components } from '@/shared/api/schema';

type AccessNotice = components['schemas']['MyAccessNoticeResponse'];

/**
 * Aviso no topo do Início quando o acesso ao portal tem fim marcado.
 *
 * Diz até quando o acesso continua, no fuso do personal trainer. Nunca diz porquê: o motivo e a
 * nota do bloqueio são só do personal trainer.
 */
export function AccessEndNotice({ notice }: { notice: AccessNotice }) {
  return (
    <div
      role="status"
      className="border-border bg-card border-l-warning flex items-start gap-3 rounded-lg border border-l-4 p-3 text-sm"
    >
      <Clock aria-hidden className="text-warning mt-0.5 size-5 shrink-0" />
      <p>
        O teu acesso ao portal continua até{' '}
        <strong className="font-medium">{accessEndLabel(notice.ends_at, notice.time_zone)}</strong>.
        Depois dessa hora o acesso termina.
      </p>
    </div>
  );
}
