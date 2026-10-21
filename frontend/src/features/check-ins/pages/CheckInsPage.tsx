import { CalendarPlus } from 'lucide-react';
import { parseAsInteger, parseAsStringLiteral, useQueryStates } from 'nuqs';
import { Link } from 'react-router';

import { CheckInsList } from '@/features/check-ins/components/CheckInsList';
import { CHECK_IN_FILTERS } from '@/features/check-ins/lib/checkInStatus';
import { PageHeader } from '@/shared/components/PageHeader';
import { Button } from '@/shared/components/ui/button';

/**
 * "Avaliações" (periódicas, antes «check-ins») do personal trainer: todos os clientes, filtro de
 * estado e de datas. O endereço mantém-se `/trainer/check-ins`.
 *
 * O estado e a página vivem no URL (`?status=&page=`): o KPI do painel liga a
 * `?status=unreviewed` e a página abre já filtrada nas avaliações por rever.
 */
export function CheckInsPage() {
  const [{ status, page }, setQuery] = useQueryStates({
    status: parseAsStringLiteral(CHECK_IN_FILTERS),
    page: parseAsInteger.withDefault(1),
  });

  return (
    <section className="space-y-6">
      <PageHeader
        title="Avaliações"
        description="Respostas dos clientes, por rever e agendadas."
        action={
          <Button asChild>
            <Link to="/trainer/check-ins/new">
              <CalendarPlus aria-hidden /> Agendar avaliação
            </Link>
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
    </section>
  );
}
