import { createBrowserRouter, type RouteObject } from 'react-router';

import { AppRouteRoot } from '@/app/AppRouteRoot';
import { LegacyLoginRedirect } from '@/app/LegacyLoginRedirect';
import { NotFoundPage } from '@/app/NotFoundPage';
import { RequireAuth } from '@/app/guards/RequireAuth';
import { RequireRole } from '@/app/guards/RequireRole';
import { AppShell } from '@/app/layouts/AppShell';
import { AuthLayout } from '@/app/layouts/AuthLayout';
import { PortalLayout } from '@/app/layouts/PortalLayout';
import { AccountSecurityPage } from '@/features/account';
import {
  AcceptInvitePage,
  ConfirmEmailPage,
  ForgotPasswordPage,
  LoginPage,
  ResendConfirmationPage,
  ResetPasswordPage,
  SignUpPage,
} from '@/features/auth';
import { BillingPage } from '@/features/billing/pages/BillingPage';
import { CheckInsPage } from '@/features/check-ins/pages/CheckInsPage';
import { AdminOverviewPage } from '@/features/admin-overview/pages/AdminOverviewPage';
import { CatalogPage } from '@/features/admin-catalog/pages/CatalogPage';
import { ModerationPage } from '@/features/admin-moderation/pages/ModerationPage';
import { ClientDetailPage } from '@/features/clients/pages/ClientDetailPage';
import { ClientsPage } from '@/features/clients/pages/ClientsPage';
import {
  PortalCheckInsPage,
  PortalHomePage,
  PortalNutritionPage,
  PortalPlanPage,
  PortalProfilePage,
  PortalSupplementsPage,
  PortalTodayPage,
} from '@/features/portal';
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
import { AUTH_ROUTES, LEGACY_LOGIN_ROUTES } from '@/shared/config/auth-routes';

/**
 * Rotas da aplicação.
 */

const applicationRoutes: RouteObject[] = [
  { path: '/', element: <RootRedirect /> },

  // Páginas públicas de autenticação, todas sob a mesma moldura. `/aceitar-convite` fica aqui
  // e não atrás do `RequireAuth`: tem de funcionar com e sem sessão.
  {
    element: <AuthLayout />,
    children: [
      { path: AUTH_ROUTES.login, element: <LoginPage /> },
      { path: AUTH_ROUTES.signUp, element: <SignUpPage /> },
      { path: AUTH_ROUTES.forgotPassword, element: <ForgotPasswordPage /> },
      { path: AUTH_ROUTES.resetPassword, element: <ResetPasswordPage /> },
      { path: AUTH_ROUTES.confirmEmail, element: <ConfirmEmailPage /> },
      { path: AUTH_ROUTES.acceptInvite, element: <AcceptInvitePage /> },
      { path: AUTH_ROUTES.resendConfirmation, element: <ResendConfirmationPage /> },
    ],
  },

  ...LEGACY_LOGIN_ROUTES.map((path): RouteObject => ({ path, element: <LegacyLoginRedirect /> })),

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
              { path: 'account', element: <AccountSecurityPage /> },
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
              { path: 'account', element: <AccountSecurityPage /> },
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
              { index: true, element: <PortalHomePage /> },
              { path: 'today', element: <PortalTodayPage /> },
              { path: 'plan', element: <PortalPlanPage /> },
              { path: 'nutrition', element: <PortalNutritionPage /> },
              { path: 'supplements', element: <PortalSupplementsPage /> },
              { path: 'check-ins', element: <PortalCheckInsPage /> },
              { path: 'profile', element: <PortalProfilePage /> },
              { path: 'account', element: <AccountSecurityPage /> },
            ],
          },
        ],
      },
    ],
  },

  // Qualquer outra rota mostra a página 404, que funciona com e sem sessão.
  { path: '*', element: <NotFoundPage /> },
];

export const routes: RouteObject[] = [
  {
    element: <AppRouteRoot />,
    children: applicationRoutes,
  },
];

export const router = createBrowserRouter(routes);
