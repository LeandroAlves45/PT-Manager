import { format, parseISO } from 'date-fns';
import { CalendarClock } from 'lucide-react';
import { toast } from 'sonner';

import { useCompleteSessionMutation } from '@/features/trainer-dashboard/api/dashboard';
import { BentoCard } from '@/features/trainer-dashboard/components/BentoCard';
import {
  COMPLETE_SESSION_ERRORS,
  SESSION_STATUS_LABELS,
} from '@/features/trainer-dashboard/lib/sessionStatus';
import { isApiProblem } from '@/shared/api/problem';
import type { components } from '@/shared/api/schema';
import { Button } from '@/shared/components/ui/button';

type SessionsToday = components['schemas']['SessionsTodayResponse'];

/** Hora local do browser de um instante ISO (`starts_at` vem com offset). */
function hourOf(value: string): string {
  return format(parseISO(value), 'HH:mm');
}

/**
 * Bloco "Sessões de hoje": até cinco sessões e "Registar presença" nas agendadas.
 *
 * A ação é por linha (uma sessão, uma presença); o botão fica desativado só na linha cujo
 * pedido está a decorrer.
 */
export function SessionsTodayBlock({
  data,
  className,
}: {
  data: SessionsToday;
  className?: string;
}) {
  const complete = useCompleteSessionMutation();

  async function registerAttendance(sessionId: string, clientName: string) {
    try {
      await complete.mutateAsync(sessionId);
      toast.success(`Presença de ${clientName} registada com sucesso.`);
    } catch (error) {
      const message = isApiProblem(error) ? COMPLETE_SESSION_ERRORS[error.code] : undefined;
      toast.error(message ?? 'Não foi possível registar a presença. Tenta novamente.');
    }
  }

  return (
    <BentoCard
      title={`Sessões de hoje · ${data.total_count}`}
      icon={CalendarClock}
      className={className}
      aside={
        data.next_session_starts_at !== null && (
          <span className="text-primary font-mono text-xs">
            próxima {hourOf(data.next_session_starts_at)}
          </span>
        )
      }
    >
      {data.items.length === 0 ? (
        <p className="text-muted-foreground text-sm">Sem sessões marcadas para hoje.</p>
      ) : (
        <ul className="divide-border divide-y">
          {data.items.map((session) => {
            const details = [session.session_type, session.location].filter(
              (value): value is string => value !== null && value.trim() !== ''
            );
            const pending = complete.isPending && complete.variables === session.session_id;

            return (
              <li key={session.session_id} className="flex items-center gap-3 py-2">
                <span className="font-display w-14 shrink-0 text-lg tabular-nums">
                  {hourOf(session.starts_at)}
                </span>
                <div className="min-w-0 flex-1">
                  <p className="truncate text-sm font-medium">{session.client_name}</p>
                  <p className="text-muted-foreground truncate text-xs">
                    {[...details, `${session.duration_minutes} min`].join(' · ')}
                  </p>
                </div>
                {session.status === 'scheduled' ? (
                  <Button
                    size="sm"
                    variant="outline"
                    className="min-h-11 md:min-h-8"
                    disabled={pending}
                    aria-label={`Registar presença de ${session.client_name}`}
                    onClick={() => void registerAttendance(session.session_id, session.client_name)}
                  >
                    {pending ? 'A registar…' : 'Registar presença'}
                  </Button>
                ) : (
                  <span className="text-muted-foreground text-xs">
                    {SESSION_STATUS_LABELS[session.status] ?? session.status}
                  </span>
                )}
              </li>
            );
          })}
        </ul>
      )}
    </BentoCard>
  );
}
