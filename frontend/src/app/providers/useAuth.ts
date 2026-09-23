import { useContext } from 'react';

import { AuthContext, type AuthContextValue } from '@/app/providers/auth-context';
import { invariant } from '@/shared/lib/invariant';

/** Lê a sessão atual. Só funciona dentro de <AuthProvider>. */
export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext);
  invariant(context, 'useAuth foi usado fora de <AuthProvider>.');
  return context;
}
