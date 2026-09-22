/**
 * Falha alto e cedo quando uma pré-condição do código é violada.
 *
 * Usar só para erros de programação (um provider em falta, um contexto consumido fora do
 * seu `Provider`). Erros esperados da API são `ApiProblem`, não excepções.
 *
 * @param condition Condição que tem de ser verdadeira.
 * @param message Mensagem mostrada ao programador quando falha.
 */
export function invariant(condition: unknown, message: string): asserts condition {
  if (!condition) {
    throw new Error(`[pt-manager] ${message}`);
  }
}
