import { useState } from 'react';

import { CorrectCheckInForm } from '@/features/check-ins/components/CorrectCheckInForm';
import { answerLines, type AnswerLine } from '@/features/check-ins/lib/answers';
import type { components } from '@/shared/api/schema';
import { Button } from '@/shared/components/ui/button';
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetHeader,
  SheetTitle,
} from '@/shared/components/ui/sheet';
import { formatDate, formatDateTime } from '@/shared/lib/format';

type CheckIn = components['schemas']['CheckInResponse'];

/**
 * Painel lateral de um check-in respondido: leitura da resposta e, a pedido, a correção.
 *
 * Abre sempre em leitura; "Corrigir" troca para o formulário. O formulário só monta com o
 * painel aberto, por isso cada abertura começa limpa.
 *
 * @param checkIn Check-in respondido, ou `null` com o painel fechado.
 * @param onClose Fecha o painel.
 */
export function CheckInDetailSheet({
  checkIn,
  onClose,
}: {
  checkIn: CheckIn | null;
  onClose: () => void;
}) {
  const [correcting, setCorrecting] = useState(false);

  function close() {
    setCorrecting(false);
    onClose();
  }

  return (
    <Sheet
      open={checkIn !== null}
      onOpenChange={(open) => {
        if (!open) close();
      }}
    >
      <SheetContent className="w-full sm:max-w-160">
        {checkIn !== null && (
          <>
            <SheetHeader>
              <SheetTitle>
                {correcting ? 'Corrigir check-in' : 'Check-in'} de {checkIn.client_name}
              </SheetTitle>
              <SheetDescription>
                Dia {formatDate(checkIn.check_in_date)}
                {checkIn.responded_at !== null &&
                  ` · respondido a ${formatDateTime(checkIn.responded_at)}`}
                {checkIn.reviewed_at !== null &&
                  ` · revisto a ${formatDateTime(checkIn.reviewed_at)}`}
              </SheetDescription>
            </SheetHeader>
            <div className="min-h-0 flex-1 overflow-y-auto px-4">
              {correcting ? (
                <CorrectCheckInForm checkIn={checkIn} onSaved={close} />
              ) : (
                <AnswerView checkIn={checkIn} onCorrect={() => setCorrecting(true)} />
              )}
            </div>
          </>
        )}
      </SheetContent>
    </Sheet>
  );
}

function AnswerView({ checkIn, onCorrect }: { checkIn: CheckIn; onCorrect: () => void }) {
  const { metrics, measurements, feedback } = answerLines(checkIn);

  return (
    <div className="space-y-6 pb-4">
      <AnswerGroup title="Valores" lines={metrics} columns />
      {measurements.length > 0 && <AnswerGroup title="Medidas (cm)" lines={measurements} columns />}
      {feedback.length > 0 && <AnswerGroup title="Respostas" lines={feedback} />}
      {checkIn.target_date !== null && (
        <p className="text-muted-foreground text-sm">
          Data-alvo: {formatDate(checkIn.target_date)}
        </p>
      )}
      <div className="border-border flex justify-end border-t pt-4">
        <Button variant="outline" onClick={onCorrect}>
          Corrigir valores
        </Button>
      </div>
    </div>
  );
}

function AnswerGroup({
  title,
  lines,
  columns = false,
}: {
  title: string;
  lines: readonly AnswerLine[];
  columns?: boolean;
}) {
  return (
    <section className="space-y-2">
      <h3 className="font-medium">{title}</h3>
      <dl className={columns ? 'grid gap-3 sm:grid-cols-2' : 'space-y-3'}>
        {lines.map((line) => (
          <div key={line.label}>
            <dt className="text-muted-foreground text-xs">{line.label}</dt>
            <dd className="text-sm whitespace-pre-line">{line.value}</dd>
          </div>
        ))}
      </dl>
    </section>
  );
}
