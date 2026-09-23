import { Copy, TriangleAlert } from 'lucide-react';
import { useState } from 'react';

import { isApiProblem } from '@/shared/api/problem';
import { Button } from '@/shared/components/ui/button';

const PROBLEM_MESSAGES: Readonly<Record<string, string>> = {
  authentication_invalid_credentials: 'O email ou a palavra-passe não estão corretos.',
  authentication_refresh_token_required: 'A sessão terminou. Inicia sessão novamente.',
  authentication_csrf_token_invalid: 'A sessão mudou. Tenta novamente.',
  subscription_required: 'Esta funcionalidade exige uma subscrição ativa.',
  rate_limit_exceeded: 'Foram feitos demasiados pedidos. Aguarda um momento e tenta novamente.',
};

function messageFor(error: unknown): string {
  const generic = 'Não conseguimos completar a operação. Os dados continuam aqui. Tenta novamente.';

  if (!isApiProblem(error)) return generic;

  // `title` é o código estável do contrato. `detail` só é fallback porque pode ser
  // alterado no backend e não é necessariamente texto de produto em PT-PT.
  const translated = PROBLEM_MESSAGES[error.code];
  if (translated !== undefined) return translated;

  const fallback = error.message.trim();
  return fallback.length > 0 && fallback.length <= 240 ? fallback : generic;
}

/**
 * Bloco de erro inesperado.
 *
 * Decisões do desenho:
 * - sem código HTTP visível: "erro 500" não diz nada a um personal trainer;
 * - o `correlation_id` é copiável, porque é o que liga o ecrã aos logs do servidor;
 * - "Reportar" não existe — não há endpoint de report;
 * - `aria-live="assertive"` porque o utilizador tem de saber que a acção falhou.
 *
 * Erros de validação nunca chegam aqui: vão para os campos do formulário.
 */
export function ErrorState({
  title = 'Não foi possível carregar.',
  error,
  onRetry,
}: {
  title?: string;
  error: unknown;
  onRetry?: () => void;
}) {
  const [copied, setCopied] = useState(false);
  const correlationId = isApiProblem(error) ? error.correlationId : null;
  const detail = messageFor(error);

  async function copyCorrelationId(): Promise<void> {
    if (correlationId === null) return;

    try {
      await navigator.clipboard.writeText(correlationId);
      setCopied(true);
    } catch {
      // Sem permissão ou fora de contexto seguro: o id continua visível para copiar à mão.
    }
  }

  return (
    <div
      role="alert"
      aria-live="assertive"
      className="border-destructive/40 bg-card flex flex-col gap-3 rounded-xl border p-6"
    >
      <div className="flex items-center gap-2">
        <TriangleAlert aria-hidden className="text-destructive size-5" />
        <h2 className="font-display text-xl">{title}</h2>
      </div>

      <p className="text-muted-foreground text-sm">{detail}</p>

      <div className="flex flex-wrap items-center gap-2 pt-1">
        {onRetry !== undefined && (
          <Button onClick={onRetry} size="sm">
            Tentar novamente
          </Button>
        )}

        {correlationId !== null && (
          <Button variant="outline" size="sm" onClick={() => void copyCorrelationId()}>
            <Copy aria-hidden />
            {copied ? 'Id copiado' : 'Copiar id'}
          </Button>
        )}
      </div>

      {correlationId !== null && (
        <p className="tabular text-muted-foreground text-xs">{correlationId}</p>
      )}
    </div>
  );
}
