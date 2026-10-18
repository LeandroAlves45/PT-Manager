import { Navigate, useLocation } from 'react-router';

import { AUTH_ROUTES } from '@/shared/config/auth-routes';

/**
 * Redireciona `/auth` e `/auth/login` para `/entrar`.
 *
 * Preserva `state`: um favorito antigo com `state.from` (ou um aviso) não pode perder o
 * destino de regresso.
 */
export function LegacyLoginRedirect() {
  const state: unknown = useLocation().state;

  return <Navigate to={AUTH_ROUTES.login} replace state={state} />;
}
