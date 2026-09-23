import { Navigate, Outlet } from 'react-router';

import { useAuth } from '@/app/providers/useAuth';
import { homeRouteFor, type AppRole } from '@/shared/api/session';
import { FullPageSpinner } from '@/shared/components/FullPageSpinner';

/**
 * Exige um papel concreto.
 *
 * Quem abre a área de outro papel é reencaminhado para a sua própria home, não para um
 * ecrã de erro: a barra de endereço é território do utilizador e enganar-se nela não é
 * uma falha que mereça um 403 na cara.
 */
export function RequireRole({ role }: { role: AppRole }) {
  const { status, session } = useAuth();

  if (status === 'restoring') return <FullPageSpinner label="A restaurar sessão..." />;

  if (session === null) return <Navigate to="/auth/login" replace />;

  if (session.role !== role) return <Navigate to={homeRouteFor(session.role)} replace />;

  return <Outlet />;
}
