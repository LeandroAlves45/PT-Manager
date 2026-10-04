import { ScheduleCheckInForm } from '@/features/check-ins/components/ScheduleCheckInForm';
import type { ClientChoice } from '@/features/clients';
import type { components } from '@/shared/api/schema';
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetHeader,
  SheetTitle,
} from '@/shared/components/ui/sheet';

type CheckIn = components['schemas']['CheckInResponse'];

/**
 * Painel lateral "Agendar check-in" (sem `checkIn`) ou "Reagendar check-in".
 *
 * O formulário só monta com o painel aberto e tem `key` por check-in: cada abertura começa
 * limpa e reagendar outro check-in nunca herda os valores do anterior.
 */
export function ScheduleCheckInSheet({
  open,
  onOpenChange,
  client,
  checkIn,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  client: ClientChoice | null;
  checkIn?: CheckIn;
}) {
  const name = checkIn?.client_name ?? client?.name;

  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent className="w-full sm:max-w-130">
        <SheetHeader>
          <SheetTitle>
            {checkIn === undefined ? 'Agendar check-in' : 'Reagendar check-in'}
          </SheetTitle>
          <SheetDescription>
            {name === undefined
              ? 'Escolhe o cliente e o dia.'
              : `Com ${name}. Escolhe o dia do check-in.`}
          </SheetDescription>
        </SheetHeader>
        {open && (
          <div className="min-h-0 flex-1 overflow-y-auto px-4">
            <ScheduleCheckInForm
              key={checkIn?.id ?? 'new'}
              client={client}
              {...(checkIn === undefined ? {} : { checkIn })}
              onSaved={() => onOpenChange(false)}
            />
          </div>
        )}
      </SheetContent>
    </Sheet>
  );
}
