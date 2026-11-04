import { Navigate, Outlet } from 'react-router';

import { useAuth } from '@/app/providers/useAuth';
import { homeRouteFor, type AppRole } from '@/shared/api/session';
import { FullPageSpinner } from '@/shared/components/FullPageSpinner';
import { AUTH_ROUTES } from '@/shared/config/auth-routes';
import { NativeClientOnlyNotice } from '@/shared/native/NativeClientOnlyNotice';
import { isNativeApp } from '@/shared/native/platform';

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

  if (session === null) return <Navigate to={AUTH_ROUTES.login} replace />;

  if (session.role !== role) return <Navigate to={homeRouteFor(session.role)} replace />;

  // A app v1 é só para clientes: personal trainers e superuser usam a web (sem subscrição
  // nem pagamentos dentro da app, por causa das regras das lojas).
  if (isNativeApp() && role !== 'client') return <NativeClientOnlyNotice />;

  return <Outlet />;
}
