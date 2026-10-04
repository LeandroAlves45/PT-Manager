import { createBrowserRouter, Navigate, type RouteObject } from 'react-router';

import { AppRouteRoot } from '@/app/AppRouteRoot';
import { RequireAuth } from '@/app/guards/RequireAuth';
import { RequireRole } from '@/app/guards/RequireRole';
import { AppShell } from '@/app/layouts/AppShell';
import { AuthLayout } from '@/app/layouts/AuthLayout';
import { PortalLayout } from '@/app/layouts/PortalLayout';
import { LoginPage } from '@/features/auth/pages/LoginPage';
import { BillingPage } from '@/features/billing/pages/BillingPage';
import { CheckInsPage } from '@/features/check-ins/pages/CheckInsPage';
import { AdminOverviewPage } from '@/features/admin-overview/pages/AdminOverviewPage';
import { CatalogPage } from '@/features/admin-catalog/pages/CatalogPage';
import { ModerationPage } from '@/features/admin-moderation/pages/ModerationPage';
import { ClientDetailPage } from '@/features/clients/pages/ClientDetailPage';
import { ClientsPage } from '@/features/clients/pages/ClientsPage';
import { LibraryPage } from '@/features/library/pages/LibraryPage';
import {
  MealPlansPage,
  SupplementAssignmentsPage,
  TrainingPlansPage,
} from '@/features/prescriptions';
import { SessionsPage } from '@/features/sessions/pages/SessionsPage';
import { SettingsPage } from '@/features/trainer-settings/pages/SettingsPage';
import { TrainerDashboardPage } from '@/features/trainer-dashboard/pages/TrainerDashboardPage';
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
              { index: true, element: <TrainerDashboardPage /> },
              { path: 'clients', element: <ClientsPage /> },
              { path: 'clients/:clientId', element: <ClientDetailPage /> },
              { path: 'sessions', element: <SessionsPage /> },
              { path: 'check-ins', element: <CheckInsPage /> },
              { path: 'training-plans', element: <TrainingPlansPage /> },
              { path: 'meal-plans', element: <MealPlansPage /> },
              { path: 'supplement-assignments', element: <SupplementAssignmentsPage /> },
              { path: 'library', element: <LibraryPage /> },
              { path: 'settings', element: <SettingsPage /> },
              { path: 'billing', element: <BillingPage /> },
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
