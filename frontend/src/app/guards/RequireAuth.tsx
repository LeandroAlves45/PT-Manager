import { Navigate, Outlet, useLocation } from 'react-router';

import { useAuth } from '@/app/providers/useAuth';
import { FullPageSpinner } from '@/shared/components/FullPageSpinner';

/**
 * Exige sessão iniciada.
 *
 * Enquanto o arranque decorre (`csrf → refresh`) mostra o ecrã de carregamento: mandar
 * para o login antes de o refresh responder expulsaria um utilizador que está autenticado.
 * O caminho pedido viaja em `state.from` para o login poder voltar lá depois.
 */
export function RequireAuth() {
  const { status } = useAuth();
  const location = useLocation();

  if (status === 'restoring') return <FullPageSpinner label="A restaurar sessão..." />;

  if (status === 'anonymous') {
    return (
      <Navigate
        to="/auth/login"
        replace
        state={{ from: `${location.pathname}${location.search}` }}
      />
    );
  }

  return <Outlet />;
}
