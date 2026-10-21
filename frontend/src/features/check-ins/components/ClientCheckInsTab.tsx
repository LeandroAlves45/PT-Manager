import { CalendarPlus } from 'lucide-react';
import { useState } from 'react';
import { Link } from 'react-router';

import { CheckInsList } from '@/features/check-ins/components/CheckInsList';
import type { CheckInStatusFilter } from '@/features/check-ins/lib/checkInStatus';
import type { ClientChoice } from '@/features/clients';
import { Button } from '@/shared/components/ui/button';

/**
 * Tab "Avaliações" do detalhe do cliente (`?tab=check-ins`): as avaliações periódicas dele, com
 * as mesmas ações da página, e "Agendar avaliação" com o cliente já escolhido (página
 * `CheckInSchedulePage` com `?client_id=`).
 *
 * Também cobre o bloco "Últimas avaliações" do layout: o resumo 6B não traz a
 * lista, e esta tab mostra-a ordenada do mais recente, sem endpoint novo.
 *
 * Orçamento de pedidos: só ao abrir a tab — `GET /check-ins?client_id`.
 *
 * @param client Cliente do detalhe.
 * @param canSchedule `false` num cliente arquivado: o backend recusaria avaliações novas.
 */
export function ClientCheckInsTab({
  client,
  canSchedule,
}: {
  client: ClientChoice;
  canSchedule: boolean;
}) {
  const [status, setStatus] = useState<CheckInStatusFilter | null>(null);
  const [page, setPage] = useState(1);

  return (
    <section className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h2 className="font-display text-xl">Avaliações</h2>
        {canSchedule && (
          <Button asChild>
            <Link to={`/trainer/check-ins/new?client_id=${client.id}`}>
              <CalendarPlus aria-hidden /> Agendar avaliação
            </Link>
          </Button>
        )}
      </div>
      <CheckInsList
        client={client}
        status={status}
        onStatusChange={(next) => {
          setStatus(next);
          setPage(1);
        }}
        page={page}
        onPageChange={setPage}
      />
    </section>
  );
}
