import { createContext } from 'react';

import type { Session } from '@/shared/api/session';

/**
 * Estado do arranque da sessão.
 *
 * `restoring` existe para os guards não mandarem ninguém para o login enquanto o
 * `csrf → refresh` do arranque ainda está a decorrer.
 */
export type AuthStatus = 'restoring' | 'authenticated' | 'anonymous';

export interface AuthContextValue {
  readonly status: AuthStatus;
  readonly session: Session | null;
  /** Termina a sessão no servidor e limpa a memória. */
  readonly signOut: () => Promise<void>;
}

export const AuthContext = createContext<AuthContextValue | null>(null);
