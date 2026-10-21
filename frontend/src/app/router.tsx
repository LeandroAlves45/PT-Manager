import type { ComponentType } from 'react';
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
  AcceptTrainerInvitePage,
  ConfirmEmailPage,
  ForgotPasswordPage,
  LoginPage,
  ResendConfirmationPage,
  ResetPasswordPage,
  SignUpPage,
} from '@/features/auth';
import {
  PortalCheckInsPage,
  PortalHomePage,
  PortalNutritionPage,
  PortalPlanPage,
  PortalProfilePage,
  PortalSupplementsPage,
  PortalTodayPage,
} from '@/features/portal';
import { RootRedirect } from '@/app/RootRedirect';
import { RouteFallback } from '@/app/RouteFallback';
import { LibraryKindRedirect, PackTypesRedirect, SetLogsRedirect } from '@/app/TrainerRedirects';
import { AUTH_ROUTES, LEGACY_LOGIN_ROUTES } from '@/shared/config/auth-routes';

/**
 * Rotas da aplicação.
 */

const CATALOG_KINDS = ['foods', 'exercises', 'supplements'] as const;
const LIBRARY_KINDS = ['exercises', 'foods', 'supplements'] as const;

/**
 * Rota lazy (U47, U62): a página só se descarrega quando é aberta, para o bundle de cada papel
 * não carregar as páginas dos outros.
 */
function lazyPage(
  path: string | undefined,
  load: () => Promise<{ default: ComponentType }>
): RouteObject {
  return {
    ...(path === undefined ? { index: true } : { path }),
    lazy: async () => ({ Component: (await load()).default }),
  };
}

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
      { path: AUTH_ROUTES.trainerInvite, element: <AcceptTrainerInvitePage /> },
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
              // U47: as páginas do admin só se descarregam quando o superuser as abre, para
              // não pesarem no bundle do personal trainer e do cliente.
              {
                index: true,
                lazy: async () => ({
                  Component: (await import('@/features/admin-overview/pages/AdminOverviewPage'))
                    .AdminOverviewPage,
                }),
              },
              {
                path: 'moderation',
                lazy: async () => ({
                  Component: (await import('@/features/admin-moderation/pages/ModerationPage'))
                    .ModerationPage,
                }),
              },
              {
                path: 'trainers',
                lazy: async () => ({
                  Component: (await import('@/features/admin-trainers/pages/AdminTrainersPage'))
                    .AdminTrainersPage,
                }),
              },
              {
                path: 'trainers/invite',
                lazy: async () => ({
                  Component: (await import('@/features/admin-trainers/pages/InviteTrainerPage'))
                    .InviteTrainerPage,
                }),
              },
              ...CATALOG_KINDS.flatMap((kind): RouteObject[] => [
                {
                  path: `catalog/${kind}`,
                  lazy: async () => {
                    const { CatalogPage } =
                      await import('@/features/admin-catalog/pages/CatalogPage');
                    return { element: <CatalogPage kind={kind} /> };
                  },
                },
                {
                  path: `catalog/${kind}/new`,
                  lazy: async () => {
                    const { CatalogEditorPage } =
                      await import('@/features/admin-catalog/pages/CatalogEditorPage');
                    return { element: <CatalogEditorPage kind={kind} /> };
                  },
                },
                {
                  path: `catalog/${kind}/:itemId`,
                  lazy: async () => {
                    const { CatalogEditorPage } =
                      await import('@/features/admin-catalog/pages/CatalogEditorPage');
                    return { element: <CatalogEditorPage kind={kind} /> };
                  },
                },
              ]),
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
              // U56: nenhum painel lateral de edição; cada formulário tem a sua página.
              lazyPage(undefined, async () => ({
                default: (await import('@/features/trainer-dashboard/pages/TrainerDashboardPage'))
                  .TrainerDashboardPage,
              })),

              // Clientes
              lazyPage('clients', async () => ({
                default: (await import('@/features/clients/pages/ClientsPage')).ClientsPage,
              })),
              lazyPage('clients/new', async () => ({
                default: (await import('@/features/clients/pages/ClientEditorPage'))
                  .ClientEditorPage,
              })),
              lazyPage('clients/:clientId', async () => ({
                default: (await import('@/features/clients/pages/ClientDetailPage'))
                  .ClientDetailPage,
              })),
              lazyPage('clients/:clientId/edit', async () => ({
                default: (await import('@/features/clients/pages/ClientEditorPage'))
                  .ClientEditorPage,
              })),
              lazyPage('clients/:clientId/initial-assessment', async () => ({
                default: (await import('@/features/clients/pages/InitialAssessmentPage'))
                  .InitialAssessmentPage,
              })),
              lazyPage('clients/:clientId/sell-pack', async () => ({
                default: (await import('@/features/packs/pages/SellPackPage')).SellPackPage,
              })),

              // Sessões e packs
              lazyPage('sessions', async () => ({
                default: (await import('@/features/sessions/pages/SessionsPage')).SessionsPage,
              })),
              lazyPage('sessions/new', async () => ({
                default: (await import('@/features/sessions/pages/SessionEditorPage'))
                  .SessionEditorPage,
              })),
              lazyPage('sessions/sell-pack', async () => ({
                default: (await import('@/features/packs/pages/SellPackPage')).SellPackPage,
              })),
              { path: 'sessions/pack-types', element: <PackTypesRedirect /> },
              lazyPage('sessions/pack-types/new', async () => ({
                default: (await import('@/features/packs/pages/PackTypeEditorPage'))
                  .PackTypeEditorPage,
              })),
              lazyPage('sessions/pack-types/:packTypeId', async () => ({
                default: (await import('@/features/packs/pages/PackTypeEditorPage'))
                  .PackTypeEditorPage,
              })),
              lazyPage('sessions/:sessionId', async () => ({
                default: (await import('@/features/sessions/pages/SessionEditorPage'))
                  .SessionEditorPage,
              })),

              // Avaliações (antes «check-ins»; o endereço mantém-se)
              lazyPage('check-ins', async () => ({
                default: (await import('@/features/check-ins/pages/CheckInsPage')).CheckInsPage,
              })),
              lazyPage('check-ins/new', async () => ({
                default: (await import('@/features/check-ins/pages/CheckInSchedulePage'))
                  .CheckInSchedulePage,
              })),
              lazyPage('check-ins/:checkInId', async () => ({
                default: (await import('@/features/check-ins/pages/CheckInDetailPage'))
                  .CheckInDetailPage,
              })),
              lazyPage('check-ins/:checkInId/reschedule', async () => ({
                default: (await import('@/features/check-ins/pages/CheckInSchedulePage'))
                  .CheckInSchedulePage,
              })),

              // Planos de treino: passos com o rascunho no layout (U57)
              lazyPage('training-plans', async () => ({
                default: (await import('@/features/prescriptions/pages/TrainingPlansPage'))
                  .TrainingPlansPage,
              })),
              {
                path: 'training-plans/new',
                lazy: async () => ({
                  Component: (await import('@/features/prescriptions/pages/TrainingPlanEditorPage'))
                    .TrainingPlanEditorLayout,
                }),
                children: [
                  lazyPage(undefined, async () => ({
                    default: (await import('@/features/prescriptions/pages/TrainingPlanEditorPage'))
                      .TrainingPlanDetailsStep,
                  })),
                  lazyPage('structure', async () => ({
                    default: (await import('@/features/prescriptions/pages/TrainingPlanEditorPage'))
                      .TrainingPlanStructureStep,
                  })),
                ],
              },
              lazyPage('training-plans/:planId', async () => ({
                default: (await import('@/features/prescriptions/pages/TrainingPlanViewPage'))
                  .TrainingPlanViewPage,
              })),
              {
                path: 'training-plans/:planId/edit',
                lazy: async () => ({
                  Component: (await import('@/features/prescriptions/pages/TrainingPlanEditorPage'))
                    .TrainingPlanEditorLayout,
                }),
                children: [
                  lazyPage(undefined, async () => ({
                    default: (await import('@/features/prescriptions/pages/TrainingPlanEditorPage'))
                      .TrainingPlanDetailsStep,
                  })),
                  lazyPage('structure', async () => ({
                    default: (await import('@/features/prescriptions/pages/TrainingPlanEditorPage'))
                      .TrainingPlanStructureStep,
                  })),
                ],
              },
              { path: 'training-plans/:planId/set-logs', element: <SetLogsRedirect /> },
              lazyPage('training-plans/:planId/set-logs/:logId', async () => ({
                default: (await import('@/features/prescriptions/pages/SetLogCorrectionPage'))
                  .SetLogCorrectionPage,
              })),

              // Planos alimentares: passo das metas e passo das refeições (U57)
              lazyPage('meal-plans', async () => ({
                default: (await import('@/features/prescriptions/pages/MealPlansPage'))
                  .MealPlansPage,
              })),
              {
                path: 'meal-plans/new',
                lazy: async () => ({
                  Component: (await import('@/features/prescriptions/pages/MealPlanEditorPage'))
                    .MealPlanEditorLayout,
                }),
                children: [
                  lazyPage(undefined, async () => ({
                    default: (await import('@/features/prescriptions/pages/MealPlanEditorPage'))
                      .MealPlanTargetsStep,
                  })),
                  lazyPage('meals', async () => ({
                    default: (await import('@/features/prescriptions/pages/MealPlanEditorPage'))
                      .MealPlanMealsStep,
                  })),
                ],
              },
              lazyPage('meal-plans/:planId', async () => ({
                default: (await import('@/features/prescriptions/pages/MealPlanViewPage'))
                  .MealPlanViewPage,
              })),
              {
                path: 'meal-plans/:planId/edit',
                lazy: async () => ({
                  Component: (await import('@/features/prescriptions/pages/MealPlanEditorPage'))
                    .MealPlanEditorLayout,
                }),
                children: [
                  lazyPage(undefined, async () => ({
                    default: (await import('@/features/prescriptions/pages/MealPlanEditorPage'))
                      .MealPlanTargetsStep,
                  })),
                  lazyPage('meals', async () => ({
                    default: (await import('@/features/prescriptions/pages/MealPlanEditorPage'))
                      .MealPlanMealsStep,
                  })),
                ],
              },

              // Suplementos atribuídos
              lazyPage('supplement-assignments', async () => ({
                default: (await import('@/features/prescriptions/pages/SupplementAssignmentsPage'))
                  .SupplementAssignmentsPage,
              })),
              lazyPage('supplement-assignments/new', async () => ({
                default: (
                  await import('@/features/prescriptions/pages/SupplementAssignmentEditorPage')
                ).SupplementAssignmentEditorPage,
              })),
              lazyPage('supplement-assignments/:assignmentId', async () => ({
                default: (
                  await import('@/features/prescriptions/pages/SupplementAssignmentEditorPage')
                ).SupplementAssignmentEditorPage,
              })),

              // Biblioteca privada
              lazyPage('library', async () => ({
                default: (await import('@/features/library/pages/LibraryPage')).LibraryPage,
              })),
              ...LIBRARY_KINDS.flatMap((kind): RouteObject[] => [
                { path: `library/${kind}`, element: <LibraryKindRedirect /> },
                {
                  path: `library/${kind}/new`,
                  lazy: async () => {
                    const { LibraryEditorPage } =
                      await import('@/features/library/pages/LibraryEditorPage');
                    return { element: <LibraryEditorPage kind={kind} /> };
                  },
                },
                {
                  path: `library/${kind}/:itemId`,
                  lazy: async () => {
                    const { LibraryEditorPage } =
                      await import('@/features/library/pages/LibraryEditorPage');
                    return { element: <LibraryEditorPage kind={kind} /> };
                  },
                },
              ]),

              lazyPage('settings', async () => ({
                default: (await import('@/features/trainer-settings/pages/SettingsPage'))
                  .SettingsPage,
              })),
              lazyPage('billing', async () => ({
                default: (await import('@/features/billing/pages/BillingPage')).BillingPage,
              })),
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
    hydrateFallbackElement: <RouteFallback />,
    children: applicationRoutes,
  },
];

export const router = createBrowserRouter(routes);
