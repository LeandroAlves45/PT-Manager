import { NuqsAdapter } from 'nuqs/adapters/react-router/v8';
import { Outlet } from 'react-router';

/** Disponibiliza o adapter de query string já dentro do contexto do React Router. */
export function AppRouteRoot() {
  return (
    <NuqsAdapter>
      <Outlet />
    </NuqsAdapter>
  );
}
