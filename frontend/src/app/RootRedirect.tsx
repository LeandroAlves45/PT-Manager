import { Navigate } from 'react-router';

import { useAuth } from '@/app/providers/useAuth';
import { homeRouteFor } from '@/shared/api/session';
import { FullPageSpinner } from '@/shared/components/FullPageSpinner';
import { AUTH_ROUTES } from '@/shared/config/auth-routes';

/**
 * Decide o destino da raiz `/`.
 *
 * Só depois de o arranque `csrf → refresh` terminar: redireccionar antes disso mandaria
 * para o login quem tem sessão válida.
 */
export function RootRedirect() {
  const { status, session } = useAuth();

  if (status === 'restoring') return <FullPageSpinner label="A restaurar sessão..." />;

  if (session === null) return <Navigate to={AUTH_ROUTES.login} replace />;

  return <Navigate to={homeRouteFor(session.role)} replace />;
}
