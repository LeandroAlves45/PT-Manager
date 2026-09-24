import { createBrowserRouter, Navigate, type RouteObject } from 'react-router';

import { AppRouteRoot } from '@/app/AppRouteRoot';
import { RequireAuth } from '@/app/guards/RequireAuth';
import { RequireRole } from '@/app/guards/RequireRole';
import { AppShell } from '@/app/layouts/AppShell';
import { AuthLayout } from '@/app/layouts/AuthLayout';
import { PortalLayout } from '@/app/layouts/PortalLayout';
import { LoginPage } from '@/features/auth/pages/LoginPage';
import { AdminOverviewPage } from '@/features/admin-overview/pages/AdminOverviewPage';
import { CatalogPage } from '@/features/admin-catalog/pages/CatalogPage';
import { ModerationPage } from '@/features/admin-moderation/pages/ModerationPage';
import { RootRedirect } from '@/app/RootRedirect';
import { PhasePlaceholderPage } from '@/shared/components/PhasePlaceholderPage';

/**
 * Rotas da aplicação.
 */

function placeholder(title: string, phase: string): RouteObject['element'] {
  return <PhasePlaceholderPage title={title} phase={phase} />;
}

const applicationRoutes: RouteObject[] = [
  { path: '/', element: <RootRedirect /> },

  {
    path: '/auth',
    element: <AuthLayout />,
    children: [
      { index: true, element: <Navigate to="/auth/login" replace /> },
      { path: 'login', element: <LoginPage /> },
    ],
  },

  {
    element: <RequireAuth />,
    children: [
      {
        element: <RequireRole role="superuser" />,
        children: [
          {
            path: '/admin',
            element: <AppShell />,
            children: [
              { index: true, element: <AdminOverviewPage /> },
              { path: 'moderation', element: <ModerationPage /> },
              { path: 'catalog/foods', element: <CatalogPage kind="foods" /> },
              { path: 'catalog/exercises', element: <CatalogPage kind="exercises" /> },
              { path: 'catalog/supplements', element: <CatalogPage kind="supplements" /> },
            ],
          },
        ],
      },

      {
        element: <RequireRole role="trainer" />,
        children: [
          {
            path: '/trainer',
            element: <AppShell />,
            children: [
              { index: true, element: placeholder('Painel', '6E') },
              { path: 'clients', element: placeholder('Clientes', '6E') },
              { path: 'clients/:clientId', element: placeholder('Cliente', '6E') },
              { path: 'sessions', element: placeholder('Sessões e packs', '6E') },
              { path: 'check-ins', element: placeholder('Check-ins', '6E') },
              { path: 'training-plans', element: placeholder('Planos de treino', '6E') },
              { path: 'meal-plans', element: placeholder('Planos alimentares', '6E') },
              { path: 'library', element: placeholder('Biblioteca', '6E') },
              { path: 'settings', element: placeholder('Marca própria', '6E') },
              { path: 'billing', element: placeholder('Subscrição', '6E') },
            ],
          },
        ],
      },

      {
        element: <RequireRole role="client" />,
        children: [
          {
            path: '/portal',
            element: <PortalLayout />,
            children: [
              { index: true, element: <Navigate to="/portal/today" replace /> },
              { path: 'today', element: placeholder('Treino de hoje', '6F') },
              { path: 'nutrition', element: placeholder('Nutrição', '6F') },
              { path: 'supplements', element: placeholder('Suplementos', '6F') },
              { path: 'check-ins', element: placeholder('Check-ins', '6F') },
              { path: 'profile', element: placeholder('Perfil', '6F') },
            ],
          },
        ],
      },
    ],
  },

  // Qualquer outra rota volta á raiz, que decide o destino conforme a sessão.
  { path: '*', element: <Navigate to="/" replace /> },
];

export const routes: RouteObject[] = [
  {
    element: <AppRouteRoot />,
    children: applicationRoutes,
  },
];

export const router = createBrowserRouter(routes);
