import { CalendarPlus } from 'lucide-react';
import { useState } from 'react';

import { CheckInsList } from '@/features/check-ins/components/CheckInsList';
import { ScheduleCheckInSheet } from '@/features/check-ins/components/ScheduleCheckInSheet';
import type { CheckInStatusFilter } from '@/features/check-ins/lib/checkInStatus';
import type { ClientChoice } from '@/features/clients';
import { Button } from '@/shared/components/ui/button';

/**
 * Tab "Check-ins" do detalhe do cliente: os check-ins dele, com as mesmas ações da página,
 * e "Agendar check-in" com o cliente já escolhido.
 *
 * Também cobre o bloco "Últimos check-ins" do layout: o resumo 6B não traz a
 * lista, e esta tab mostra-a ordenada do mais recente, sem endpoint novo.
 *
 * Orçamento de pedidos: só ao abrir a tab — `GET /check-ins?client_id`.
 *
 * @param client Cliente do detalhe.
 * @param canSchedule `false` num cliente arquivado: o backend recusaria check-ins novos.
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
  const [creating, setCreating] = useState(false);

  return (
    <section className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h2 className="font-display text-xl">Check-ins</h2>
        {canSchedule && (
          <Button onClick={() => setCreating(true)}>
            <CalendarPlus aria-hidden /> Agendar check-in
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
      <ScheduleCheckInSheet open={creating} onOpenChange={setCreating} client={client} />
    </section>
  );
}
