import { confirmEmail } from '@/features/auth/api/auth';
import { isApiProblem, type ApiProblem } from '@/shared/api/problem';

/** Resultado de confirmar o email: sucesso, ou o problema devolvido pelo servidor. */
export type ConfirmEmailOutcome = { ok: true } | { ok: false; problem: ApiProblem };

/**
 * Pedidos de confirmação já feitos, por token.
 *
 * O token é de uso único: o StrictMode monta o efeito duas vezes e o segundo pedido
 * receberia `consumed` depois de o primeiro ter confirmado. Partilhar a mesma promessa por
 * token faz as duas montagens verem o mesmo resultado, com um único pedido.
 */
const requests = new Map<string, Promise<ConfirmEmailOutcome>>();

/**
 * Confirma o email uma só vez por token.
 *
 * Em falha de rede a promessa rejeita e a entrada sai do mapa, para o utilizador poder
 * tentar de novo.
 */
export function confirmEmailOnce(token: string): Promise<ConfirmEmailOutcome> {
  let request = requests.get(token);

  if (request === undefined) {
    request = confirmEmail(token).then(
      (): ConfirmEmailOutcome => ({ ok: true }),
      (failure: unknown): ConfirmEmailOutcome => {
        if (!isApiProblem(failure)) {
          requests.delete(token);
          throw failure;
        }
        return { ok: false, problem: failure };
      }
    );
    requests.set(token, request);
  }

  return request;
}

/** Esquece os pedidos feitos. Só para testes: o mapa vive no módulo e sobrevive entre testes. */
export function resetConfirmEmailRequests(): void {
  requests.clear();
}
