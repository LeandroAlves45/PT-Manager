import { Check, Loader2 } from 'lucide-react';
import { toast } from 'sonner';

import { useToggleIntakeMutation } from '@/features/portal/api/portal';
import type { components } from '@/shared/api/schema';
import { isApiProblem } from '@/shared/api/problem';
import { Badge } from '@/shared/components/ui/badge';
import { Button } from '@/shared/components/ui/button';

type IntakeItem = components['schemas']['MyTodaySupplementIntakeItemResponse'];
type Assignment = components['schemas']['MySupplementAssignmentResponse'];

/** Códigos confirmados em `MarkMySupplementIntakeHandler` e `UnmarkMySupplementIntakeHandler`. */
const INTAKE_ERROR_MESSAGES: Record<string, string> = {
  supplement_assignment_inactive: 'Este suplemento já não te está atribuído.',
  supplement_assignment_not_found: 'Este suplemento já não existe na tua lista.',
};

/** Mensagem PT para a falha marcar ou desmarcar uma toma; genérica quando desconhecida. */
function intakeFailureMessage(failure: unknown): string {
  const code = isApiProblem(failure) ? (failure.fieldErrors[0]?.code ?? failure.code) : null;

  return (
    (code !== null && Object.hasOwn(INTAKE_ERROR_MESSAGES, code)
      ? INTAKE_ERROR_MESSAGES[code]
      : undefined) ?? 'Não foi possível guardar a toma do suplemento. Tenta novamente.'
  );
}

/**
 * Um suplemento ativo com a toma de hoje: dose, quando tomar e o botão de estado (44 px).
 *
 * O botão espera pelo servidor: fica desativado enquanto o pedido e a releitura
 * correm, e o visto só muda com os dados novos. Cada linha tem a sua mutation, por isso um
 * pedido lento não bloqueia as outras. A falha vai para um toast e não para a linha: depois
 * de um 409 a atribuição sai da lista e a linha deixa de existir.
 *
 * @param item Toma de hoje (`GET /portal/my-supplements/intakes/today`).
 * @param detail Atribuição com as notas do personal trainer; `undefined` enquanto a lista de
 *   detalhes carrega ou quando falhou. A linha funciona sem ela.
 */
export function SupplementIntakeRow({
  item,
  detail,
}: {
  item: IntakeItem;
  detail: Assignment | undefined;
}) {
  const toggle = useToggleIntakeMutation();

  return (
    <li className="border-border bg-card space-y-2 rounded-lg border p-3">
      <div className="flex items-start gap-3">
        <div className="min-w-0 flex-1 space-y-0.5">
          <p className="flex flex-wrap items-center gap-2">
            <span className="min-w-0 font-medium wrap-break-word">{item.supplement_name}</span>
            {detail?.is_supplement_archived === true && (
              <Badge variant="secondary">Descontinuado</Badge>
            )}
          </p>
          <p className="text-muted-foreground text-sm wrap-break-word">
            {item.serving_size} · {item.timing}
          </p>
        </div>
        <Button
          type="button"
          size="icon"
          variant={item.is_taken ? 'default' : 'outline'}
          className="size-11 shrink-0"
          aria-label={
            item.is_taken
              ? `Desmarcar toma de ${item.supplement_name}`
              : `Marcar toma de ${item.supplement_name}`
          }
          aria-pressed={item.is_taken}
          disabled={toggle.isPending}
          onClick={() =>
            toggle.mutate(
              { assignmentId: item.assignment_id, taken: !item.is_taken },
              { onError: (failure) => toast.error(intakeFailureMessage(failure)) }
            )
          }
        >
          {toggle.isPending ? (
            <Loader2 aria-hidden className="animate-spin" />
          ) : (
            <Check aria-hidden />
          )}
        </Button>
      </div>

      {detail !== undefined && detail.trainer_notes !== null && (
        <p className="text-sm wrap-break-word">{detail.trainer_notes}</p>
      )}
      {detail !== undefined && detail.supplement_description !== null && (
        <p className="text-muted-foreground text-sm wrap-break-word">
          {detail.supplement_description}
        </p>
      )}
    </li>
  );
}
