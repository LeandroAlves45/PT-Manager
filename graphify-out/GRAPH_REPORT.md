# Graph Report - Projeto_pt_manager  (2026-07-13)

## Corpus Check
- 274 files · ~150,368 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 2338 nodes · 4969 edges · 172 communities (126 shown, 46 thin omitted)
- Extraction: 90% EXTRACTED · 10% INFERRED · 0% AMBIGUOUS · INFERRED: 478 edges (avg confidence: 0.76)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `96aed6d1`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- Supplement
- User
- auth.py
- timedelta
- TestCatalogoSuplementos
- nutrition.py
- TestDefinePasswordAndAutoLogin
- Exercises.jsx
- TrainingPlans.jsx
- cn
- AdminService
- card.tsx
- client_portal.py
- nutrition.py
- TrainingSession
- button.tsx
- hash_password
- TrainerSettings
- utc_now_datetime
- useAuth
- TestLogin
- commit_or_rollback
- verify_email
- stripe_webhook.py
- PlanDayExercise
- MealsPlanPage.jsx
- compilerOptions
- ClientPack
- CheckInRead
- .can_add_client
- TestCreateAssessment
- AssessmentPage.jsx
- axiosConfig.js
- FastAPI
- ClientDetails.jsx
- compilerOptions
- set_password_via_invite
- nutrition.py
- TestCalculateTMBAllFormulas
- adminApi.js
- dependencies
- Notification
- NutritionPage.jsx
- ClientLayout.jsx
- training.py
- StripeService
- TestGetTierForCount
- MyProfile.jsx
- get_activity_factor_options
- PackType
- client.py
- calculate_macros_from_grams_per_kg
- calculate_macros_from_percentages
- compilerOptions
- InitialAssessmentRead
- utc_now
- InitialAssessment
- .generate_invite
- billing.py
- assessment.py
- NotificationService
- TrainerSubscription
- EmailService
- TestHasActiveAccess
- security.py
- .log_error
- chart.tsx
- SupplementPage.jsx
- Exercise
- ClientRepository
- calculate_tmb_all_formulas
- carousel.tsx
- ActiveTokenRepository
- test_macro_calculator.py
- devDependencies
- drawer.tsx
- seed_catalogue
- BillingPage.jsx
- App.jsx
- lifespan
- main.py
- scheduler.py
- exercisesApi.js
- context-menu.tsx
- training_plans.py
- run_migrations
- SignupService
- handlers.js
- _build_adherence
- pack_types.py
- SubscriptionRead
- ClientFirstLoginPage.jsx
- training_session.py
- rate_limit.py
- get_session
- scripts
- ClientPackPurchase
- RateLimitEmailMiddleware
- Settings
- seed_demo_data
- .cancel_pending_reminders_for_session
- package.json
- _build_macro_grams
- subscription_service.py
- __init__.py
- @chakra-ui/react
- cmdk
- cors
- embla-carousel-react
- @emotion/styled
- @eslint/js
- eslint-plugin-react-hooks
- eslint-plugin-react-refresh
- @radix-ui/react-accordion
- @radix-ui/react-alert-dialog
- @radix-ui/react-avatar
- @radix-ui/react-checkbox
- @radix-ui/react-context-menu
- @radix-ui/react-dropdown-menu
- @radix-ui/react-label
- @radix-ui/react-separator
- @radix-ui/react-slot
- @radix-ui/react-switch
- @radix-ui/react-tabs
- react-day-picker
- react-dom
- react-hook-form
- react-router-dom
- react-toastify
- recharts
- tailwind-merge
- vaul
- @vercel/speed-insights
- globals
- jsdom
- msw
- prettier
- tailwindcss
- @tailwindcss/vite
- @testing-library/jest-dom
- @testing-library/react
- @testing-library/user-event
- @types/node
- @types/react
- @types/recharts
- typescript
- @vitejs/plugin-react
- vitest
- vercel.json
- pt-manager
- autoprefixer

## God Nodes (most connected - your core abstractions)
1. `commit_or_rollback()` - 84 edges
2. `cn()` - 61 edges
3. `Client` - 53 edges
4. `User` - 46 edges
5. `Button` - 39 edges
6. `utc_now_datetime()` - 35 edges
7. `Badge()` - 29 edges
8. `Input` - 28 edges
9. `Card` - 26 edges
10. `CardContent` - 26 edges

## Surprising Connections (you probably didn't know these)
- `create_assessment()` --indirect_call--> `Client`  [INFERRED]
  backend/app/api/routes/assessments.py → backend/app/db/models/client.py
- `create_assessment()` --calls--> `commit_or_rollback()`  [INFERRED]
  backend/app/api/routes/assessments.py → backend/app/utils/db_errors.py
- `list_assessments_by_client()` --indirect_call--> `Client`  [INFERRED]
  backend/app/api/routes/assessments.py → backend/app/db/models/client.py
- `get_assessment()` --indirect_call--> `Client`  [INFERRED]
  backend/app/api/routes/assessments.py → backend/app/db/models/client.py
- `update_assessment()` --indirect_call--> `Client`  [INFERRED]
  backend/app/api/routes/assessments.py → backend/app/db/models/client.py

## Import Cycles
- None detected.

## Communities (172 total, 46 thin omitted)

### Community 0 - "Supplement"
Cohesion: 0.06
Nodes (57): assign_supplement_to_client(), _build_response(), _get_assignment_or_404(), _get_client_or_404(), _get_supplement_or_404(), list_client_supplements(), Session, Router de atribuicao de suplementos a clientes (SU-04).   Permite ao trainer g (+49 more)

### Community 1 - "User"
Cohesion: 0.07
Nodes (32): create_user(), Trainer cria conta User para Client existente (tenant isolation)., SQLModel, Modelo de base de dados para utilizadores autenticados.  Um User representa qu, Representa um utilizador autenticado no sistema.      Roles disponíveis:, User, datetime, Session (+24 more)

### Community 2 - "auth.py"
Cohesion: 0.06
Nodes (45): change_password(), get_my_profile(), list_users(), login(), logout(), Request, Response, Session (+37 more)

### Community 3 - "timedelta"
Cohesion: 0.09
Nodes (23): api_key_headers(), client(), client_headers(), client_record(), client_user(), db(), engine_in_memory(), conftest.py - infraestrutura de testes para o PT Manager.  NOTA: variaveis de am (+15 more)

### Community 4 - "TestCatalogoSuplementos"
Cohesion: 0.04
Nodes (27): Testes de integracao para suplementos (SU-01 a SU-05).  Cobre:   CRUD do cata, PATCH actualiza apenas os campos enviados., Trainer B nao pode editar suplementos criados por Trainer A.         Verifica a, Trainer B nao pode arquivar suplementos do Trainer A., DELETE remove permanentemente o suplemento., Clientes não devem ver o campo trainer_notes (informacao interna do trainer)., Testes de atribuição de suplementos a clientes., POST /clients/{id}/supplements atribui suplemento e devolve 201. (+19 more)

### Community 5 - "nutrition.py"
Cohesion: 0.11
Nodes (34): MealPlan, MealPlanItem, MealPlanMeal, MealPlanMealSupplement, SQLModel, Modelos ORM para o sistem de Nutrição  Estrutura: foods - tabela principal de, Refeições dentro do plano alimentar (ex: pequeno-almoço, almoço, jantar, lanches, Linha de um alimento dentro de uma refeição do plano alimentar. (+26 more)

### Community 6 - "TestDefinePasswordAndAutoLogin"
Cohesion: 0.08
Nodes (19): Testes de integracao para o fluxo de convite de clientes (AU-09).  Cobre o flu, Trainer B não pode gerar convite para cliente de Trainer A., Falha no envio de email não deve bloquear a geração do link (best-effort)., Tentar gerar convite para cliente que ainda não tem conta User.         O train, Testes de definição de password e auto-login via convite., Cria token valido na BD e devolve o raw token., POST /invite/set-password/{token} com password valida deve:         - Devolver, Helper identico ao da implementacao. (+11 more)

### Community 7 - "Exercises.jsx"
Cohesion: 0.15
Nodes (34): MUSCLE_GROUPS, MuscleMultiSelect(), getStatusStyle(), PlanList(), AlertDialogAction, AlertDialogCancel, AlertDialogContent, AlertDialogDescription (+26 more)

### Community 8 - "TrainingPlans.jsx"
Cohesion: 0.16
Nodes (13): createTrainingPlan(), deleteTrainingPlan(), getDayExercises(), getPlanDays(), getTrainingPlans(), setClientActivePlan(), updateTrainingPlan(), ActivatePlanDialog() (+5 more)

### Community 9 - "cn"
Cohesion: 0.25
Nodes (7): Breadcrumb, BreadcrumbEllipsis(), BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator()

### Community 10 - "AdminService"
Cohesion: 0.08
Nodes (30): activate_trainer(), get_metrics(), grant_exemption(), list_trainers(), Session, Router de administração - apenas superusers.  O superuser tem visibilidade glo, Revoga isenção de billing de um Personal Trainer,      obrigando-o a subscrever, Devolve métricas globais da plataforma para o superuser. (+22 more)

### Community 11 - "card.tsx"
Cohesion: 0.07
Nodes (50): getPlatformMetrics(), getSubscription(), changeMyPassword(), getMyMealPlans(), getMyProfile(), getMyTrainingPlan(), respondToCheckIn(), upsertExerciseSetLogs() (+42 more)

### Community 12 - "client_portal.py"
Cohesion: 0.10
Nodes (33): _client_set_log_to_read(), _client_to_portal_profile_read(), _get_active_client_plan(), _get_client_id(), _get_client_or_404(), _get_day_exercise_for_active_plan_or_404(), get_my_check_ins(), get_my_meal_plans() (+25 more)

### Community 13 - "nutrition.py"
Cohesion: 0.08
Nodes (33): calculate_macros(), FoodCreate, FoodUpdate, FormulaResult, MacroAdherence, MacroCalculationRequest, MacroCalculationResponse, MacroDistribution (+25 more)

### Community 14 - "TrainingSession"
Cohesion: 0.12
Nodes (23): cancel_session(), complete_session(), list_sessions(), mark_session_missed(), Session, Marca uma sessão como concluída e consome um pack do cliente., Agenda uma nova sessão de treino para um cliente específico., schedule_session_for_client() (+15 more)

### Community 15 - "button.tsx"
Cohesion: 0.21
Nodes (20): getGlobalSupplements(), getMyCheckIns(), Button, ButtonProps, DialogContent, DialogDescription, DialogFooter(), DialogHeader() (+12 more)

### Community 16 - "hash_password"
Cohesion: 0.11
Nodes (16): hash_password(), Converte a password em hash usando bcrypt., Verifica se a password em texto plano corresponde ao hash armazenado., verify_password(), Session, seed_superuser(), Testa o sistema de hash de passwords usando bcrypt, O hash deve ser uma string não vazia. (+8 more)

### Community 17 - "TrainerSettings"
Cohesion: 0.10
Nodes (26): delete_trainer_logo(), get_or_create_trainer_settings(), get_trainer_profile(), get_trainer_settings(), BaseModel, Session, Router de perfil e branding do trainer.   Endpoints:     GET   /trainer-profi, Actualiza as configurações de branding do trainer.     Apenas os campos enviado (+18 more)

### Community 18 - "utc_now_datetime"
Cohesion: 0.06
Nodes (43): create_access_token(), Cria um token JWT com as claims necessárias para autenticação e controlo de aces, SQLModel, Modelo para Refresh Tokens -> segurança da sessão., Tokens de refresh — permitem renovar access tokens sem pedir credenciais., Retorna True se não foi revogado e ainda não expirou., RefreshToken, datetime (+35 more)

### Community 19 - "useAuth"
Cohesion: 0.09
Nodes (26): App(), getDashboardForRole(), ProtectedRoute(), mockUseAuth, applyBodyColor(), applyBrandColor(), AuthProvider(), AuthConsumer() (+18 more)

### Community 20 - "TestLogin"
Cohesion: 0.08
Nodes (16): Testes de integracao para o sistema de active tokens (migration 006).  Princip, Email que nao existe na BD deve devolver 401 (igual a password errada)., Pedidos sem X-API-Key devem ser rejeitados com 401,         mesmo que as creden, Trainer suspenso (is_active=False) nao deve conseguir fazer login., Testes de logout e invalidação de tokens activos., Auxiliar: faz login e devolve os headers de autenticacao., Apos POST /auth/logout, a linha em active_tokens deve ser apagada., Usar o token apos logout deve devolver 401.         Verifica que a invalidação (+8 more)

### Community 21 - "commit_or_rollback"
Cohesion: 0.19
Nodes (22): archive_client(), _build_client_with_pack(), _client_status(), create_client(), delete_client(), get_client_details(), get_my_client_profile(), _get_trainer_id_filter() (+14 more)

### Community 22 - "verify_email"
Cohesion: 0.11
Nodes (25): Request, Response, Session, Endpoints públicos de signup., Reenvia email de verificação para trainer (fallback).      Sempre devolve a me, Signup de novo Personal Trainer.      Fluxo:     1. Valida email + password +, Verifica o email do trainer e retorna access_token.      Fluxo:     1. Valida, resend_verification_email() (+17 more)

### Community 23 - "stripe_webhook.py"
Cohesion: 0.06
Nodes (34): Any, _handle_payment_failed(), _handle_payment_succeeded(), _handle_subscription_deleted(), _handle_subscription_updated(), _handle_trial_will_end(), _normalize_stripe_status(), Request (+26 more)

### Community 24 - "PlanDayExercise"
Cohesion: 0.20
Nodes (28): create_day_exercise(), create_set_load(), _day_exercise_to_read(), _day_exercise_with_details_to_read(), delete_day_exercise(), delete_plan(), delete_plan_day(), delete_set_load() (+20 more)

### Community 25 - "MealsPlanPage.jsx"
Cohesion: 0.07
Nodes (40): addCheckinNotes(), createAssessment(), createCheckin(), getAssessmentsByClient(), getCheckinsByClient(), skipCheckin(), updateAssessment(), getClients() (+32 more)

### Community 26 - "compilerOptions"
Cohesion: 0.08
Nodes (25): compilerOptions, allowSyntheticDefaultImports, baseUrl, esModuleInterop, forceConsistentCasingInFileNames, isolatedModules, jsx, lib (+17 more)

### Community 27 - "ClientPack"
Cohesion: 0.06
Nodes (50): create_pack_type(), delete_pack_type(), _get_owned_pack_type(), list_pack_types(), _pack_type_visibility_filter(), Session, Rotas para gestão de tipos de packs (catálogo de packs disponíveis).  Responsa, Cria um novo tipo de pack com validação de nome único. (+42 more)

### Community 28 - "CheckInRead"
Cohesion: 0.17
Nodes (19): add_trainer_notes(), create_checkin(), get_my_pending_checkins(), list_checkins_for_client(), Session, Router para Check-Ins periódicos.  Endpoints:     POST   /check-ins/, respond_to_checkin(), skip_checkin() (+11 more)

### Community 29 - ".can_add_client"
Cohesion: 0.14
Nodes (14): Verifica se o trainer pode adicionar mais um cliente.          Retorna uma tup, _make_subscription(), Testes unitários para o SubscriptionService.  Estes testes cobrem a lógica pur, STARTER com 49 clientes não pode adicionar mais clientes., PRO deve permitir adicionar clientes sem limite prático., Subscrição cancelada não pode adicionar clientes., Subscrição com trial expirado não pode adicionar clientes., Subscrição em trial dentro dos limites pode adicionar clientes. (+6 more)

### Community 30 - "TestCreateAssessment"
Cohesion: 0.08
Nodes (14): Testes de integração para o router de avaliações iniciais., Cliente sem avaliações devolve lista vazia., POST /assessments/ com payload mínimo deve criar avaliação e devolver 201., Trainer B não pode listar avaliações de clientes do Trainer A., Avaliação com peso e altura deve guardar esses valores., Criar avaliação para cliente inexistente deve devolver 404., Trainer B não pode criar avaliação para cliente de Trainer A., Não é possível criar avaliação para cliente arquivado. (+6 more)

### Community 31 - "AssessmentPage.jsx"
Cohesion: 0.05
Nodes (42): API Reference, Authentication, Backend, Backend Issues, Backend (Render.com), Backend Setup, Backend Setup, Backend Tests (+34 more)

### Community 32 - "axiosConfig.js"
Cohesion: 0.15
Nodes (8): applyBrandColor(), AuthContext, AuthProvider(), hexToHSL(), api, failedQueue, createBillingPortal(), createCheckout()

### Community 33 - "FastAPI"
Cohesion: 0.11
Nodes (14): health_check(), Session, Endpoint de health check para monitoramento.          Verifica:     - API est, Seed do superuser — garante que existe sempre uma conta de superuser na base de, Middleware para adicionar email ao rate limiting key. Extrai o email do body JS, Serviço de autenticação - login, logout, refresh com SEGURANÇA., Serviços de negócio relacionados a packs (pacotes de sessões).  Responsabilida, Serviço de signup de Personal Trainer com verificação de email. (+6 more)

### Community 34 - "ClientDetails.jsx"
Cohesion: 0.12
Nodes (29): getClient(), createPackType(), deletePackType(), getPackTypes(), updatePackType(), cancelSession(), completeSession(), createSession() (+21 more)

### Community 35 - "compilerOptions"
Cohesion: 0.09
Nodes (22): compilerOptions, allowImportingTsExtensions, isolatedModules, jsx, lib, module, moduleDetection, moduleResolution (+14 more)

### Community 36 - "set_password_via_invite"
Cohesion: 0.13
Nodes (20): generate_invite(), Request, Response, Session, Router do fluxo de convite de clientes.  Este router tem dois grupos de endpoi, Valida o token de convite.     Usado na página /invite/:token para mostrar o no, Cliente define a sua password usando o token de convite.     A password é defin, Gera link de convite para um cliente. (+12 more)

### Community 37 - "nutrition.py"
Cohesion: 0.14
Nodes (23): _assert_food_owner(), create_food(), create_meal_plan(), delete_food(), delete_meal_plan(), get_food(), get_meal_plan(), list_foods() (+15 more)

### Community 38 - "TestCalculateTMBAllFormulas"
Cohesion: 0.09
Nodes (12): As 3 fórmulas devem ter chaves distintas - sem duplicados, Testa os valores EXATOS da fórmula Harris-Benedict para homem.          Cálcul, Homem e mulher com os mesmos dados biométricos devem ter TMB diferentes., Verifica que uma activity_key inválida levanta um ValueError., Verifica que sex inválido não é tratado como female por engano., Parametrize: corre este mesmo teste para cada activity_key válida.         Em v, Agrupa todos os testes relacionados à função calculate_tmb_all_formulas., Verifica que a função devolve sempre exatamente 3 resultados,         um por fó (+4 more)

### Community 39 - "adminApi.js"
Cohesion: 0.14
Nodes (23): activateTrainer(), archiveGlobalSupplement(), createGlobalExercise(), createGlobalFood(), createGlobalSupplement(), deleteGlobalExercise(), deleteGlobalFood(), deleteGlobalSupplement() (+15 more)

### Community 40 - "dependencies"
Cohesion: 0.10
Nodes (21): axios, class-variance-authority, clsx, @emotion/react, framer-motion, dependencies, axios, class-variance-authority (+13 more)

### Community 41 - "Notification"
Cohesion: 0.15
Nodes (20): delete_notification(), dispatch_due_notifications(), get_notification_stats(), list_pending_notifications(), _parse_legacy_template_message(), PendingNotificationRead, BaseModel, Session (+12 more)

### Community 42 - "NutritionPage.jsx"
Cohesion: 0.17
Nodes (16): createUser(), signupTrainer(), archiveClient(), createClient(), unarchiveClient(), updateClient(), generateInvite(), purchasePack() (+8 more)

### Community 43 - "ClientLayout.jsx"
Cohesion: 0.07
Nodes (37): getPortalBranding(), bottomNavItems, isItemActive(), NavContent(), navItems, Avatar, AvatarFallback, AvatarImage (+29 more)

### Community 44 - "training.py"
Cohesion: 0.18
Nodes (19): ClientActivePlanCreate, ClientExerciseSetLogRead, ClientExerciseSetLogUpsertItem, ClientExerciseSetLogUpsertRequest, ClonePlanToClientCreate, ExerciseCreate, ExerciseUpdate, PlanDayExerciseCreate (+11 more)

### Community 45 - "StripeService"
Cohesion: 0.06
Nodes (34): create_billing_portal(), create_checkout_session(), _ensure_stripe_customer_and_subscription(), get_subscription(), _persist_stripe_ids(), Request, Session, Router de billing — dashboard de subscrição do trainer.  Endpoints:     GET (+26 more)

### Community 46 - "TestGetTierForCount"
Cohesion: 0.14
Nodes (11): Determina o tier correcto com base no n. de clientes activos.          Esta e, Testa o cálculo de tier com base no número de clientes activos., Zero clientes deve manter o trainer no tier FREE., Cinco clientes ainda deve manter o trainer no tier FREE., Seis clientes deve subir o trainer para o tier STARTER., Quarenta e nove clientes ainda deve pertencer ao tier STARTER., Cinquenta clientes deve subir o trainer para o tier PRO., Cem clientes deve continuar no tier PRO. (+3 more)

### Community 47 - "MyProfile.jsx"
Cohesion: 0.11
Nodes (10): Testes unitários para o sistema de autenticação.  Estes testes cobrem as funçõ, O payload do token deve conter o user_id correto no campo 'sub'., O payload do token deve conter o role correto., Se client_id for fornecido, deve estar presente no payload do token., Trainer tokens não devem conter o claim 'cid'., O token deve conter uma data de expiração (claim 'exp')., Tokens para trainers e clients devem ser diferentes mesmo com o mesmo user_id., Testa a criação e verificação de tokens JWT (+2 more)

### Community 48 - "get_activity_factor_options"
Cohesion: 0.19
Nodes (9): list_activity_factors(), get_activity_factor_options(), Devolve a lista de fatores de atividade para popular um select no frontend., Testes para as opções de fatores de atividade., Verifica que a função devolve exatamente 5 opções de fatores de atividade., Verifica que cada opção devolvida tem os campos 'key', 'label' e 'factor'., As chaves devolvidas devem corresponder exatamente ao dicionário         ACTIVI, Verifica que cada opção corresponde ao valor em ACTIVITY_FACTORS. (+1 more)

### Community 49 - "PackType"
Cohesion: 0.12
Nodes (17): Admin, API Reference, Assessments, Authentication, Billing, Check-ins, Client Portal, Clients (+9 more)

### Community 50 - "client.py"
Cohesion: 0.12
Nodes (15): ActivePackInfo, ClientCreate, ClientRead, ClientUpdate, BaseModel, SQLModel, Schemas Pydantic para criação, atualização e leitura de clientes., Payload para criação de um novo cliente.     aqui validamos formato; regras de (+7 more)

### Community 51 - "calculate_macros_from_grams_per_kg"
Cohesion: 0.15
Nodes (11): calculate_macros_from_grams_per_kg(), Calcula macros a partir de rácios g/kg de peso corporal.      Não existe valid, Testes para cálculo de macros a partir de rácios por kg corporal., Verifica o cálculo com valores conhecidos.          Cliente 97kg com rácios:, Verifica que o kcal_total devolvido é consistente com as gramas calculadas., Verifica que o g/kg devolvido é igual ao input fornecido., Verifica que um rácio negativo levanta um ValueError., Verifica que peso zero é rejeitado no cálculo por g/kg. (+3 more)

### Community 52 - "calculate_macros_from_percentages"
Cohesion: 0.15
Nodes (11): calculate_macros_from_percentages(), Calcula a quantidade de macros em gramas a partir de um objetivo calórico e perc, Testes para cálculo de macros a partir de percentagens., Verifica o cálculo com valores conhecidos.          Cálculo manual:, O kcal_total devolvido deve ser consistente com as gramas calculadas.         F, Verifica que o g/kg é calculado corretamente.         g_por_kg = gramas / peso_, Verifica que percentagens que não somam 100% levantam um ValueError., Verifica que objetivo calórico zero é rejeitado. (+3 more)

### Community 53 - "compilerOptions"
Cohesion: 0.11
Nodes (17): compilerOptions, allowImportingTsExtensions, isolatedModules, lib, module, moduleDetection, moduleResolution, noEmit (+9 more)

### Community 54 - "InitialAssessmentRead"
Cohesion: 0.10
Nodes (30): create_assessment(), get_assessment(), list_assessments_by_client(), Session, Router de avaliações iniciais — InitialAssessment.   Estes endpoints gerem o f, update_assessment(), HealthQuestionnaire, InitialAssessmentCreate (+22 more)

### Community 55 - "utc_now"
Cohesion: 0.18
Nodes (16): _active_to_read(), close_active_plan(), get_active_plan(), Converte ClientActivePlan em schema de resposta.     Busca automaticamente os d, set_active_plan(), ClientActivePlanRead, Modelo para leitura de plano ativo do cliente.     Inclui informações do client, ClientActivePlan (+8 more)

### Community 56 - "InitialAssessment"
Cohesion: 0.14
Nodes (13): Architecture, Background Jobs, Features, License, Overview, Project Structure, PT Manager — Backend API, Subscription Status Flow (+5 more)

### Community 57 - ".generate_invite"
Cohesion: 0.33
Nodes (10): create_plan_day(), day_to_read(), list_plan_days(), update_plan_day(), TrainingPlanDayRead, ClientExerciseSetLog, SQLModel, Carga real executada pelo cliente em cada série de um exercício.     Permite co (+2 more)

### Community 58 - "billing.py"
Cohesion: 0.18
Nodes (11): Food, Catálogo de alimentos com macros por 100g.      Formula de kcal: (carbs*4 + pr, _calculate_item_macros(), create_food(), get_food_by_id(), list_foods(), Calcula os macros de um item do plano alimentar baseado no alimento e quantidade, Lista alimentos visíveis para o Personal Trainer:         - Alimentos globais ( (+3 more)

### Community 59 - "assessment.py"
Cohesion: 0.17
Nodes (13): AssessmentCreate, AssessmentRead, MeasurementCreate, MeasurementRead, PhotoCreate, PhotoRead, BaseModel, QuestionnaireData (+5 more)

### Community 60 - "NotificationService"
Cohesion: 0.16
Nodes (14): NotificationChannel, NotificationStatus, Enum, str, Canais suportados para envio., Tipo de destinatário., Estados do ciclo de vida da notificação., RecipientType (+6 more)

### Community 61 - "TrainerSubscription"
Cohesion: 0.15
Nodes (14): SQLModel, Modelo de subscrição do trainer.  Cada trainer tem exactamente uma linha nesta, Estados possíveis de uma subscrição de trainer., Tiers de preço com base no número de clientes ativos., Modelo de subscrição do trainer.      Relação 1:1 com User (role="trainer")., SubscriptionStatus, SubscriptionTier, TrainerSubscription (+6 more)

### Community 62 - "EmailService"
Cohesion: 0.33
Nodes (9): addExerciseToDay(), createPlanDay(), deleteDayExercise(), deletePlanDay(), updateDayExercise(), PlanDaysList(), AccordionContent, AccordionItem (+1 more)

### Community 63 - "TestHasActiveAccess"
Cohesion: 0.17
Nodes (9): Verifica se o trainer tem acesso activo a plataforma.          Retorna False s, Testa a verificação de acesso activo à plataforma., Sem subscrição, o trainer não tem acesso., Subscrição activa concede acesso., Subscrição em trial válido concede acesso., Subscrição em trial expirado não concede acesso., Subscrição cancelada não concede acesso., Subscrição PAST_DUE ainda concede acesso durante o grace period. (+1 more)

### Community 64 - "security.py"
Cohesion: 0.13
Nodes (15): get_current_user(), Session, Módulo de segurança — autenticação, autorização e guards de acesso.  Camadas d, Valida o JWT Bearer e devolve o utilizador autenticado da base de dados., Garante que o utilizador autenticado é um superuser., Garante Personal Trainer ou superuser (sem verificar subscricao)., Personal Trainer com subscrição activa, superuser, ou isento de billing., Garante que o utilizador autenticado é um cliente. (+7 more)

### Community 65 - ".log_error"
Cohesion: 0.18
Nodes (12): ErrorCategory, ErrorLog, ErrorTracker, BaseModel, Enum, Exception, str, Centraliza logging estruturado de erros para debug. (+4 more)

### Community 66 - "chart.tsx"
Cohesion: 0.14
Nodes (11): react, useCarousel(), ChartConfig, ChartContainer, ChartContext, ChartContextProps, ChartLegendContent, ChartTooltipContent (+3 more)

### Community 67 - "SupplementPage.jsx"
Cohesion: 0.24
Nodes (6): Testes da validação pública de tokens de convite., Auxiliar: cria token de convite directamente na BD., GET /invite/validate/{token} com token valido devolve valid=True e nome., Token que não existe na BD deve devolver valid=False., Token com expires_at no passado deve devolver valid=False., TestValidateToken

### Community 68 - "Exercise"
Cohesion: 0.36
Nodes (12): _assert_owner(), create_exercise(), delete_exercise(), list_exercises(), Session, Router de exercícios — catálogo global + privado por trainer.   Regras de negó, Verifica se o exercício pertence ao trainer autenticado.     Levanta 403 se não, _to_read() (+4 more)

### Community 69 - "ClientRepository"
Cohesion: 0.19
Nodes (8): ClientRepository, Session, Repository para operações de Client (perfil de aluno) na base de dados., Abstrai acesso a dados de Client (perfil de aluno).      Segurança: Usa soft d, Busca Client ativo pelo seu id., Cria novo Client (perfil de aluno) para um Personal Trainer.          Args:, Busca todos os clientes ativos de um trainer.          Exclui clientes arquiva, Soft delete de um cliente (marca como arquivado).          Args:

### Community 70 - "calculate_tmb_all_formulas"
Cohesion: 0.24
Nodes (12): calculate_tmb_all_formulas(), _harris_benedict(), _mifflin_st_jeor(), Serviço de cálculo de macros e Taxa Metabólica Basal (TMB).  Este módulo é PUR, Mifflin-St Jeor (1990) — considerada a mais precisa para a população geral., Fórmula de Waldemar.      Variante implementada (usada em contexto PT/BR despo, Calcula a TMB e o TDEE pelas 3 fórmulas.      Parâmetros:         weight_kg, Resultado de uma fórmula TMB.     frozen=True torna o objeto imutável após cria (+4 more)

### Community 71 - "carousel.tsx"
Cohesion: 0.15
Nodes (12): Carousel, CarouselApi, CarouselContent, CarouselContext, CarouselContextProps, CarouselItem, CarouselNext, CarouselOptions (+4 more)

### Community 72 - "ActiveTokenRepository"
Cohesion: 0.16
Nodes (13): ActiveToken, SQLModel, Modelo para tokens de autenticação ativos.  Cada utilizador autenticado possui, Tokens de sessão ativos.      Segurança: Guarda HASH do JWT, não o JWT complet, ActiveTokenRepository, datetime, Session, Repositorio para Active Tokens com hash seguro. (+5 more)

### Community 73 - "test_macro_calculator.py"
Cohesion: 0.33
Nodes (5): female_client(), male_athlete(), Testes unitários para os cálculos de TMB, TDEE e macros nutricionais., Dados típicos de um atleta masculino para testes., Dados típicos de uma cliente feminina para testes.

### Community 74 - "devDependencies"
Cohesion: 0.18
Nodes (11): eslint, devDependencies, eslint, postcss, tailwindcss, @types/react-dom, vite, postcss (+3 more)

### Community 75 - "drawer.tsx"
Cohesion: 0.25
Nodes (6): DrawerContent, DrawerDescription, DrawerFooter(), DrawerHeader(), DrawerOverlay, DrawerTitle

### Community 76 - "seed_catalogue"
Cohesion: 0.29
Nodes (10): Session, Seed do catálogo global — exercícios, alimentos e suplementos pré-definidos., Seed idempotente do catálogo global.       Verifica por nome antes de inserir, Insere exercícios globais se não existirem (owner_trainer_id = None)., Insere alimentos globais se não existirem (owner_trainer_id = None)., Insere suplementos globais se não existirem (created_by_user_id = None)., seed_catalogue(), _seed_exercises() (+2 more)

### Community 77 - "BillingPage.jsx"
Cohesion: 0.50
Nodes (4): PackConsumption, SQLModel, Modelos de base de dados para sessões de treino e consumo de packs.   Training, Registo de consumo de uma sessão num pack do cliente.       Criado automaticam

### Community 78 - "App.jsx"
Cohesion: 0.40
Nodes (5): Environment Variables, Getting Started, Prerequisites, Running Locally, Running with Docker Compose

### Community 79 - "lifespan"
Cohesion: 0.25
Nodes (7): Session, Seed idempotente:     - Garante PackTypes 2/4/6/8 aulas     - Atualiza nome/is, seed_pack_types(), lifespan(), Startup hooks:     - Inicializa a base de dados     - Executa as migrations, Desliga o scheduler graciosamente.      Chamado no hook on_shutdown do FastAPI, shutdown_scheduler()

### Community 80 - "main.py"
Cohesion: 0.24
Nodes (9): global_exception_handler(), Exception, Request, _rate_limit_exceeded_handler(), Ponto de entrada da aplicação FastAPI — app factory e configuração global.  Re, Handler para o Rate Limit Exceeded, Captura qualquer excepção não tratada e devolve uma resposta 500 genérica., JSONResponse (+1 more)

### Community 81 - "scheduler.py"
Cohesion: 0.40
Nodes (5): Scheduler de notificacoes — APScheduler em background.  Dois jobs:   1. dispa, Exclui as linhas expiradas de active_tokens.      Esta é uma limpeza adicional, Inicia o scheduler APScheduler em background.      Chamado no hook on_startup, start_scheduler(), token_cleanup_job()

### Community 82 - "exercisesApi.js"
Cohesion: 0.36
Nodes (6): createExercise(), deleteExercise(), getExercises(), updateExercise(), useExercises(), Exercises()

### Community 83 - "context-menu.tsx"
Cohesion: 0.20
Nodes (9): ContextMenuCheckboxItem, ContextMenuContent, ContextMenuItem, ContextMenuLabel, ContextMenuRadioItem, ContextMenuSeparator, ContextMenuShortcut(), ContextMenuSubContent (+1 more)

### Community 84 - "training_plans.py"
Cohesion: 0.44
Nodes (8): clone_template_to_client(), create_plan(), list_plans(), _plan_to_read(), update_plan(), TrainingPlanRead, Plano de treino completo. Pode ser:     - Template (client_id = None): modelo r, TrainingPlan

### Community 85 - "run_migrations"
Cohesion: 0.25
Nodes (7): Split SQL into individual statements, handling:     - Single-line comments (--), Executa migrações SQL em ordem, com rastreamento via schema_migrations.     - C, run_migrations(), main(), migrate_runner.py — Script standalone para execução de migrations.   Por que e, Ponto de entrada do runner de migrations.       Retorna:         0 — todas as, _split_sql_statements()

### Community 86 - "SignupService"
Cohesion: 0.31
Nodes (6): Session, Orquestra signup de novo Personal Trainer., Reenvia email de verificação para trainer (sem revelar estado da conta)., Gera token de verificacao + SHA256 hash.          Returns:             (token, Signup de novo Personal Trainer com email verification obrigatório.          F, SignupService

### Community 87 - "handlers.js"
Cohesion: 0.40
Nodes (4): Alert, AlertDescription, AlertTitle, alertVariants

### Community 88 - "_build_adherence"
Cohesion: 0.50
Nodes (4): MacroSummary, Resumo de macros calculado via query agregada.     Não corresponde a nenhuma ta, Agrega uma lista de macros (dicts ou MacroSummary) num único MacroSummary., _sum_macros()

### Community 89 - "pack_types.py"
Cohesion: 0.50
Nodes (3): init_db(), Inicialização da base de dados.  Responsabilidades:     1. Importar todos os, Cria as tabelas na BD caso não existam     Em produto, isto é substituido por m

### Community 90 - "SubscriptionRead"
Cohesion: 0.50
Nodes (4): Authentication & Authorization, JWT Flow, Roles, Subscription-Gated Access

### Community 91 - "ClientFirstLoginPage.jsx"
Cohesion: 0.52
Nodes (5): setPasswordViaInvite(), validateInvite(), ClientFirstLoginPage(), renderInvitePage(), renderWithValidToken()

### Community 92 - "training_session.py"
Cohesion: 0.43
Nodes (6): SQLModel, Atualização parcial de uma sessão de treino., Agendar uma sessão de treino individual., TrainingSessionCreate, TrainingSessionRead, TrainingSessionUpdate

### Community 93 - "rate_limit.py"
Cohesion: 0.29
Nodes (6): build_email_ip_key(), Request, RateLimitConfig, Configuração centralizada de rate limiting.  Todos os routers importam daqui:, Combinação de IP + email para rate limiting.     Protege contra brute-force em, Limites de rate limiting para todos os endpoints.

### Community 94 - "get_session"
Cohesion: 0.29
Nodes (6): _get_connect_args(), get_session(), Session, Configuração de base de dados e factory de sessões.  Responsabilidades:   - C, Retorna argumentos de conexão específicos para cada tipo de base de dados., Dependency FastAPI que fornece uma sessão de base de dados por request.      C

### Community 95 - "scripts"
Cohesion: 0.29
Nodes (7): scripts, build, dev, lint, preview, test, test:coverage

### Community 96 - "ClientPackPurchase"
Cohesion: 0.50
Nodes (4): Automatic Tier Management, Stripe Integration, Trainer Registration Flow, Webhooks Handled

### Community 97 - "RateLimitEmailMiddleware"
Cohesion: 0.33
Nodes (5): Request, RateLimitEmailMiddleware, Middleware que extrai email do body JSON para rate limiting., Intercepta POSTs de auth para extrair email e armazenar em request.scope., BaseHTTPMiddleware

### Community 98 - "Settings"
Cohesion: 0.40
Nodes (4): Configuração central da aplicação via variáveis de ambiente., Configurações da aplicação      database_url:     - SQlite local: "sqlite:///, Settings, BaseSettings

### Community 99 - "seed_demo_data"
Cohesion: 0.40
Nodes (4): Session, Seed de demonstração — cria apenas o trainer de teste.   O que é criado:, Cria o trainer de demonstração se ainda não existirem.      Só corre se SEED_D, seed_demo_data()

### Community 100 - ".cancel_pending_reminders_for_session"
Cohesion: 0.50
Nodes (4): Foods, Macro Calculator, Meal Plans, Nutrition

### Community 101 - "package.json"
Cohesion: 0.40
Nodes (4): name, private, type, version

### Community 102 - "_build_macro_grams"
Cohesion: 0.50
Nodes (4): _build_macro_grams(), MacroGrams, Calcula todas as métricas derivadas a partir das gramas base.     Chamado inter, Quantidades de macronutrientes em gramas.

### Community 103 - "subscription_service.py"
Cohesion: 0.67
Nodes (3): Core Models, Database Schema, Migration History

### Community 137 - "tailwindcss"
Cohesion: 0.67
Nodes (3): Deployment, Docker (Self-hosted), Render.com (Production)

## Knowledge Gaps
- **276 isolated node(s):** `pt-manager`, `name`, `private`, `version`, `type` (+271 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **46 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `commit_or_rollback()` connect `PlanDayExercise` to `Supplement`, `User`, `AdminService`, `client_portal.py`, `TrainerSettings`, `utc_now_datetime`, `commit_or_rollback`, `ClientPack`, `CheckInRead`, `FastAPI`, `nutrition.py`, `Notification`, `StripeService`, `InitialAssessmentRead`, `utc_now`, `.generate_invite`, `Exercise`, `ClientRepository`, `ActiveTokenRepository`, `training_plans.py`, `SignupService`?**
  _High betweenness centrality (0.146) - this node is a cross-community bridge._
- **Why does `Client` connect `commit_or_rollback` to `Supplement`, `User`, `auth.py`, `timedelta`, `nutrition.py`, `ClientRepository`, `TestDefinePasswordAndAutoLogin`, `AdminService`, `client_portal.py`, `TrainingSession`, `utc_now_datetime`, `training_plans.py`, `InitialAssessmentRead`, `utc_now`, `ClientPack`, `CheckInRead`?**
  _High betweenness centrality (0.068) - this node is a cross-community bridge._
- **Why does `calculate_macros()` connect `nutrition.py` to `nutrition.py`, `calculate_tmb_all_formulas`, `get_activity_factor_options`, `calculate_macros_from_grams_per_kg`, `calculate_macros_from_percentages`?**
  _High betweenness centrality (0.063) - this node is a cross-community bridge._
- **Are the 81 inferred relationships involving `commit_or_rollback()` (e.g. with `create_assessment()` and `update_assessment()`) actually correct?**
  _`commit_or_rollback()` has 81 INFERRED edges - model-reasoned connections that need verification._
- **Are the 35 inferred relationships involving `Client` (e.g. with `create_assessment()` and `get_assessment()`) actually correct?**
  _`Client` has 35 INFERRED edges - model-reasoned connections that need verification._
- **Are the 8 inferred relationships involving `User` (e.g. with `get_portal_branding()` and `_handle_trial_will_end()`) actually correct?**
  _`User` has 8 INFERRED edges - model-reasoned connections that need verification._
- **What connects `Router de administração - apenas superusers.  O superuser tem visibilidade glo`, `Devolve métricas globais da plataforma para o superuser.`, `Lista todos os Personal Trainers com o estado das suas subscrições.     Permite` to the rest of the system?**
  _890 weakly-connected nodes found - possible documentation gaps or missing edges._