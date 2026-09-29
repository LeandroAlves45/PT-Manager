import { format, parseISO } from 'date-fns';
import { pt } from 'date-fns/locale';
import { CalendarPlus, ChevronLeft, ChevronRight } from 'lucide-react';

import { DAY_PAGE_SIZE, useSessionListQuery } from '@/features/sessions/api/queries';
import { SessionsTable } from '@/features/sessions/components/SessionsTable';
import { dayRange, shiftDay, todayKey } from '@/features/sessions/lib/dates';
import { EmptyState } from '@/shared/components/EmptyState';
import { ErrorState } from '@/shared/components/ErrorState';
import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';
import { Skeleton } from '@/shared/components/ui/skeleton';

/**
 * Agenda de um dia: ‹ Hoje › e um seletor de data, com as sessões desse dia.
 *
 * Orçamento de pedidos: um `GET /sessions?starts_from&starts_before` por dia visitado, com
 * a página máxima (o dia cabe sempre numa página).
 *
 * @param day Dia aberto ("yyyy-MM-dd"), controlado por quem chama para "Marcar sessão"
 *   pré-preencher o mesmo dia.
 */
export function DayAgenda({
  day,
  onDayChange,
  onNewSession,
}: {
  day: string;
  onDayChange: (day: string) => void;
  onNewSession: () => void;
}) {
  const range = dayRange(day);
  const query = useSessionListQuery({
    clientId: null,
    status: null,
    startsFrom: range.startsFrom,
    startsBefore: range.startsBefore,
    page: 1,
    pageSize: DAY_PAGE_SIZE,
  });
  const items = query.data?.items ?? [];
  const isToday = day === todayKey();

  return (
    <section aria-label="Agenda do dia" className="space-y-4">
      <div className="flex flex-wrap items-center gap-2">
        <Button
          variant="outline"
          size="icon"
          className="size-11 md:size-9"
          aria-label="Dia anterior"
          onClick={() => onDayChange(shiftDay(day, -1))}
        >
          <ChevronLeft aria-hidden />
        </Button>
        <Button
          variant={isToday ? 'secondary' : 'outline'}
          className="min-h-11 md:min-h-9"
          aria-pressed={isToday}
          onClick={() => onDayChange(todayKey())}
        >
          Hoje
        </Button>
        <Button
          variant="outline"
          size="icon"
          className="size-11 md:size-9"
          aria-label="Dia seguinte"
          onClick={() => onDayChange(shiftDay(day, 1))}
        >
          <ChevronRight aria-hidden />
        </Button>
        <Input
          type="date"
          aria-label="Escolher dia"
          className="w-auto"
          value={day}
          onChange={(event) => {
            if (event.target.value !== '') onDayChange(event.target.value);
          }}
        />
        <h2 className="font-display ml-1 text-xl first-letter:uppercase">
          {format(parseISO(day), "EEEE, d 'de' MMMM", { locale: pt })}
        </h2>
      </div>

      {query.isPending ? (
        <div role="status" aria-label="A carregar agenda…" className="space-y-2">
          {Array.from({ length: 3 }, (_, index) => (
            <Skeleton key={index} className="h-12 w-full" />
          ))}
        </div>
      ) : query.isError ? (
        <ErrorState error={query.error} onRetry={() => void query.refetch()} />
      ) : items.length === 0 ? (
        <EmptyState
          icon={CalendarPlus}
          title="Sem sessões neste dia"
          description="Marca uma sessão ou escolhe outro dia."
          action={<Button onClick={onNewSession}>Marcar sessão</Button>}
        />
      ) : (
        <SessionsTable sessions={items} showClient showDate={false} />
      )}
    </section>
  );
}
