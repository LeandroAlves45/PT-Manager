import type { ClientChoice } from '@/features/clients';
import { SessionForm } from '@/features/sessions/components/SessionForm';
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetHeader,
  SheetTitle,
} from '@/shared/components/ui/sheet';

/**
 * Painel lateral "Marcar sessão", usado pela agenda e pela tab do cliente.
 *
 * O formulário só monta com o painel aberto: cada abertura começa limpa.
 */
export function NewSessionSheet({
  open,
  onOpenChange,
  client,
  defaultDate,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  client: ClientChoice | null;
  defaultDate?: string;
}) {
  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent className="w-full sm:max-w-130">
        <SheetHeader>
          <SheetTitle>Marcar sessão</SheetTitle>
          <SheetDescription>
            {client === null
              ? 'Escolhe o cliente, o dia e a hora.'
              : `Com ${client.name}. Escolhe o dia e a hora.`}
          </SheetDescription>
        </SheetHeader>
        {open && (
          <div className="min-h-0 flex-1 overflow-y-auto px-4">
            <SessionForm
              client={client}
              {...(defaultDate === undefined ? {} : { defaultDate })}
              onSaved={() => onOpenChange(false)}
            />
          </div>
        )}
      </SheetContent>
    </Sheet>
  );
}
