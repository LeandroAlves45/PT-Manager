# PT Manager — Sprint Roadmap (12 semanas)

*Planeamento Detalhado — Julho 2026 — alinhado com `00_ARCHITECTURE.md` v3.0*

---

## Overview

Migração Backend: Python (FastAPI + SQLModel) → C# (.NET 10 + EF Core), modular monolith com Clean Architecture organizada por feature (ver `00_ARCHITECTURE.md §2`).

**Timeline:** 12 semanas (3 meses)
**Entrega:** Backend MVP em Render (free tier) com Neon PostgreSQL, Upstash Redis, Upstash QStash, Resend, Stripe, Cloudinary
**Fora do MVP:** RabbitMQ/MassTransit, AutoMapper, MediatR, `IRepository<T>` genérico, PostgreSQL RLS (ver `00_ARCHITECTURE.md §17`)

---

## SPRINT 0: Setup + Decisões (Semana 0 — 3 dias)

### Objectivo
Preparação técnica. Zero código novo de domínio.

### Tarefas

1. **Repositório Setup**
   - Estrutura no monorepo: `backend/` com `PTManager.sln`, `Directory.Build.props`, `Directory.Packages.props` (central package management)
   - Confirmar que o `.gitignore` não ignora `*.sln`; manter `docs/` deliberadamente ignorado
   - `backend-python/` confirmado fora do Git, mantido só como referência local

2. **Ambiente Local**
   - .NET 10 SDK instalado
   - Visual Studio 2026 ou VS Code + C# Dev Kit
   - PostgreSQL 17 local (ou Neon branch de teste)
   - Docker (para Testcontainers)
   - Conta Upstash (Redis + QStash) em modo dev/free

3. **Setup Inicial**
   ```bash
   dotnet new sln -n PTManager --format sln
   dotnet new classlib -n Domain -o src/Domain
   dotnet new classlib -n Application -o src/Application
   dotnet new classlib -n Infrastructure -o src/Infrastructure
   dotnet new webapi -n Api -o src/Api --use-controllers
   dotnet new xunit -n Domain.UnitTests -o tests/Domain.UnitTests
   dotnet new xunit -n Application.UnitTests -o tests/Application.UnitTests
   dotnet new xunit -n Infrastructure.IntegrationTests -o tests/Infrastructure.IntegrationTests
   dotnet new xunit -n Api.FunctionalTests -o tests/Api.FunctionalTests
   dotnet new xunit -n ArchitectureTests -o tests/ArchitectureTests
   ```
   - Referências de projeto conforme `00_ARCHITECTURE.md §2.2` (Domain não depende de nada; Application depende de Domain; Infrastructure depende de Application+Domain; Api depende de Application e referencia Infrastructure só no composition root)

4. **Decisões já tomadas (não reabrir sem novo sinal, ver `00_ARCHITECTURE.md §17`)**
   - ✓ ASP.NET Core Controllers (não Minimal APIs)
   - ✓ Sem RabbitMQ/MassTransit — Upstash QStash + `durable_jobs`/`outbox_messages` em Postgres
   - ✓ Sem AutoMapper/MediatR — mapping e dispatch explícitos
   - ✓ Sem `IRepository<T>` genérico — portas específicas por caso de uso
   - ✓ `users` própria com stores customizados do Identity (não o schema padrão)

5. **CI/CD Stub**
   - `.github/workflows/ci.yml` (workflow manual válido, global ao monorepo, sem pipeline real)
   - `.github/workflows/deploy.yml` (workflow manual válido, sem deploy real ou secrets)

### Deliverables
- ✓ `backend/` estruturado, 4 projetos de produção + 5 projetos de teste compilando vazios
- ✓ `.gitignore` confirmado para permitir `*.sln` e manter `docs/` local
- ✓ GitHub Actions workflows stub com YAML válido
- ✓ `Directory.Packages.props` criado apenas com packages consumidos no Sprint 0

### Commits
- `chore: initialize C# backend structure`
- `ci: add GitHub Actions stubs`
- `fix: allow .sln files in gitignore`

---

## SPRINT 1: Domain Layer (Semanas 1-2)

### Objectivo
Portar entidades Python → C# Entities + Value Objects, sem abstrações genéricas de persistência.

### Tarefas

1. **Entities (`Domain/Entities/`)**, organizadas por pasta de feature, não uma pasta plana:
   ```
   Identity/User.cs, RefreshToken.cs
   Clients/Client.cs
   Nutrition/MealPlan.cs, MealPlanMeal.cs, MealPlanMealItem.cs, MealPlanMealSupplement.cs, Food.cs
   Training/TrainingPlan.cs, TrainingPlanDay.cs, TrainingPlanDayExercise.cs, ExerciseSet.cs, ClientExerciseSetLog.cs, Exercise.cs
   Sessions/Session.cs
   Assessments/InitialAssessment.cs, CheckIn.cs
   Supplements/Supplement.cs, ClientSupplementAssignment.cs
   Billing/TrainerSubscription.cs, PackType.cs, ClientSessionPack.cs, ProcessedStripeEvent.cs
   Notifications/Notification.cs
   Jobs/DurableJob.cs, OutboxMessage.cs
   TrainerSettings/TrainerSettings.cs
   ```
   - IDs: `Guid`, gerado no construtor da entidade (`Guid.NewGuid()`), nunca pelo Postgres — ver `01_DATABASE_SCHEMA.md` Decisão 1
   - Timestamps: sempre `DateTime.UtcNow`, propriedade `IClock` injetada onde a testabilidade importa (evita `DateTime.UtcNow` espalhado e não mockável)
   - Soft delete: `IsDeleted` em todas as entidades tenant-owned
   - Multi-tenancy: `OwnerTrainerId` como propriedade de instância, nunca resolvida via `HttpContext` dentro da entidade

2. **ValueObjects (`Domain/ValueObjects/`)**
   ```
   MacroSummary.cs           (protein_g, carbs_g, fats_g, kcal)
   SubscriptionStatus.cs     (ACTIVE, INACTIVE, SUSPENDED, CANCELLED)
   SubscriptionTier.cs       (FREE, STARTER, PRO)
   EmailAddress.cs           (validação de formato)
   JobStatus.cs              (Pending, Processing, Completed, Failed, DeadLetter)
   ```

3. **Portas específicas (`Domain/Interfaces/`)** — sem `IRepository<T>` genérico nem `IUnitOfWork` catch-all:
   ```
   ITenantContext.cs         (trainer efetivo, utilizador, role, origem, flag administrativa)
   IClock.cs
   Repositórios/query services específicos, um por agregado que precisa (ex. IMealPlanQueries, IClientWriter)
   ```

4. **Testes Unitários (`Domain.UnitTests/`)**
   - Entidades: criação, invariantes (ex. `starts_date <= ends_date`)
   - Value Objects: `MacroSummaryTests`, `SubscriptionStatusTests`, `EmailAddressTests`
   - ~20 testes, sem mocks (Domain não tem dependências externas)

### Deliverables
- ✓ `Domain` compila sem erros e sem depender de EF Core, ASP.NET ou qualquer package de infraestrutura
- ✓ Entidades e Value Objects portados, organizados por feature
- ✓ Portas específicas definidas (sem genéricos)
- ✓ ~20 testes unitários passam

### Commits
- `feat: add domain entities and value objects organized by feature`
- `test: add domain unit tests`

---

## SPRINT 2: Infrastructure + EF Core (Semanas 3-4)

### Objectivo
DbContext, migration inicial e persistência especializada de jobs e outbox.

### Tarefas

#### Gate obrigatório antes da `InitialCreate`

As decisões seguintes estão aprovadas no planeamento. A `InitialCreate` continua
bloqueada até o Domain, a Infrastructure e os testes correspondentes serem
implementados:

1. **Integridade cross-tenant nas escritas**
   - Definir constraints e FKs compostas, ou uma proteção equivalente, para
     impedir que `owner_trainer_id` do trainer A seja combinado com recursos do
     trainer B.
   - Cobrir pelo menos `meal_plans`, `training_plans`,
     `initial_assessments`, `checkins`, `client_supplement_assignments`, `sessions`,
     `client_session_packs` e `notifications`.
   - Definir validação de entidades adicionadas ou modificadas antes de
     `SaveChanges`. Global Query Filters protegem leituras, não escritas
     (`00_ARCHITECTURE.md §6.3`).

2. **Owner e renovação do lease de jobs**
   - Persistir identidade do owner do lease, duração e expiração.
   - Definir claim condicional, renovação antes da expiração e recuperação de
     jobs presos em `Processing`.
   - Alinhar `DurableJob`, schema e repository antes da migration
     (`00_ARCHITECTURE.md §9.4`).

3. **Recuperação da outbox após crash**
   - Definir claim persistido, lease ou mecanismo equivalente para recuperar
     mensagens que fiquem em `dispatched` quando o processo termina antes de
     `completed`.
   - Definir retry, idempotência, estado terminal e timestamps necessários.
   - Alinhar `OutboxMessage`, schema e repository antes da migration
     (`00_ARCHITECTURE.md §10.3`).

4. **Revisão final do modelo**
   - Confirmar nullability, defaults, limites, índices e delete behaviors do
     schema contra o Domain.
   - Atualizar `01_DATABASE_SCHEMA.md` e o código afetado antes de executar
     `dotnet ef migrations add InitialCreate`.
   - Aplicar o pacote vinculativo
     `docs/backend-files/sprint_2_every/sprint_2_newTables/`, incluindo Domain,
     28 DbSets/configurações, interceptor, testes unitários e Testcontainers.
   - Confirmar 28 tabelas da aplicação e `__EFMigrationsHistory`, total 29.
   - Corrigir `Client`, `InitialAssessment`, `Food` e `MealPlan` e implementar o
     núcleo puro de cálculo nutricional com testes unitários e de metadata.
   - Manter a migration bloqueada até existirem 28 entidades mapeadas, metadata
     verde e testes PostgreSQL de isolamento, constraints, jobs e outbox.
   - Diferir handlers, preview, persistência por casos de uso e endpoints para o
     Sprint 3.

5. **Limite do Sprint 2**
   - Não implementar neste gate handlers, DTOs, Controllers ou frontend para os
     contratos alterados.
   - Tratar `starts_at`, estados de sessão, dados de Client, `kcal_target`,
     snapshots de packs e atribuições de suplementos no Sprint 3.

Não gerar `InitialCreate` enquanto a implementação e os testes destes quatro
pontos não estiverem concluídos.

1. **Packages** (via `Directory.Packages.props`):
   - `Microsoft.EntityFrameworkCore` 10.0
   - `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0
   - `Microsoft.EntityFrameworkCore.Design` 10.0

2. **DbContext (`Infrastructure/Data/PtManagerDbContext.cs`)**
   - `DbSet<T>` para todas as entities de `01_DATABASE_SCHEMA.md`
   - Fluent API configuration por entidade para mapping, constraints, índices e relações
   - Global Query Filters centralizados no `PtManagerDbContext`, resolvidos a partir de `ITenantContext` scoped, com `CurrentTrainerId.HasValue` e filtros derivados nas filhas
   - `TenantWriteValidationInterceptor`: validação com I/O em `SavingChangesAsync`; `SaveChanges()` síncrono falha explicitamente
   - Lazy loading desativado

3. **Migration inicial**
   ```bash
   dotnet tool run dotnet-ef migrations add InitialCreate --project src/Infrastructure/Infrastructure.csproj --startup-project src/Api/Api.csproj --output-dir Data/Migrations
   dotnet tool run dotnet-ef database update --project src/Infrastructure/Infrastructure.csproj --startup-project src/Api/Api.csproj
   ```
   - Verificar 28 tabelas da aplicação mais `__EFMigrationsHistory`, total 29
   - Verificar constraints, FKs compostas, nullability, defaults, delete
     behaviors e índices, incluindo os GIN de pesquisa
   - Inspecionar o código gerado da migration antes de a aplicar, sem editar
     manualmente a migration

4. **Persistência especializada (`Infrastructure/Persistence/`)**
   - Implementar apenas `DurableJobRepository` e `OutboxRepository`, necessários aos gates
   - Claim em transação curta com `SELECT ... FOR UPDATE SKIP LOCKED`
   - Renovação, conclusão e falha condicionadas por estado, token e lease ativo
   - Diferir os outros nove repositórios para o Sprint 3, quando existirem consumidores concretos

5. **Testes Integração (`Infrastructure.IntegrationTests/`)**
   - Testcontainers PostgreSQL (spin-up automático)
   - Persistência de jobs e outbox: claim, renovação, conclusão, falha e recuperação
   - Teste dedicado ao Global Query Filter: trainer A não vê dados de trainer B mesmo sem filtro explícito na query
   - Testes negativos de escrita: trainer A não cria nem associa registos a
     clientes ou recursos do trainer B, mesmo com IDs manipulados
   - Testes concorrentes de jobs: claim único, token correto, recuperação e
     rejeição de renovação/conclusão depois da expiração
   - Testes de outbox: recuperação após crash entre `dispatched` e `completed`,
     retry e idempotência
   - ~30-40 testes

### Deliverables
- ✓ `Infrastructure` compila
- ✓ DbContext funciona contra PostgreSQL (local + Neon)
- ✓ Migration `InitialCreate` aplicada com sucesso, 29 tabelas confirmadas
- ✓ Gate pré-`InitialCreate` fechado e registado nas fontes canónicas
- ✓ Repositórios específicos implementados
- ✓ ~40 testes integração passam, incluindo isolamento multi-tenant em leituras
  e escritas, leases de jobs e recuperação da outbox

### Commits
- `feat: add EF Core DbContext with global query filters`
- `feat: implement feature-specific repositories`
- `test: add repository and multi-tenant isolation tests`

---

## SPRINT 3: Application Layer (Semanas 5-6)

### Objectivo
Handlers explícitos por caso de uso, DTOs, validação, mapping manual.

### Tarefas

1. **Packages**:
   - `FluentValidation` (core, sem `.AspNetCore` — validação chamada explicitamente pelos handlers, ver `00_ARCHITECTURE.md §14`)

2. **Handlers (`Application/Features/<Feature>/`)** — um por caso de uso, não um "Service" genérico com dez métodos:
   ```
   Clients/CreateClientHandler.cs, UpdateClientHandler.cs, ArchiveClientHandler.cs
   Nutrition/CreateMealPlanHandler.cs, ...
   Training/CreateTrainingPlanHandler.cs, ...
   Billing/CreateCheckoutSessionHandler.cs, ProcessStripeWebhookHandler.cs
   Notifications/EnqueueNotificationHandler.cs
   Authentication/LoginHandler.cs, RefreshTokenHandler.cs, SignupHandler.cs
   ```
   - Cada handler recebe um DTO de entrada, chama as portas necessárias (repositórios, `IEmailSender`, `IPaymentGateway`, `ICacheService`), devolve `Result`/`Result<T>` (`00_ARCHITECTURE.md §4.3`)
   - Sem MediatR: os controllers chamam o handler diretamente via DI

3. **DTOs (`Application/Features/<Feature>/Dtos/`)** — junto ao handler que os usa, não numa pasta `DTOs/` global

4. **Validators (`Application/Features/<Feature>/Validators/`)**
   - `FluentValidation`, chamado explicitamente no handler (`validator.ValidateAsync`), incluindo regras assíncronas contra repositórios

5. **Mapping**
   - Métodos de extensão explícitos (`ToDto()`, `ToEntity()`) por feature — sem AutoMapper. Mais código, mas sem "magia" de reflection a debugar

6. **Exceptions (`Application/Common/Exceptions/`)**
   ```
   DomainException.cs
   ValidationException.cs
   ExternalServiceException.cs
   ```

7. **Testes Unitários (`Application.UnitTests/`)**
   - Um handler testado com doubles das portas (sem Testcontainers aqui)
   - Validators: casos válidos/inválidos
   - ~60 testes

### Deliverables
- ✓ `Application` compila, sem depender de Infrastructure
- ✓ Handlers implementados por feature
- ✓ DTOs e Validators colocados junto da feature
- ✓ Mapping explícito, sem AutoMapper
- ✓ ~60 testes unitários passam

### Commits
- `feat: add application handlers organized by feature`
- `feat: add DTOs and validators`
- `test: add unit tests for handlers and validators`

---

## SPRINT 4: API Controllers + Auth (Semanas 7-8)

### Objectivo
Endpoints HTTP, ASP.NET Core Identity, middleware, compatibilidade de contrato.

### Tarefas

1. **Packages**:
   - `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0
   - Package concreto de ASP.NET Core Identity confirmado apenas depois de desenhar os custom stores da Decisão 2 em `01_DATABASE_SCHEMA.md`
   - `Microsoft.AspNetCore.OpenApi` já instalado no Sprint 0; não adicionar `Swashbuckle.AspNetCore` sem uma decisão arquitectural nova

2. **Program.cs (composition root)**
   - `AddApplication()`, `AddInfrastructure()` — sem tipos concretos de Infrastructure espalhados pela API
   - `Microsoft.AspNetCore.RateLimiting` (nativo, sem package extra)
   - CORS: lista explícita de origens Vercel (produção + preview deployments)
   - JWT + cookie de refresh token conforme `00_ARCHITECTURE.md §5.2`

3. **Middlewares (`Api/Middlewares/`)**
   ```
   ExceptionHandlingMiddleware.cs   (Result/exceção → Problem Details, nunca stack trace)
   CorrelationIdMiddleware.cs
   ```

4. **Controllers (`Api/Controllers/`)** — finos, sem EF Core nem regras de negócio, um handler por ação:
   ```
   AuthController          /auth/login, /auth/signup, /auth/logout, /auth/refresh
   ClientsController       /clients CRUD
   MealPlansController     /meal-plans CRUD
   TrainingPlansController /training-plans CRUD
   ClientPortalController  /portal/my-plan, /portal/my-nutrition, /portal/branding, /portal/my-profile
   AssessmentsController   /assessments, /checkins
   SupplementsController   /supplements, /client-supplement-assignments
   SessionsController      /sessions
   AdminController         /admin/trainers, /admin/health (superuser only)
   InternalJobsController  /api/internal/jobs/dispatch (QStash, assinatura validada, sem auth de utilizador)
   ```

5. **Matriz de migração de contrato**
   - Antes de implementar cada controller, classificar os endpoints Python correspondentes como Preserve / Alias / Remove (`00_ARCHITECTURE.md §4.2`)
   - Contract tests cobrindo os casos duvidosos (trailing slash, PUT vs PATCH, prefixos de signup)

6. **Testes Integração (`Api.FunctionalTests/`)**
   - `WebApplicationFactory`: auth (login/signup/refresh/reuso de refresh token), CRUD multi-tenant, Problem Details, contract tests
   - ~40-50 testes

### Deliverables
- ✓ `Api` compila e corre localmente
- ✓ Controllers implementados, sem lógica de negócio nem EF Core direto
- ✓ Auth completo (login/signup/logout/refresh com rotação)
- ✓ Rate limiting e CORS configurados
- ✓ Matriz de migração de contrato preenchida
- ✓ ~50 testes integração passam
- ✓ Swagger/OpenAPI gerado

### Commits
- `feat: add API controllers and endpoints`
- `feat: add JWT authentication with refresh token rotation`
- `feat: add middleware stack and rate limiting`
- `test: add endpoint integration and contract tests`

---

## SPRINT 5: Jobs Duráveis, Outbox e Serviços Externos (Semana 9)

### Objectivo
Dispatcher de jobs ativado por QStash, outbox transacional, integrações externas. **Sem RabbitMQ/MassTransit** (ver `00_ARCHITECTURE.md §9`).

### Tarefas

1. **Dispatcher (`Infrastructure/Jobs/`)**
   ```
   JobDispatcher.cs          (reclama jobs vencidos com FOR UPDATE SKIP LOCKED, aplica lease, cria scope+TenantContext por job)
   OutboxDispatcher.cs       (entrega itens pendentes da outbox de forma idempotente)
   ```
   - Endpoint `POST /api/internal/jobs/dispatch`: valida assinatura QStash, limita tamanho do body, processa batch limitado, propaga correlation ID, não expõe detalhes na resposta
   - Entrega at-least-once — handlers de job devem ser idempotentes (chave `idempotency_key`)
   - Job em `Processing` cujo lease expira volta a ficar elegível (recuperação de falhas do processo)

2. **Handlers de job (`Application/Features/Jobs/`)**
   ```
   SendNotificationJobHandler.cs
   ProcessBillingJobHandler.cs
   ```

3. **External Services (`Infrastructure/ExternalServices/`)**
   ```
   ResendEmailSender.cs      implementa IEmailSender
   StripePaymentGateway.cs   implementa IPaymentGateway, idempotency key por operação de negócio
   CloudinaryMediaService.cs
   UpstashRedisCacheService.cs  implementa ICacheService sobre HybridCache + Upstash Redis
   ```
   - Todas com timeout e tratamento de erro transitório
   - Se um SDK não estiver ativamente mantido, preferir `HttpClient` tipado (`00_ARCHITECTURE.md §11`)

4. **Webhook Stripe**
   - Lê raw body, valida `Stripe-Signature`, deduplica via `processed_stripe_events`, escreve o outbox message na mesma transação (`00_ARCHITECTURE.md §10.2`)

5. **Configuration (`Program.cs`)**
   ```
   builder.Configuration["Stripe:SecretKey"]
   builder.Configuration["Resend:ApiKey"]
   builder.Configuration["Cloudinary:CloudName"]
   builder.Configuration["Upstash:RedisConnectionString"]
   builder.Configuration["Upstash:QStashSigningKey"]
   ```

6. **Testes**
   - Reclamação concorrente de jobs (dois dispatchers não processam o mesmo job)
   - Retry transitório, falha permanente → `dead_letter`
   - Assinatura QStash inválida rejeitada
   - Webhook Stripe: evento duplicado, fora de ordem, falha antes/depois do commit
   - Cache: falha de Redis não impede a operação principal (fallback documentado em `00_ARCHITECTURE.md §8.2`)
   - ~30 testes

### Deliverables
- ✓ Dispatcher de jobs e outbox funcionando contra Postgres real (Testcontainers)
- ✓ Endpoint interno validado por assinatura QStash
- ✓ 4 serviços externos integrados (Resend, Stripe, Cloudinary, Upstash Redis)
- ✓ Webhook Stripe idempotente e transacional
- ✓ ~30 testes passam

### Commits
- `feat: add durable job dispatcher and outbox pattern`
- `feat: add QStash-triggered internal dispatch endpoint`
- `feat: add external service integrations (Resend, Stripe, Cloudinary, Upstash Redis)`
- `test: add job dispatcher and webhook idempotency tests`

---

## SPRINT 6: Observabilidade (Semana 10)

### Objectivo
Logs estruturados, tracing, error tracking — adequados ao filesystem efémero do Render.

### Tarefas

1. **Packages**:
   - `Sentry.AspNetCore`
   - OpenTelemetry (`OpenTelemetry.Instrumentation.AspNetCore`, `.Http`, `.EntityFrameworkCore`)
   - **Sem** Serilog file sink — filesystem do container é efémero (`00_ARCHITECTURE.md §12.1`)

2. **Logging**
   - `ILogger<T>` em todos os handlers/serviços, mensagens estruturadas (`{ClientId}`, nunca interpolação de string)
   - Output JSON para console em produção
   - Redaction de passwords, tokens, cookies, API keys

3. **Correlation ID Middleware**
   - Gera se não existir, injeta em `LogContext`, propaga em headers de resposta

4. **Sentry + OpenTelemetry**
   - DSN e exporter confirmados no Sprint 0 dentro dos limites free tier
   - Instrumentação: ASP.NET Core, HttpClient, EF Core/Npgsql, jobs e integrações externas via Activities próprias

5. **Health Checks**
   ```
   GET /health/live    (processo responde, não consulta serviços externos)
   GET /health/ready    (confirma PostgreSQL; Redis/QStash/Stripe/Resend/Cloudinary não bloqueiam readiness)
   ```

6. **Métricas mínimas**
   - Latência/erros HTTP, duração de queries, pool de ligações, cache hit ratio, jobs pendentes/tentativas/dead-letter, webhooks Stripe duplicados/falhados, falhas de email

### Deliverables
- ✓ Logs estruturados em JSON, sem file sink
- ✓ Correlation IDs propagados
- ✓ Sentry + OpenTelemetry ativos
- ✓ `/health/live` e `/health/ready` distintos e operacionais
- ✓ ~10 testes passam

### Commits
- `feat: add structured logging and correlation IDs`
- `feat: add Sentry and OpenTelemetry instrumentation`
- `feat: add liveness and readiness health endpoints`

---

## SPRINT 7: Testing + CI/CD (Semana 11)

### Objectivo
Suite completa, incluindo testes de arquitetura, pipeline automatizado.

### Tarefas

1. **Unit Tests** — `Domain.UnitTests` + `Application.UnitTests`: ~80 testes (invariantes de domínio, handlers, validators)

2. **Integration/Functional Tests** — `Infrastructure.IntegrationTests` + `Api.FunctionalTests`: ~90 testes (repositórios, multi-tenant, endpoints, auth, Stripe, jobs)

3. **Architecture Tests (`ArchitectureTests/`)** — novo projeto dedicado (`00_ARCHITECTURE.md §15.7`), verifica automaticamente:
   - Domain não depende de Application/Infrastructure/Api
   - Application não depende de Infrastructure/Api
   - Controllers não usam `DbContext` diretamente
   - Infrastructure não expõe implementações concretas fora do composition root
   - Nenhuma feature usa `IgnoreQueryFilters` sem autorização explícita

4. **Test Coverage**
   - Target: 80%+, via Coverlet ou Microsoft Code Coverage (não OpenCover)

5. **GitHub Actions CI**
   ```yaml
   name: CI (Backend + Frontend)
   on: [push, pull_request]
   jobs:
     backend:
       steps:
         - uses: actions/setup-dotnet@v4
           with: { dotnet-version: '10.0.x' }
         - run: dotnet restore backend/
         - run: dotnet build backend/ --no-restore
         - run: dotnet test backend/ --no-build --collect:"XPlat Code Coverage"
     frontend:
       steps:
         - uses: actions/setup-node@v4
         - run: npm install && npm run test
   ```

### Deliverables
- ✓ ~170 testes (unit + integration + functional) passam
- ✓ Projeto de architecture tests a bloquear violações de camada no CI
- ✓ 80%+ code coverage
- ✓ GitHub Actions CI verde

### Commits
- `test: add architecture tests project`
- `test: complete unit, integration and functional test suites`
- `ci: add GitHub Actions workflow`

---

## SPRINT 8: Produção (Semana 12)

### Objectivo
Deploy no Render free tier, validação final, documentação de handoff.

### Tarefas

1. **Environment Configs**
   - `appsettings.json` (dev), `appsettings.Production.json` (prod)
   - Variáveis no Render:
     ```
     ASPNETCORE_ENVIRONMENT=Production
     ConnectionStrings__DefaultConnection=postgresql://... (Neon)
     Stripe__SecretKey=sk_live_...
     Resend__ApiKey=re_...
     Jwt__Secret=...
     Upstash__RedisConnectionString=...
     Upstash__QStashSigningKey=...
     Sentry__DSN=...
     ```

2. **Migrations em produção**
   - O plano gratuito do Render não tem pre-deploy command (`00_ARCHITECTURE.md §7.3`) — a migration corre como passo de release controlado, manual ou via workflow dedicado, nunca automaticamente no arranque da API
   - Testar `InitialCreate` contra um branch de teste do Neon antes de aplicar em produção

3. **QStash em produção**
   - Confirmar schedule de vinte em vinte minutos apontado para `/api/internal/jobs/dispatch`
   - Confirmar signing key e validação de assinatura ativas
   - Documentar o atraso aceite nos lembretes (cold start + intervalo de vinte minutos) como limitação conhecida do MVP gratuito

4. **Segurança**
   - Nenhum secret em código-fonte
   - CORS restrito às origens Vercel de produção
   - `Microsoft.AspNetCore.RateLimiting` ativo em login/signup/recuperação de conta
   - JWT secret gerado, mínimo 32 caracteres

5. **Documentação + Handoff**
   - Runbook de debugging em produção
   - Checklist de deployment
   - Procedimento de rollback (Render permite reverter para deploy anterior; Neon tem backups automáticos)

6. **Validação Final**
   - E2E manual: signup trainer → convite cliente → primeiro login → sessão → email
   - Confirmar `/health/live` e `/health/ready`
   - Confirmar que uma falha de Redis não bloqueia login/signup (fallback local)

### Deliverables
- ✓ Backend deployado em Render (free tier)
- ✓ Neon PostgreSQL em produção com migration aplicada de forma controlada
- ✓ Upstash Redis + QStash confirmados em produção
- ✓ Resend, Stripe, Cloudinary integrados
- ✓ Sentry + OpenTelemetry ativos
- ✓ Documentação de deploy e rollback completa

### Commits
- `chore: configure production environment`
- `docs: add deployment runbook and rollback checklist`

---

## Summary by Week

| Semana | Sprint | Foco | Entrega |
|--------|--------|------|---------|
| 0 | Sprint 0 | Setup + Decisões | Repo ready, 4 projetos + 5 projetos de teste |
| 1-2 | Sprint 1 | Domain Layer | Entities + Value Objects por feature |
| 3-4 | Sprint 2 | Infrastructure | DbContext + migration `InitialCreate` + stores de jobs/outbox |
| 5-6 | Sprint 3 | Application | Handlers + DTOs + Validators por feature |
| 7-8 | Sprint 4 | API | Controllers, Auth JWT+refresh, multi-tenancy |
| 9 | Sprint 5 | Jobs + Outbox | Dispatcher QStash, outbox Stripe, serviços externos |
| 10 | Sprint 6 | Observabilidade | Logs estruturados, Sentry, OpenTelemetry, health checks |
| 11 | Sprint 7 | Testing + CI/CD | ~170 testes + architecture tests + CI |
| 12 | Sprint 8 | Produção | Deploy Render free, QStash produção, docs |

---

## Milestones

| Milestone | Sprint | Data | Critério |
|-----------|--------|------|----------|
| Infrastructure Ready | 2 | Fim Semana 4 | DbContext + migration + persistência de jobs/outbox |
| API funcional | 4 | Fim Semana 8 | Endpoints a responder, auth completo |
| Jobs duráveis | 5 | Fim Semana 9 | Dispatcher + outbox + QStash a funcionar |
| Observabilidade | 6 | Fim Semana 10 | Logs, Sentry, OpenTelemetry em produção |
| Testes | 7 | Fim Semana 11 | ~170 testes, architecture tests, CI verde |
| Go-live | 8 | Fim Semana 12 | Deploy em produção no Render free tier |

---

## Risks & Mitigations

| Risk | Probabilidade | Impacto | Mitigação |
|------|---------------|--------|-----------|
| EF Core query performance | Média | Alto | Sprint 2: `AsNoTracking`, projeção, índices confirmados por teste |
| Multi-tenancy bug | Baixa | Alto | Sprint 2/4: testes de isolamento cross-tenant dedicados |
| Cold start / atraso do QStash | Alta (aceite no MVP) | Baixo | Documentado como limitação do plano gratuito, não bloqueia go-live |
| Job preso em `Processing` | Baixa | Médio | Lease com expiração, recuperação automática (Sprint 5) |
| Falha do Neon/migration | Muito Baixa | Crítico | Migration testada em branch antes de produção, rollback documentado |
| Quota Sentry/Upstash excedida | Baixa | Médio | Sampling e limites confirmados no Sprint 0/6 |

---

*Fim do Sprints Roadmap*
