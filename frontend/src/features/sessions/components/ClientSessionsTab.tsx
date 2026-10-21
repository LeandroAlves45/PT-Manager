import { CalendarPlus } from 'lucide-react';
import { Link } from 'react-router';

import type { ClientChoice } from '@/features/clients';
import { ClientPacksPanel } from '@/features/packs';
import { SessionsList } from '@/features/sessions/components/SessionsList';
import { Button } from '@/shared/components/ui/button';

/**
 * Tab "Sessões" do detalhe do cliente: os packs dele (vender, fim previsto, cancelar) e as
 * sessões dele (lista com as mesmas ações da agenda), mais "Marcar sessão" com o cliente
 * já escolhido (página `SessionEditorPage` com `?client_id=`).
 *
 * Orçamento de pedidos: só ao abrir a tab — `GET /client-session-packs?client_id` e
 * `GET /sessions?client_id`; os packs com saldo para o formulário só com ele aberto.
 *
 * @param client Cliente do detalhe.
 * @param canSchedule `false` num cliente arquivado: o backend recusaria sessões novas.
 */
export function ClientSessionsTab({
  client,
  canSchedule,
}: {
  client: ClientChoice;
  canSchedule: boolean;
}) {
  return (
    <div className="space-y-8">
      <section className="space-y-3">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <h3 className="font-medium">Sessões</h3>
          {canSchedule && (
            <Button asChild>
              <Link to={`/trainer/sessions/new?client_id=${client.id}`}>
                <CalendarPlus aria-hidden /> Marcar sessão
              </Link>
            </Button>
          )}
        </div>
        <SessionsList clientId={client.id} />
      </section>
      <section className="space-y-3">
        <h3 className="font-medium">Packs</h3>
        <ClientPacksPanel client={client} />
      </section>
    </div>
  );
}
