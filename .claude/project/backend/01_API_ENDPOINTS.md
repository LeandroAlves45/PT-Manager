# PT Manager — Endpoints da API v1

> Gerado a partir de `docs/api/api-surface.v1.txt` (snapshot OpenAPI) cruzado com os
> atributos `[Authorize]`, `[AllowAnonymous]` e `[EnableRateLimiting]` dos 24 controllers
> em `backend/src/Api/Controllers/`. Data: 2026-09-15.
>
> **Ressalva:** os endpoints da Fase 5D (vídeo de exercício, Cloudflare R2) ainda não
> constam do snapshot — acrescentar quando o Gate 5D fechar, regenerando a partir do
> OpenAPI. Fonte de verdade é sempre o OpenAPI em runtime (`/openapi/v1.json`, só em
> Development) e o código dos controllers.

## Leitura rápida

- Prefixo de todas as rotas públicas: `/api/v1` (omitido nas tabelas). `/api/internal/*`
  não é consumido pelo frontend.
- Roles: `superuser`, `trainer`, `client` (claim `role`). "autenticado" = qualquer role.
- `p:` parâmetro de path, `q:` query; `!` obrigatório, `?` opcional. Nomes em snake_case.
- Rate limit `global`: 300 pedidos/min por utilizador autenticado, 60/min por IP anónimo.
  Limites das policies nomeadas em [02_CONTRATO_HTTP_FRONTEND.md](02_CONTRATO_HTTP_FRONTEND.md).
- Padrões transversais: `POST …/archive` e `POST …/reactivate` alternam `is_active`
  (sem soft delete); `DELETE` só existe em catálogos globais e media.

## Contagem por grupo

| Grupo | Endpoints | Role |
|---|---:|---|
| [Autenticação e conta](#auth) | 16 | anónimo, autenticado, client, trainer |
| [Administração — moderação](#admin) | 4 | superuser (contexto administrativo) |
| [Catálogo global — exercícios](#global-exercises) | 7 | superuser |
| [Catálogo global — alimentos](#global-foods) | 7 | superuser |
| [Catálogo global — suplementos](#global-supplements) | 7 | superuser |
| [Clientes](#clients) | 6 | trainer |
| [Avaliação inicial](#initial-assessments) | 3 | trainer |
| [Check-ins](#check-ins) | 6 | trainer |
| [Sessões](#sessions) | 10 | trainer |
| [Tipos de pack](#pack-types) | 6 | trainer |
| [Packs de sessões do cliente](#client-session-packs) | 6 | trainer |
| [Exercícios privados do trainer](#exercises) | 6 | trainer |
| [Planos de treino](#training-plans) | 7 | trainer |
| [Registo de séries](#exercise-set-logs) | 3 | trainer |
| [Alimentos privados do trainer](#foods) | 6 | trainer |
| [Planos alimentares](#meal-plans) | 6 | trainer |
| [Nutrição — cálculo](#nutrition) | 1 | trainer |
| [Suplementos privados do trainer](#supplements) | 6 | trainer |
| [Atribuição de suplementos](#supplement-assignments) | 6 | trainer |
| [Definições do trainer e marca](#trainer-settings) | 7 | trainer |
| [Billing (Stripe)](#billing) | 4 | anónimo (assinatura Stripe), trainer |
| [Portal do cliente](#portal) | 11 | client |
| [Interno (jobs)](#internal) | 1 | anónimo (assinatura QStash) |
| **Total** | **142** | |

<a id="auth"></a>

## Autenticação e conta

| Método | Rota | Role | Parâmetros | Rate limit | Notas |
|---|---|---|---|---|---|
| `POST` | `/auth/accept-invite` | client | — | `auth_invite_client` | `RequireOrigin` |
| `POST` | `/auth/change-password` | autenticado | — | `auth_change_password` | `RequireOrigin` |
| `POST` | `/auth/confirm-email` | anónimo | — | `auth_email_confirmation` | `RequireOrigin` |
| `POST` | `/auth/csrf` | anónimo | — | `auth_csrf_bootstrap` | `RequireOrigin`; lê cookie refresh, devolve `csrf_token` |
| `POST` | `/auth/google/challenge` | anónimo | — | `auth_google_sign_in` | `RequireOrigin` |
| `POST` | `/auth/google/link` | autenticado | — | `auth_google_link` | `RequireOrigin` |
| `POST` | `/auth/google/link/challenge` | autenticado | — | `auth_google_link` | `RequireOrigin` |
| `POST` | `/auth/google/sign-in` | anónimo | — | `auth_google_sign_in` | `RequireOrigin` |
| `POST` | `/auth/invite-client` | trainer | — | `auth_invite_client` | `RequireOrigin` |
| `POST` | `/auth/login` | anónimo | — | `auth_login` | `RequireOrigin` |
| `POST` | `/auth/logout` | anónimo | — | `auth_logout` | `RequireOrigin`; cookie refresh + `X-CSRF-Token` |
| `POST` | `/auth/password-reset/complete` | anónimo | — | `auth_password_reset_complete` | `RequireOrigin` |
| `POST` | `/auth/password-reset/request` | anónimo | — | `auth_password_reset_request` | `RequireOrigin` |
| `POST` | `/auth/refresh` | anónimo | — | `auth_refresh` | `RequireOrigin`; cookie refresh + `X-CSRF-Token` |
| `POST` | `/auth/resend-confirmation` | autenticado | — | `auth_email_confirmation_resend` | `RequireOrigin` |
| `POST` | `/auth/signup` | anónimo | — | `auth_signup` | `RequireOrigin` |

<a id="admin"></a>

## Administração — moderação

| Método | Rota | Role | Parâmetros | Rate limit | Notas |
|---|---|---|---|---|---|
| `POST` | `/admin/content-moderation/exercises/{exerciseId}/block` | superuser (contexto administrativo) | p:exerciseId! | `admin_moderation` | auditoria administrativa |
| `POST` | `/admin/content-moderation/exercises/{exerciseId}/unblock` | superuser (contexto administrativo) | p:exerciseId! | `admin_moderation` | auditoria administrativa |
| `POST` | `/admin/content-moderation/foods/{foodId}/block` | superuser (contexto administrativo) | p:foodId! | `admin_moderation` | auditoria administrativa |
| `POST` | `/admin/content-moderation/foods/{foodId}/unblock` | superuser (contexto administrativo) | p:foodId! | `admin_moderation` | auditoria administrativa |

<a id="global-exercises"></a>

## Catálogo global — exercícios

| Método | Rota | Role | Parâmetros | Rate limit | Notas |
|---|---|---|---|---|---|
| `GET` | `/global-exercises` | superuser | q:activity?, q:page_number?, q:page_size?, q:search? | `global` | auditoria administrativa; paginado |
| `POST` | `/global-exercises` | superuser | — | `global` | auditoria administrativa |
| `DELETE` | `/global-exercises/{exerciseId}` | superuser | p:exerciseId! | `global` | auditoria administrativa |
| `GET` | `/global-exercises/{exerciseId}` | superuser | p:exerciseId! | `global` | auditoria administrativa |
| `PATCH` | `/global-exercises/{exerciseId}` | superuser | p:exerciseId! | `global` | auditoria administrativa |
| `POST` | `/global-exercises/{exerciseId}/archive` | superuser | p:exerciseId! | `global` | auditoria administrativa |
| `POST` | `/global-exercises/{exerciseId}/reactivate` | superuser | p:exerciseId! | `global` | auditoria administrativa |

<a id="global-foods"></a>

## Catálogo global — alimentos

| Método | Rota | Role | Parâmetros | Rate limit | Notas |
|---|---|---|---|---|---|
| `GET` | `/global-foods` | superuser | q:activity?, q:page_number?, q:page_size?, q:search? | `global` | auditoria administrativa; paginado |
| `POST` | `/global-foods` | superuser | — | `global` | auditoria administrativa |
| `DELETE` | `/global-foods/{foodId}` | superuser | p:foodId! | `global` | auditoria administrativa |
| `GET` | `/global-foods/{foodId}` | superuser | p:foodId! | `global` | auditoria administrativa |
| `PATCH` | `/global-foods/{foodId}` | superuser | p:foodId! | `global` | auditoria administrativa |
| `POST` | `/global-foods/{foodId}/archive` | superuser | p:foodId! | `global` | auditoria administrativa |
| `POST` | `/global-foods/{foodId}/reactivate` | superuser | p:foodId! | `global` | auditoria administrativa |

<a id="global-supplements"></a>

## Catálogo global — suplementos

| Método | Rota | Role | Parâmetros | Rate limit | Notas |
|---|---|---|---|---|---|
| `GET` | `/global-supplements` | superuser | q:activity?, q:page_number?, q:page_size?, q:search? | `global` | auditoria administrativa; paginado |
| `POST` | `/global-supplements` | superuser | — | `global` | auditoria administrativa |
| `DELETE` | `/global-supplements/{supplementId}` | superuser | p:supplementId! | `global` | auditoria administrativa |
| `GET` | `/global-supplements/{supplementId}` | superuser | p:supplementId! | `global` | auditoria administrativa |
| `PATCH` | `/global-supplements/{supplementId}` | superuser | p:supplementId! | `global` | auditoria administrativa |
| `POST` | `/global-supplements/{supplementId}/archive` | superuser | p:supplementId! | `global` | auditoria administrativa |
| `POST` | `/global-supplements/{supplementId}/reactivate` | superuser | p:supplementId! | `global` | auditoria administrativa |

<a id="clients"></a>

## Clientes

| Método | Rota | Role | Parâmetros | Rate limit | Notas |
|---|---|---|---|---|---|
| `GET` | `/clients` | trainer | q:activity?, q:page_number?, q:page_size?, q:search? | `global` | paginado |
| `POST` | `/clients` | trainer | — | `global` |  |
| `GET` | `/clients/{clientId}` | trainer | p:clientId! | `global` |  |
| `PATCH` | `/clients/{clientId}` | trainer | p:clientId! | `global` |  |
| `POST` | `/clients/{clientId}/archive` | trainer | p:clientId! | `global` |  |
| `POST` | `/clients/{clientId}/reactivate` | trainer | p:clientId! | `global` |  |

<a id="initial-assessments"></a>

## Avaliação inicial

| Método | Rota | Role | Parâmetros | Rate limit | Notas |
|---|---|---|---|---|---|
| `GET` | `/clients/{clientId}/initial-assessment` | trainer | p:clientId! | `global` |  |
| `POST` | `/initial-assessments` | trainer | — | `global` |  |
| `PUT` | `/initial-assessments/{assessmentId}` | trainer | p:assessmentId! | `global` |  |

<a id="check-ins"></a>

## Check-ins

| Método | Rota | Role | Parâmetros | Rate limit | Notas |
|---|---|---|---|---|---|
| `GET` | `/check-ins` | trainer | q:client_id?, q:from_date?, q:page_number?, q:page_size?, q:status?, q:to_date? | `global` | paginado |
| `POST` | `/check-ins` | trainer | — | `global` |  |
| `GET` | `/check-ins/{checkInId}` | trainer | p:checkInId! | `global` |  |
| `PUT` | `/check-ins/{checkInId}/answer` | trainer | p:checkInId! | `global` |  |
| `POST` | `/check-ins/{checkInId}/cancel` | trainer | p:checkInId! | `global` |  |
| `PATCH` | `/check-ins/{checkInId}/reschedule` | trainer | p:checkInId! | `global` |  |

<a id="sessions"></a>

## Sessões

| Método | Rota | Role | Parâmetros | Rate limit | Notas |
|---|---|---|---|---|---|
| `GET` | `/sessions` | trainer | q:client_id?, q:page_number?, q:page_size?, q:starts_before?, q:starts_from?, q:status? | `global` | paginado |
| `POST` | `/sessions` | trainer | — | `global` |  |
| `GET` | `/sessions/{sessionId}` | trainer | p:sessionId! | `global` |  |
| `POST` | `/sessions/{sessionId}/cancel-by-client` | trainer | p:sessionId! | `global` |  |
| `POST` | `/sessions/{sessionId}/cancel-by-trainer` | trainer | p:sessionId! | `global` |  |
| `POST` | `/sessions/{sessionId}/complete` | trainer | p:sessionId! | `global` |  |
| `POST` | `/sessions/{sessionId}/no-show` | trainer | p:sessionId! | `global` |  |
| `PATCH` | `/sessions/{sessionId}/pack` | trainer | p:sessionId! | `global` |  |
| `PATCH` | `/sessions/{sessionId}/reschedule` | trainer | p:sessionId! | `global` |  |
| `POST` | `/sessions/{sessionId}/restore` | trainer | p:sessionId! | `global` |  |

<a id="pack-types"></a>

## Tipos de pack

| Método | Rota | Role | Parâmetros | Rate limit | Notas |
|---|---|---|---|---|---|
| `GET` | `/pack-types` | trainer | q:activity?, q:page_number?, q:page_size?, q:search? | `global` | paginado |
| `POST` | `/pack-types` | trainer | — | `global` |  |
| `GET` | `/pack-types/{packTypeId}` | trainer | p:packTypeId! | `global` |  |
| `PATCH` | `/pack-types/{packTypeId}` | trainer | p:packTypeId! | `global` |  |
| `POST` | `/pack-types/{packTypeId}/archive` | trainer | p:packTypeId! | `global` |  |
| `POST` | `/pack-types/{packTypeId}/reactivate` | trainer | p:packTypeId! | `global` |  |

<a id="client-session-packs"></a>

## Packs de sessões do cliente

| Método | Rota | Role | Parâmetros | Rate limit | Notas |
|---|---|---|---|---|---|
| `GET` | `/client-session-packs` | trainer | q:activity?, q:client_id?, q:page_number?, q:page_size? | `global` | paginado |
| `POST` | `/client-session-packs` | trainer | — | `global` |  |
| `GET` | `/client-session-packs/usable` | trainer | q:client_id? | `global` |  |
| `GET` | `/client-session-packs/{clientSessionPackId}` | trainer | p:clientSessionPackId! | `global` |  |
| `POST` | `/client-session-packs/{clientSessionPackId}/cancel` | trainer | p:clientSessionPackId! | `global` |  |
| `PATCH` | `/client-session-packs/{clientSessionPackId}/expected-end-date` | trainer | p:clientSessionPackId! | `global` |  |

<a id="exercises"></a>

## Exercícios privados do trainer

| Método | Rota | Role | Parâmetros | Rate limit | Notas |
|---|---|---|---|---|---|
| `GET` | `/exercises` | trainer | q:activity?, q:page_number?, q:page_size?, q:search? | `global` | paginado |
| `POST` | `/exercises` | trainer | — | `global` |  |
| `GET` | `/exercises/{exerciseId}` | trainer | p:exerciseId! | `global` |  |
| `PATCH` | `/exercises/{exerciseId}` | trainer | p:exerciseId! | `global` |  |
| `POST` | `/exercises/{exerciseId}/archive` | trainer | p:exerciseId! | `global` |  |
| `POST` | `/exercises/{exerciseId}/reactivate` | trainer | p:exerciseId! | `global` |  |

<a id="training-plans"></a>

## Planos de treino

| Método | Rota | Role | Parâmetros | Rate limit | Notas |
|---|---|---|---|---|---|
| `GET` | `/training-plans` | trainer | q:activity?, q:client_id?, q:page_number?, q:page_size?, q:search? | `global` | paginado |
| `POST` | `/training-plans` | trainer | — | `global` |  |
| `GET` | `/training-plans/{trainingPlanId}` | trainer | p:trainingPlanId! | `global` |  |
| `PATCH` | `/training-plans/{trainingPlanId}` | trainer | p:trainingPlanId! | `global` |  |
| `PUT` | `/training-plans/{trainingPlanId}` | trainer | p:trainingPlanId! | `global` |  |
| `POST` | `/training-plans/{trainingPlanId}/archive` | trainer | p:trainingPlanId! | `global` |  |
| `PUT` | `/training-plans/{trainingPlanId}/structure` | trainer | p:trainingPlanId! | `global` |  |

<a id="exercise-set-logs"></a>

## Registo de séries

| Método | Rota | Role | Parâmetros | Rate limit | Notas |
|---|---|---|---|---|---|
| `GET` | `/exercise-set-logs` | trainer | q:client_id?, q:page_number?, q:page_size?, q:performed_from?, q:performed_to?, q:training_plan_id? | `global` | paginado |
| `POST` | `/exercise-set-logs` | trainer | — | `global` |  |
| `PATCH` | `/exercise-set-logs/{exerciseSetLogId}` | trainer | p:exerciseSetLogId! | `global` |  |

<a id="foods"></a>

## Alimentos privados do trainer

| Método | Rota | Role | Parâmetros | Rate limit | Notas |
|---|---|---|---|---|---|
| `GET` | `/foods` | trainer | q:activity?, q:page_number?, q:page_size?, q:search? | `global` | paginado |
| `POST` | `/foods` | trainer | — | `global` |  |
| `GET` | `/foods/{foodId}` | trainer | p:foodId! | `global` |  |
| `PATCH` | `/foods/{foodId}` | trainer | p:foodId! | `global` |  |
| `POST` | `/foods/{foodId}/archive` | trainer | p:foodId! | `global` |  |
| `POST` | `/foods/{foodId}/reactivate` | trainer | p:foodId! | `global` |  |

<a id="meal-plans"></a>

## Planos alimentares

| Método | Rota | Role | Parâmetros | Rate limit | Notas |
|---|---|---|---|---|---|
| `GET` | `/meal-plans` | trainer | q:activity?, q:client_id?, q:page_number?, q:page_size?, q:search? | `global` | paginado |
| `POST` | `/meal-plans` | trainer | — | `global` |  |
| `GET` | `/meal-plans/{mealPlanId}` | trainer | p:mealPlanId! | `global` |  |
| `PUT` | `/meal-plans/{mealPlanId}` | trainer | p:mealPlanId! | `global` |  |
| `POST` | `/meal-plans/{mealPlanId}/archive` | trainer | p:mealPlanId! | `global` |  |
| `POST` | `/meal-plans/{mealPlanId}/reactivate` | trainer | p:mealPlanId! | `global` |  |

<a id="nutrition"></a>

## Nutrição — cálculo

| Método | Rota | Role | Parâmetros | Rate limit | Notas |
|---|---|---|---|---|---|
| `POST` | `/nutrition/preview` | trainer | — | `global` |  |

<a id="supplements"></a>

## Suplementos privados do trainer

| Método | Rota | Role | Parâmetros | Rate limit | Notas |
|---|---|---|---|---|---|
| `GET` | `/supplements` | trainer | q:activity?, q:page_number?, q:page_size?, q:search? | `global` | paginado |
| `POST` | `/supplements` | trainer | — | `global` |  |
| `GET` | `/supplements/{supplementId}` | trainer | p:supplementId! | `global` |  |
| `PATCH` | `/supplements/{supplementId}` | trainer | p:supplementId! | `global` |  |
| `POST` | `/supplements/{supplementId}/archive` | trainer | p:supplementId! | `global` |  |
| `POST` | `/supplements/{supplementId}/reactivate` | trainer | p:supplementId! | `global` |  |

<a id="supplement-assignments"></a>

## Atribuição de suplementos

| Método | Rota | Role | Parâmetros | Rate limit | Notas |
|---|---|---|---|---|---|
| `GET` | `/supplement-assignments` | trainer | q:activity?, q:client_id?, q:page_number?, q:page_size? | `global` | paginado |
| `POST` | `/supplement-assignments` | trainer | — | `global` |  |
| `GET` | `/supplement-assignments/{assignmentId}` | trainer | p:assignmentId! | `global` |  |
| `PATCH` | `/supplement-assignments/{assignmentId}` | trainer | p:assignmentId! | `global` |  |
| `POST` | `/supplement-assignments/{assignmentId}/deactivate` | trainer | p:assignmentId! | `global` |  |
| `POST` | `/supplement-assignments/{assignmentId}/reactivate` | trainer | p:assignmentId! | `global` |  |

<a id="trainer-settings"></a>

## Definições do trainer e marca

| Método | Rota | Role | Parâmetros | Rate limit | Notas |
|---|---|---|---|---|---|
| `GET` | `/trainer-settings` | trainer | — | `global` |  |
| `PATCH` | `/trainer-settings/branding` | trainer | — | `global` |  |
| `POST` | `/trainer-settings/branding/reset-colors` | trainer | — | `global` |  |
| `PATCH` | `/trainer-settings/contacts` | trainer | — | `global` |  |
| `DELETE` | `/trainer-settings/logo` | trainer | — | `global` |  |
| `PUT` | `/trainer-settings/logo` | trainer | — | `media_upload` | `multipart/form-data`, máx. 6 MiB |
| `PATCH` | `/trainer-settings/timezone` | trainer | — | `global` |  |

<a id="billing"></a>

## Billing (Stripe)

| Método | Rota | Role | Parâmetros | Rate limit | Notas |
|---|---|---|---|---|---|
| `POST` | `/billing/checkout` | trainer | — | `global` |  |
| `POST` | `/billing/customer-portal` | trainer | — | `global` |  |
| `GET` | `/billing/subscription` | trainer | — | `global` |  |
| `POST` | `/billing/webhook` | anónimo (assinatura Stripe) | — | `global` | não consumir no frontend; `api-surface` marca `required`, o controller tem `[AllowAnonymous]` |

<a id="portal"></a>

## Portal do cliente

| Método | Rota | Role | Parâmetros | Rate limit | Notas |
|---|---|---|---|---|---|
| `GET` | `/portal/branding` | client | — | `global` |  |
| `POST` | `/portal/check-ins/{checkInId}/respond` | client | p:checkInId! | `global` |  |
| `GET` | `/portal/my-check-ins/due` | client | — | `global` |  |
| `GET` | `/portal/my-nutrition` | client | — | `global` |  |
| `GET` | `/portal/my-plan` | client | — | `global` |  |
| `GET` | `/portal/my-profile` | client | — | `global` |  |
| `PATCH` | `/portal/my-profile` | client | — | `global` |  |
| `DELETE` | `/portal/my-profile/avatar` | client | — | `global` |  |
| `PUT` | `/portal/my-profile/avatar` | client | — | `media_upload` | `multipart/form-data`, máx. 6 MiB |
| `GET` | `/portal/my-supplements` | client | q:page_number?, q:page_size? | `global` | paginado |
| `GET` | `/portal/my-supplements/{assignmentId}` | client | p:assignmentId! | `global` |  |

<a id="internal"></a>

## Interno (jobs)

| Método | Rota | Role | Parâmetros | Rate limit | Notas |
|---|---|---|---|---|---|
| `POST` | `/api/internal/jobs/dispatch` | anónimo (assinatura QStash) | — | `internal_jobs_dispatch` | não consumir no frontend |

