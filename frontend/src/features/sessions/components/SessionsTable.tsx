import { format, parseISO } from 'date-fns';
import { MoreHorizontal } from 'lucide-react';
import { useState } from 'react';
import { Link } from 'react-router';
import { toast } from 'sonner';

import { useSessionTransitionMutation } from '@/features/sessions/api/mutations';
import { RescheduleForm } from '@/features/sessions/components/RescheduleForm';
import { SessionPackDialog } from '@/features/sessions/components/SessionPackDialog';
import {
  SESSION_ERRORS,
  SESSION_STATUS_LABELS,
  TRANSITION_SUCCESS,
  type SessionTransition,
} from '@/features/sessions/lib/sessionStatus';
import { isApiProblem } from '@/shared/api/problem';
import type { components } from '@/shared/api/schema';
import { ConfirmDialog } from '@/shared/components/ConfirmDialog';
import { Badge } from '@/shared/components/ui/badge';
import { Button } from '@/shared/components/ui/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/shared/components/ui/dropdown-menu';
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetHeader,
  SheetTitle,
} from '@/shared/components/ui/sheet';
import { formatDate } from '@/shared/lib/format';

type Session = components['schemas']['TrainingSessionResponse'];

/** Ações que pedem confirmação: consomem o pack ou tiram a sessão da agenda. */
type ConfirmedTransition = Extract<
  SessionTransition,
  'no-show' | 'cancel-by-trainer' | 'cancel-by-client'
>;

const CONFIRM_COPY: Record<
  ConfirmedTransition,
  { title: string; description: (name: string) => string; label: string }
> = {
  'no-show': {
    title: 'Marcar falta?',
    description: (name) =>
      `${name} não compareceu. Se a sessão tiver pack, desconta uma sessão do saldo.`,
    label: 'Marcar falta',
  },
  'cancel-by-trainer': {
    title: 'Cancelar sessão?',
    description: (name) => `Cancela a sessão com ${name}. O pack não é descontado.`,
    label: 'Cancelar sessão',
  },
  'cancel-by-client': {
    title: 'Cancelada pelo cliente?',
    description: (name) => `${name} cancelou a sessão. O pack não é descontado.`,
    label: 'Registar cancelamento',
  },
};

/** Horal local de um instante ISO. */
function hourOf(value: string): string {
  return format(parseISO(value), 'HH:mm');
}

/**
 * Tabela de sessões com as ações de cada estado.
 *
 * Agendada: "Registar presença" à vista e, no menu, falta, reagendar, trocar pack e os dois
 * cancelamentos. Estado final: "Repor como agendada" (corrige um registo errado e devolve a
 * sessão ao pack quando tinha sido descontada). Só a linha cujo pedido corre fica ocupada.
 *
 * @param showClient Coluna Cliente (escondida na tab do próprio cliente).
 * @param showDate Coluna Dia (escondida na agenda de um só dia).
 */
export function SessionsTable({
  sessions,
  showClient,
  showDate,
}: {
  sessions: readonly Session[];
  showClient: boolean;
  showDate: boolean;
}) {
  const transition = useSessionTransitionMutation();
  const [confirm, setConfirm] = useState<{
    session: Session;
    transition: ConfirmedTransition;
  } | null>(null);
  const [rescheduling, setRescheduling] = useState<Session | null>(null);
  const [changingPack, setChangingPack] = useState<Session | null>(null);

  async function run(session: Session, action: SessionTransition) {
    try {
      await transition.mutateAsync({ sessionId: session.id, transition: action });
      toast.success(TRANSITION_SUCCESS[action]);
      setConfirm(null);
    } catch (error) {
      const message = isApiProblem(error) ? SESSION_ERRORS[error.code] : undefined;
      toast.error(message ?? 'Não foi possível concluir a ação. Tenta novamente.');
    }
  }

  return (
    <>
      <div className="border-border bg-card overflow-x-auto rounded-xl border">
        <table className="w-full min-w-2xl text-sm">
          <thead className="bg-muted text-left">
            <tr>
              {showDate && (
                <th scope="col" className="p-3">
                  Dia
                </th>
              )}
              <th scope="col" className="p-3">
                Hora
              </th>
              {showClient && (
                <th scope="col" className="p-3">
                  Cliente
                </th>
              )}
              <th scope="col" className="p-3">
                Detalhes
              </th>
              <th scope="col" className="p-3">
                Estado
              </th>
              <th scope="col" className="p-3">
                Ações
              </th>
            </tr>
          </thead>
          <tbody>
            {sessions.map((session) => {
              const details = [session.session_type, session.location].filter(
                (value): value is string => value !== null && value.trim() !== ''
              );
              const pending =
                transition.isPending && transition.variables?.sessionId === session.id;
              const scheduled = session.status === 'scheduled';

              return (
                <tr key={session.id} className="border-border border-t">
                  {showDate && (
                    <td className="p-3 tabular-nums">{formatDate(session.starts_at)}</td>
                  )}
                  <th scope="row" className="font-display p-3 text-left text-lg tabular-nums">
                    {hourOf(session.starts_at)}
                  </th>
                  {showClient && (
                    <td className="p-3 font-medium">
                      <Link
                        to={`/trainer/clients/${session.client_id}`}
                        className="hover:underline"
                      >
                        {session.client_name}
                      </Link>
                    </td>
                  )}
                  <td className="text-muted-foreground p-3">
                    {[...details, `${session.duration_minutes} min`].join(' · ')}
                    {session.client_session_pack_id !== null && (
                      <Badge variant="outline" className="ml-2">
                        Pack
                      </Badge>
                    )}
                  </td>
                  <td className="p-3">
                    <Badge variant={scheduled ? 'secondary' : 'outline'}>
                      {SESSION_STATUS_LABELS[session.status] ?? session.status}
                    </Badge>
                  </td>
                  <td className="flex items-center gap-1 p-2">
                    {scheduled && (
                      <Button
                        size="sm"
                        variant="outline"
                        className="min-h-11 md:min-h-8"
                        disabled={pending}
                        aria-label={`Registar presença de ${session.client_name} às ${hourOf(session.starts_at)}`}
                        onClick={() => void run(session, 'complete')}
                      >
                        {pending ? 'A registar…' : 'Registar presença'}
                      </Button>
                    )}
                    <DropdownMenu>
                      <DropdownMenuTrigger asChild>
                        <Button
                          size="icon-sm"
                          variant="ghost"
                          className="size-11 md:size-8"
                          disabled={pending}
                          aria-label={`Mais ações da sessão de ${session.client_name} às ${hourOf(session.starts_at)}`}
                        >
                          <MoreHorizontal aria-hidden />
                        </Button>
                      </DropdownMenuTrigger>
                      <DropdownMenuContent align="end">
                        {scheduled ? (
                          <>
                            <DropdownMenuItem
                              onSelect={() => setConfirm({ session, transition: 'no-show' })}
                            >
                              Marcar falta
                            </DropdownMenuItem>
                            <DropdownMenuItem onSelect={() => setRescheduling(session)}>
                              Reagendar
                            </DropdownMenuItem>
                            <DropdownMenuItem onSelect={() => setChangingPack(session)}>
                              Trocar pack
                            </DropdownMenuItem>
                            <DropdownMenuSeparator />
                            <DropdownMenuItem
                              onSelect={() =>
                                setConfirm({ session, transition: 'cancel-by-trainer' })
                              }
                            >
                              Cancelar (por ti)
                            </DropdownMenuItem>
                            <DropdownMenuItem
                              onSelect={() =>
                                setConfirm({ session, transition: 'cancel-by-client' })
                              }
                            >
                              Cancelada pelo cliente
                            </DropdownMenuItem>
                          </>
                        ) : (
                          <DropdownMenuItem onSelect={() => void run(session, 'restore')}>
                            Repor como agendada
                          </DropdownMenuItem>
                        )}
                      </DropdownMenuContent>
                    </DropdownMenu>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>

      <ConfirmDialog
        open={confirm !== null}
        onOpenChange={(open) => {
          if (!open) setConfirm(null);
        }}
        title={confirm === null ? '' : CONFIRM_COPY[confirm.transition].title}
        description={
          confirm === null
            ? ''
            : CONFIRM_COPY[confirm.transition].description(confirm.session.client_name)
        }
        confirmLabel={confirm === null ? '' : CONFIRM_COPY[confirm.transition].label}
        destructive={confirm?.transition !== 'no-show'}
        pending={transition.isPending}
        onConfirm={() => {
          if (confirm !== null) void run(confirm.session, confirm.transition);
        }}
      />

      <Sheet
        open={rescheduling !== null}
        onOpenChange={(open) => {
          if (!open) setRescheduling(null);
        }}
      >
        <SheetContent className="w-full sm:max-w-130">
          <SheetHeader>
            <SheetTitle>Reagendar sessão</SheetTitle>
            <SheetDescription>{rescheduling?.client_name}</SheetDescription>
          </SheetHeader>
          {rescheduling !== null && (
            <div className="min-h-0 flex-1 overflow-y-auto px-4">
              <RescheduleForm session={rescheduling} onSaved={() => setRescheduling(null)} />
            </div>
          )}
        </SheetContent>
      </Sheet>

      <SessionPackDialog
        key={changingPack?.id ?? 'closed'}
        session={changingPack}
        onClose={() => setChangingPack(null)}
      />
    </>
  );
}
