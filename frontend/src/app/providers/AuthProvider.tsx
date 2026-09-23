import { useQueryClient } from '@tanstack/react-query';
import {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
  useSyncExternalStore,
  type ReactNode,
} from 'react';

import { AuthContext, type AuthStatus } from '@/app/providers/auth-context';
import { apiClient, refreshSession } from '@/shared/api/client';
import { clearSession, getSession, subscribe } from '@/shared/api/session';
import {
  publishSessionInvalidated,
  subscribeSessionInvalidated,
} from '@/shared/api/session-events';

/**
 * Restaura a sessão no arranque e expõe-na à aplicação.
 *
 * Sequência obrigatória:
 * 1. `POST /auth/csrf` — o cookie de refresh já vai no pedido e devolve um token CSRF;
 * 2. `POST /auth/refresh` com esse token — devolve a sessão e roda o cookie.
 *
 * Sem sessão válida o backend responde 401 e a aplicação fica anónima, sem erro visível:
 * abrir a aplicação pela primeira vez não é uma falha.
 *
 * Só existe um estado próprio — se o arranque já terminou. O `status` é derivado dele e
 * da sessão, para que um refresh falhado a meio da navegação passe a anónimo sem precisar
 * de mais um efeito a sincronizar estado com estado.
 */
export function AuthProvider({ children }: { children: ReactNode }) {
  const [restored, setRestored] = useState(false);
  const queryClient = useQueryClient();
  const previousUserId = useRef<string | null>(null);

  const session = useSyncExternalStore(subscribe, getSession, () => null);

  useEffect(() => {
    let cancelled = false;

    async function restore(): Promise<void> {
      try {
        // `refreshSession` deduplica chamadas do StrictMode e protege `csrf → refresh`
        // com o mesmo lock entre separadores.
        await refreshSession();
      } catch {
        // Uma indisponibilidade transitória não transforma uma sessão em inválida. O
        // arranque termina anónimo e o próximo pedido pode voltar a tentar o refresh.
      } finally {
        if (!cancelled) setRestored(true);
      }
    }

    void restore();

    return () => {
      cancelled = true;
    };
  }, []);

  useEffect(() => {
    const nextUserId = session?.userId ?? null;
    const userChanged = previousUserId.current !== null && previousUserId.current !== nextUserId;

    if (userChanged) queryClient.clear();

    previousUserId.current = nextUserId;
  }, [queryClient, session?.userId]);

  useEffect(
    () =>
      subscribeSessionInvalidated(() => {
        queryClient.clear();
        clearSession();
      }),
    [queryClient]
  );

  const status: AuthStatus = !restored
    ? 'restoring'
    : session === null
      ? 'anonymous'
      : 'authenticated';

  const signOut = useCallback(async () => {
    // O logout pode falhar (rede, sessão já revogada). A sessão local é limpa na mesma:
    // manter o utilizador "dentro" da aplicação depois de ele pedir para sair é pior.
    try {
      await apiClient.POST('/api/v1/auth/logout');
    } finally {
      queryClient.clear();
      clearSession();
      publishSessionInvalidated();
    }
  }, [queryClient]);

  const value = useMemo(() => ({ status, session, signOut }), [status, session, signOut]);

  return <AuthContext value={value}>{children}</AuthContext>;
}
