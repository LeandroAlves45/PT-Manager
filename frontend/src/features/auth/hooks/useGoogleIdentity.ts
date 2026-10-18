import { useCallback, useEffect, useRef, useState } from 'react';

import { useTheme } from '@/app/providers/useTheme';
import { loadGoogleIdentity } from '@/features/auth/lib/google-identity';
import { authErrorMessage } from '@/features/auth/lib/auth-errors';
import { apiClient, unwrap } from '@/shared/api/client';
import { isApiProblem } from '@/shared/api/problem';
import { getGoogleClientId } from '@/shared/config/env';

/** Para que serve o botão: entrar/criar conta (anónimo) ou ligar o Google a uma conta com sessão. */
export type GooglePurpose = 'sign-in' | 'link';

export interface GoogleIdentity {
  /** `false` quando o ambiente não tem cliente Google: o ecrã não mostra nada. */
  readonly available: boolean;
  /**
   * Ref (de função) do contentor onde o Google desenha o botão. É de função e não um objeto
   * `useRef` para o efeito reagir ao contentor montar, e para o hook poder devolver um objeto
   * sem o compilador do React o tratar como acesso a refs durante o render.
   */
  readonly attachContainer: (node: HTMLDivElement | null) => void;
  /** Mensagem PT-PT de uma falha a preparar o botão ou `null`. */
  readonly error: string | null;
  /** Pede um challenge novo e redesenha o botão. Obrigatório depois de qualquer tentativa. */
  readonly reset: () => void;
}

const SCRIPT_ERROR = 'Não foi possível carregar o início de sessão com Google.';

/** Largura do botão: a do contentor, dentro dos limites que a Google aceita (200 a 400 px). */
function buttonWidth(container: HTMLElement): number {
  return Math.min(400, Math.max(200, container.clientWidth || 320));
}

/**
 * Liga um botão do Google Identity Services ao challenge de segurança do backend.
 *
 * Sequência: o backend emite um `nonce` e guarda-o num cookie HttpOnly; o GIS põe esse `nonce`
 * no id_token; o backend compara os dois. O challenge é de **uso único** — depois de qualquer
 * tentativa, bem ou mal sucedida, o `nonce` anterior morreu. Por isso o hook pede sozinho um
 * desafio novo (`reset`) quando `onCredential` termina, falhe ou não.
 *
 * Sem `VITE_GOOGLE_CLIENT_ID` o hook não faz nenhum pedido nem carrega nenhum script.
 *
 * @param purpose `sign-in` (anónimo) ou `link` (conta autenticada).
 * @param onCredential Recebe o `id_token` quando o utilizador escolhe uma conta. Pode devolver
 *   uma promessa: o novo desafio só é pedido depois de ela terminar.
 */
export function useGoogleIdentity({
  purpose,
  onCredential,
}: {
  purpose: GooglePurpose;
  onCredential: (id_token: string) => void | Promise<void>;
}): GoogleIdentity {
  const clientId = getGoogleClientId();
  const { resolved } = useTheme();
  const [container, setContainer] = useState<HTMLDivElement | null>(null);
  const [attempt, setAttempt] = useState(0);
  const [error, setError] = useState<string | null>(null);

  // O callback do GIS é registado uma vez por challenge; a ref garante que chama sempre a
  // versão mais recente de `onCredential` (que lê o estado atual do formulário).
  const callbackRef = useRef(onCredential);
  useEffect(() => {
    callbackRef.current = onCredential;
  }, [onCredential]);

  const reset = useCallback(() => {
    setError(null);
    setAttempt((value) => value + 1);
  }, []);

  useEffect(() => {
    if (clientId === null || container === null) return;

    let cancelled = false;
    // Cancelar o pedido evita que um challenge antigo, que chegue depois do novo, deixe no
    // cookie HttpOnly um nonce diferente do que o GIS vai usar.
    const controller = new AbortController();

    async function prepare(id: string, node: HTMLDivElement): Promise<void> {
      try {
        const challenge =
          purpose === 'sign-in'
            ? unwrap(
                await apiClient.POST('/api/v1/auth/google/challenge', { signal: controller.signal })
              )
            : unwrap(
                await apiClient.POST('/api/v1/auth/google/link/challenge', {
                  signal: controller.signal,
                })
              );
        const google = await loadGoogleIdentity();
        if (cancelled) return;

        google.initialize({
          client_id: id,
          nonce: challenge.nonce,
          auto_select: false,
          callback: (response) => {
            // O erro do pedido é do ecrã, que já mostra; aqui só importa se terminou.
            void Promise.resolve()
              .then(() => callbackRef.current(response.credential))
              .catch(() => undefined)
              .finally(reset);
          },
        });

        node.replaceChildren();
        google.renderButton(node, {
          type: 'standard',
          theme: resolved === 'dark' ? 'filled_black' : 'outline',
          size: 'large',
          text: purpose === 'sign-in' ? 'continue_with' : 'signin_with',
          shape: 'rectangular',
          logo_alignment: 'left',
          width: buttonWidth(node),
          locale: 'pt-PT',
        });
      } catch (failure) {
        if (cancelled) return;
        // Falha do pedido (429, 503): mensagem do mapa. Falha do script (rede, bloqueador de
        // anúncios): o resto da página continua a funcionar, só o botão fica indisponível.
        setError(isApiProblem(failure) ? authErrorMessage(failure) : SCRIPT_ERROR);
      }
    }

    void prepare(clientId, container);

    return () => {
      cancelled = true;
      controller.abort();
    };
  }, [clientId, container, purpose, resolved, attempt, reset]);

  return { available: clientId !== null, attachContainer: setContainer, error, reset };
}
