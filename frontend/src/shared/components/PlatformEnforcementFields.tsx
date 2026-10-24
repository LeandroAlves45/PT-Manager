import { useId } from 'react';

import { Label } from '@/shared/components/ui/label';
import { Textarea } from '@/shared/components/ui/textarea';
import {
  PLATFORM_ENFORCEMENT_NOTE_MAX,
  PLATFORM_ENFORCEMENT_REASONS,
  type PlatformEnforcementReason,
} from '@/shared/lib/platformEnforcement';

/**
 * Campos de uma decisão de bloqueio do superuser: um dos motivos (obrigatório) e uma nota
 * livre opcional, com contador e o máximo que o servidor aceita.
 *
 * Controlado de fora: quem o usa guarda `reason` e `note` e decide quando o botão de
 * confirmar fica ativo (sem motivo, nunca).
 *
 * @param reason Motivo escolhido, ou `''` quando ainda não há.
 * @param note Texto da nota.
 * @param onReasonChange Recebe o motivo escolhido.
 * @param onNoteChange Recebe o novo texto da nota.
 * @param disabled Desativa os campos enquanto o pedido corre.
 */
export function PlatformEnforcementFields({
  reason,
  note,
  onReasonChange,
  onNoteChange,
  disabled = false,
}: {
  reason: PlatformEnforcementReason | '';
  note: string;
  onReasonChange: (reason: PlatformEnforcementReason) => void;
  onNoteChange: (note: string) => void;
  disabled?: boolean;
}) {
  const id = useId();
  const noteId = `${id}-note`;
  const counterId = `${id}-counter`;

  return (
    <div className="space-y-4">
      <fieldset className="space-y-2" disabled={disabled}>
        <legend className="text-sm font-medium">Motivo obrigatório</legend>
        {Object.entries(PLATFORM_ENFORCEMENT_REASONS).map(([code, label]) => (
          <label key={code} className="flex min-h-11 items-center gap-2 text-sm md:min-h-8">
            <input
              type="radio"
              name={`${id}-reason`}
              value={code}
              checked={reason === code}
              onChange={() => onReasonChange(code as PlatformEnforcementReason)}
            />
            {label}
          </label>
        ))}
      </fieldset>
      <div className="space-y-1.5">
        <Label htmlFor={noteId}>Nota (opcional)</Label>
        <Textarea
          id={noteId}
          value={note}
          maxLength={PLATFORM_ENFORCEMENT_NOTE_MAX}
          disabled={disabled}
          aria-describedby={counterId}
          onChange={(event) => onNoteChange(event.target.value)}
        />
        <p id={counterId} className="text-muted-foreground text-right text-xs tabular-nums">
          {note.length}/{PLATFORM_ENFORCEMENT_NOTE_MAX}
        </p>
      </div>
    </div>
  );
}
