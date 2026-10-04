import { CalendarPlus } from 'lucide-react';
import { parseAsInteger, parseAsStringLiteral, useQueryStates } from 'nuqs';
import { useState } from 'react';

import { CheckInsList } from '@/features/check-ins/components/CheckInsList';
import { ScheduleCheckInSheet } from '@/features/check-ins/components/ScheduleCheckInSheet';
import { CHECK_IN_FILTERS } from '@/features/check-ins/lib/checkInStatus';
import { PageHeader } from '@/shared/components/PageHeader';
import { Button } from '@/shared/components/ui/button';

/**
 * "Check-ins" do personal trainer: todos os clientes, filtro de estado e de datas.
 *
 * O estado e a página vivem no URL (`?status=&page=`): o KPI do painel liga a
 * `?status=unreviewed` e a página abre já filtrada nos check-ins por rever.
 */
export function CheckInsPage() {
  const [{ status, page }, setQuery] = useQueryStates({
    status: parseAsStringLiteral(CHECK_IN_FILTERS),
    page: parseAsInteger.withDefault(1),
  });
  const [creating, setCreating] = useState(false);

  return (
    <section className="space-y-6">
      <PageHeader
        title="Check-ins"
        description="Respostas dos clientes, por rever e agendadas."
        action={
          <Button onClick={() => setCreating(true)}>
            <CalendarPlus aria-hidden /> Agendar check-in
          </Button>
        }
      />
      <CheckInsList
        client={null}
        status={status}
        onStatusChange={(next) => void setQuery({ status: next, page: null })}
        page={page}
        onPageChange={(next) => void setQuery({ page: next === 1 ? null : next })}
      />
      <ScheduleCheckInSheet open={creating} onOpenChange={setCreating} client={null} />
    </section>
  );
}
