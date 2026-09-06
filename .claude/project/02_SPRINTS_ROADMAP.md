# PT Manager — Sprint Roadmap por gates

*Planeamento Detalhado — Julho 2026 — alinhado com `00_ARCHITECTURE.md` v3.0*

---

## Overview

Migração Backend: Python (FastAPI + SQLModel) → C# (.NET 10 + EF Core), modular monolith com Clean Architecture organizada por feature (ver `00_ARCHITECTURE.md §2`).

**Timeline:** sequência por gates; a estimativa original de 12 semanas é reavaliada no
fecho do Sprint 4 devido à divisão do Sprint 5 em quatro slices independentes.
**Entrega MVP:** Backend em Render com Neon PostgreSQL, Upstash QStash, Resend, Stripe
e Cloudinary. Upstash Redis é condicional ao Gate 6B.
**Fora do MVP:** capacidades do Sprint 9. AutoMapper, MediatR e `IRepository<T>`
genérico são decisões rejeitadas, não trabalho diferido.

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
   - Conta Upstash QStash em modo dev/free; Redis só é necessário se o Gate 6B aprovar a implementação

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
   - Ciclo de vida: `IsDeleted` apenas quando eliminação e arquivo são estados distintos; `Supplement` e `ClientSupplementAssignment` usam `IsActive` e preservam referências históricas
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

### Estado final

Concluído em 5 de agosto de 2026. A entrega inclui 28 entidades mapeadas,
`InitialCreate`, Global Query Filters, validação de escritas, FKs compostas,
stores de jobs e outbox, Testcontainers PostgreSQL e testes de arquitetura.
O gate final confirmou o conjunto exato das 28 tabelas, as oito relações
cross-tenant exigidas e a ausência de alterações pendentes no modelo EF Core.

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
     `docs/backend-files/sprint_2/sprint_2_newTables/`, incluindo Domain,
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

Estado em 26 de agosto de 2026: concluído. O Lote 3G fechou Authentication,
Billing SaaS, Notifications e a relação ativa de clientes. A migration
`20260826172025_CompleteSprint3Lote3G` foi gerada pelo EF Core e validada com
migrate, rollback e migrate em PostgreSQL 17 descartável. A solução terminou
com 1228 testes aprovados e sem alterações pendentes no modelo EF Core.

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
   - Cada handler recebe um DTO de entrada, chama portas específicas do caso de uso e devolve `Result`/`Result<T>` (`00_ARCHITECTURE.md §4.3`). Não antecipar gateways ou cache genéricos sem consumidor real
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
- ✓ `Application` atual compila sem depender de Infrastructure.
- ✓ Vertical slices até ao Lote 3F usam DTOs, validators e mapping explícito.
- ✓ Authentication, Billing SaaS e Notifications concluídos no Lote 3G.
- ✓ Gate integral executado após a migration: 381 Domain, 451 Application,
  360 Infrastructure e 36 Architecture.

### Commits
- `feat: add application handlers organized by feature`
- `feat: add DTOs and validators`
- `test: add unit tests for handlers and validators`

---

## SPRINT 4: API Controllers, Auth e Moderação Administrativa (Semanas 7-8)

### Objectivo
O Sprint 4A entrega endpoints HTTP, ASP.NET Core Identity, middleware e
compatibilidade de contrato. Depois de autenticação e políticas administrativas
estarem materializadas, o Sprint 4B adiciona o vertical slice restrito de
moderação de alimentos e exercícios privados.

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
   AdminContentModerationController /admin/content-moderation (superuser + contexto administrativo)
   InternalJobsController  /api/internal/jobs/dispatch (QStash, assinatura validada, sem auth de utilizador)
   ```

   A administração read-only de trainers não entra neste sprint porque não existe
   handler dedicado. A transferência está registada como `DEF-ADMIN-001`.

5. **Matriz de migração de contrato**
   - Antes de implementar cada controller, classificar os endpoints Python correspondentes como Preserve / Alias / Remove (`00_ARCHITECTURE.md §4.2`)
   - Contract tests cobrindo os casos duvidosos (trailing slash, PUT vs PATCH, prefixos de signup)

6. **Moderação administrativa, Sprint 4B**
   - Casos de uso dedicados para Block e Unblock de `Food` e `Exercise`
     privados; sem edição funcional, mudança de owner ou hard delete
   - `PlatformEnforcementStatus` independente de `IsActive`, com motivo
     estruturado obrigatório em Block e validado por allowlist
   - Bypass de query filters apenas no store administrativo e apenas por ID
   - Estado e `AdministrativeAuditEntry` persistidos na mesma transação
   - Novas associações rejeitam recursos bloqueados; planos existentes derivam
     a condição de revisão sem booleano duplicado
   - Portal do cliente nunca projeta o conteúdo bloqueado; o contrato exato é
     classificado antes do controller conforme `00_ARCHITECTURE.md §4.2`
   - Migration EF Core nova, gerada a partir do modelo deste slice e validada
     com migrate, rollback e migrate. Não integrar no Lote 3F nem editar
     migrations aplicadas
   - Sistema genérico de denúncias e fila de revisão permanece fora deste slice

7. **Testes Integração (`Api.FunctionalTests/`)**
   - `WebApplicationFactory`: auth (login/signup/refresh/reuso de refresh token), CRUD multi-tenant, Problem Details, contract tests
   - Moderação: role e contexto administrativo, Block/Unblock idempotentes,
     auditoria atómica, escrita proibida ao trainer, recurso privado de outro
     tenant, novas referências e planos existentes afetados
   - Suite base de API e auth, acrescida das suites de persistência e contrato
     do Sprint 4B

8. **Google Sign-In, Sprint 4 Fase 5**
   - Fase reativada em 2026-09-03; deixa de pertencer ao Sprint 9C
   - Quatro operações sob `/api/v1/auth/google`: challenge, sign-in,
     link/challenge e link
   - Portas provider-neutral na Application e `Google.Apis.Auth` apenas em Infrastructure
   - Identidade externa por `provider + subject`; linking nunca é automático por email
   - Implementação real concluída em 2026-09-06; migration local aplicada;
     `QG5-FRONTEND-001` diferido para fase frontend

### Deliverables
- ✓ `Api` compila e corre localmente
- ✓ Controllers implementados, sem lógica de negócio nem EF Core direto
- ✓ Auth completo (login/signup/logout/refresh com rotação)
- ✓ Rate limiting e CORS configurados
- ✓ Matriz de migração de contrato preenchida
- ✓ Moderação privada restrita a casos administrativos auditados
- ✓ Testes de API, auth, persistência e moderação passam
- ✓ Swagger/OpenAPI gerado

### Commits
- `feat: add API controllers and endpoints`
- `feat: add JWT authentication with refresh token rotation`
- `feat: add middleware stack and rate limiting`
- `feat: add audited private catalog moderation`
- `test: add endpoint integration and contract tests`

---

## SPRINT 5: Execução Durável, Billing e Media (a partir da Semana 9)

### Objectivo

O Sprint 5 é entregue em quatro sub-slices com gates independentes. A separação é
obrigatória porque jobs, billing e processamento de media têm modelos de falha,
segurança e validação diferentes. A estimativa de uma única semana é reavaliada no
fecho do Sprint 4; não se fecha um gate por pressão de calendário.

Dependências: 5B e 5C só fecham depois do Gate 5A, porque os seus efeitos posteriores
usam a outbox. O 5D depende dos Gates 5A e 5C. Desenvolvimento independente pode
avançar antes, mas nenhum gate ignora estas dependências operacionais.

Estado de partida verificado antes do Sprint 5:

- `DurableJob`, `OutboxMessage`, `IDurableJobStore`, `IOutboxStore` e os respectivos
  repositórios PostgreSQL já existem. O trabalho em falta é a orquestração do
  dispatcher, routing por tipo/versão e o endpoint QStash.
- O email de autenticação já usa `IAuthenticationEmailSender` com
  `ResendAuthenticationEmailSender`. O envio de notificações de negócio precisa de
  uma porta própria; não se recria um `IEmailSender` genérico.
- Billing já expõe `ICheckoutGateway`, `ICustomerPortalGateway` e
  `ISubscriptionReconciliationGateway`. O adapter Stripe implementa estas portas em
  vez de introduzir `IPaymentGateway`.
- `IMediaStorage`, `MediaUpload`, `ReplaceLogoHandler` e `RemoveLogoHandler` já
  existem. Falta o adapter Cloudinary, a superfície HTTP do logo e o slice do avatar.
- Não existe `ICacheService` nem um consumidor que justifique cache distribuída. O
  adapter Redis deixa de ser deliverable obrigatório e só nasce com um caso medido.

**Sem RabbitMQ/MassTransit** (ver `00_ARCHITECTURE.md §9`).

### Sprint 5A: Dispatcher, outbox e notificações

1. Implementar `JobDispatcher` e `OutboxDispatcher` sobre as stores existentes.
2. Usar routing por allowlist de `job_type`/`job_version` e `message_type`; tipos ou
   versões desconhecidos falham de forma permanente e sanitizada.
3. Criar um scope e estabelecer `TenantContext` explícito por item. Um tenant ausente
   só é aceite para tipos internos que o contrato declare como globais.
4. Preservar entrega at-least-once, idempotency key, lease com owner, renovação antes
   da expiração, backoff limitado com jitter e `dead_letter` terminal.
5. Limitar batch, concorrência e duração total da activação. Perder o lease cancela o
   processamento local e impede marcar o item como concluído.
6. Expor `POST /api/internal/jobs/dispatch` com raw body limitado, validação da
   assinatura QStash, protecção contra replay conforme o protocolo do fornecedor,
   correlation ID e resposta sem detalhes dos itens.
7. Implementar `SendNotificationJobHandler` e uma porta específica de entrega de
   notificações. Templates são escolhidos por allowlist e nunca por path recebido no
   payload.
8. Não criar `ProcessBillingJobHandler` genérico. Cada mensagem futura de billing terá
   um handler explícito quando existir um efeito concreto.

Gate 5A:

- Dois dispatchers concorrentes não reclamam o mesmo item.
- Lease expirado recupera; lease perdido não conclui o item.
- Retry transitório reutiliza a mesma idempotency key; falha permanente termina em
  `dead_letter` sem expor payload ou segredo nos logs.
- Tipo e versão desconhecidos são rejeitados de forma determinística.
- Assinatura inválida, replay e body excessivo não activam o dispatcher.
- Testes PostgreSQL reais cobrem claim, renovação, conclusão e falha concorrente.

### Sprint 5B: Stripe e billing SaaS

1. Implementar adapters Stripe para `ICheckoutGateway`, `ICustomerPortalGateway` e
   `ISubscriptionReconciliationGateway`.
2. Expor Checkout e Customer Portal apenas ao trainer autenticado e derivar o tenant
   de `ITenantContext`.
3. Usar idempotency key estável em operações mutáveis, timeout e retry apenas para
   falhas transitórias; nenhuma transacção PostgreSQL permanece aberta durante uma
   chamada externa.
4. Implementar o webhook com raw body, `Stripe-Signature`, versão explícita da API,
   allowlist de eventos, deduplicação por `event.id`, reconciliação de eventos fora de
   ordem e outbox na mesma transacção da alteração local.
5. Registar apenas IDs técnicos necessários e Stripe request ID sanitizado. Segredos,
   payload integral e dados de pagamento não entram em logs.

Gate 5B:

- Checkout repetido preserva idempotência e associação consistente do customer.
- Customer Portal não aceita customer ou trainer fornecido pelo caller.
- Webhook inválido não escreve; evento duplicado é sucesso idempotente.
- Eventos fora de ordem reconciliam o estado actual antes do commit.
- Falhas antes e depois do commit têm comportamento de retry provado em PostgreSQL
  real.

### Sprint 5C: Imagens geridas, logo e avatar moderado

1. Implementar o adapter Cloudinary de `IMediaStorage` com timeout, cancelamento,
   identificadores gerados pelo servidor e eliminação idempotente.
2. Expor `ReplaceLogo` e `RemoveLogo` depois de validar o ciclo upload, compensação,
   commit e eliminação posterior pela outbox.
3. Criar `ReplaceMyAvatar` e `RemoveMyAvatar` em
   `Application/Features/ClientPortal/`. Apenas o próprio cliente autenticado pode
   executar estes casos de uso; trainer e superuser recebem falha de autorização.
4. Expor `PUT /api/v1/portal/my-profile/avatar` com `multipart/form-data` e
   `DELETE /api/v1/portal/my-profile/avatar`. Não aceitar `avatar_url`, `client_id` nem
   `trainer_id` do caller.
5. Manter `[SensitiveResponse]` nos endpoints para impedir cache, sem o reutilizar como
   contrato de moderação.
6. Validar no mínimo tamanho, allowlist inicial `image/jpeg`, `image/png` e
   `image/webp`, assinatura real, descodificação, dimensões e número máximo de píxeis.
   O limite inicial de tamanho é 5 MiB, coerente com `ReplaceLogoCommandValidator`;
   limites de dimensões e transformação final são fechados no blueprint antes de
   implementar. Metadados EXIF, incluindo geolocalização, são removidos e a imagem é
   reencodificada antes da publicação.
7. Criar `IImageModerationService` com `Approved`, `Rejected`, `ReviewRequired` e
   `Unavailable`. Bloquear nudez explícita, conteúdo sexual e violência gráfica.
   Categorias e thresholds são configuração controlada pelo servidor, nunca input do
   cliente.
8. Aplicar moderação síncrona e fail-closed. Apenas `Approved` publica o avatar;
   `ReviewRequired` é rejeitado no MVP e `Unavailable` devolve falha temporária. A API
   não revela scores, thresholds ou categorias internas.
9. Moderar antes do upload público quando o fornecedor aceitar conteúdo directo. Se
   exigir URL, usar quarentena privada sem URL pública até à aprovação. A selecção do
   fornecedor compara privacidade, retenção, região, latência, custo e falsos
   positivos em fotografias de fitness; prefere-se análise directa do conteúdo.
10. Acrescentar `Client.AvatarPublicId` e a constraint de par com `AvatarUrl` numa
    migration EF Core nova gerada neste slice. Não persistir estado de moderação no
    fluxo síncrono.
11. Em qualquer falha, manter o avatar anterior. Compensar o novo asset quando o
    upload já ocorreu e entregar a eliminação do asset anterior pela outbox apenas
    depois do commit.
12. Serializar substituições concorrentes sobre a ficha do cliente e voltar a
    confirmar a referência activa antes de agendar qualquer eliminação.
13. Aplicar rate limiting específico ao upload para limitar abuso e custos do storage
    e da moderação.

Gate 5C:

- MIME falso, imagem corrompida, ficheiro vazio, excesso de tamanho/dimensões e actor
  diferente do próprio cliente são rejeitados antes de alterar o perfil.
- `Rejected`, `ReviewRequired` e `Unavailable` nunca tornam o asset público nem mudam
  o avatar activo.
- Falha de persistência compensa o novo asset; retry não elimina o avatar activo.
- Duas substituições concorrentes não eliminam o asset que ficar activo.
- EXIF e geolocalização não permanecem no asset publicado.
- Replace e Remove são tenant-safe, idempotentes onde aplicável e cobertos com testes
  negativos cross-tenant.
- Migrate, rollback e migrate da nova migration passam em PostgreSQL descartável.

### Sprint 5D: Upload técnico de vídeo privado

1. Manter este vertical slice separado das imagens geridas e dependente dos gates 5A
   e 5C.
2. Preferir upload directo do browser para storage privado através de autorização
   limitada emitida pelo backend.
3. A finalização autenticada confirma ownership do exercício, asset, tamanho real e
   integridade no fornecedor.
4. Processar assincronamente container, MIME, codec, duração e resolução através dos
   jobs duráveis.
5. Usar estados técnicos `Pending`, `Processing`, `Ready`, `Rejected` e `Failed`, URL
   assinada de curta duração, rate limiting por actor e quotas autoritativas em
   PostgreSQL.
6. Fechar limites concretos de tamanho, duração, resolução, formatos, codecs e quotas
   antes da implementação.
7. Manter moderação automática de conteúdo de vídeo fora do MVP. A moderação síncrona
   aprovada no 5C aplica-se apenas ao avatar.

Gate 5D:

- Upload incompleto, metadata incompatível, ownership incorrecto, acesso cross-tenant,
  transições inválidas e limpeza de assets abandonados estão cobertos.
- Retry do processamento é idempotente e a perda de lease não publica um vídeo.
- Assets não validados permanecem privados e nenhum path controlado pelo utilizador é
  utilizado.

### Configuração e secrets

Cada adapter valida as suas opções no arranque quando a feature está activa. Secrets
só vêm da configuração do ambiente, nunca de ficheiros versionados, respostas HTTP ou
logs. Os nomes exactos das opções são definidos junto do adapter e cobertos por testes
de configuração. Um fornecedor opcional indisponível não degrada `/health/ready`, mas
o caso de uso dependente falha de forma explícita e segura.

### Deliverables

- Gate 5A: dispatcher e outbox operacionais contra PostgreSQL real, activados por
  QStash e com entrega de notificações.
- Gate 5B: Checkout, Customer Portal e webhook Stripe idempotentes e transaccionais.
- Gate 5C: Cloudinary, logo e avatar exclusivo do cliente com moderação síncrona
  fail-closed e lifecycle de assets completo.
- Gate 5D: upload técnico de vídeo privado concluído sem moderação automática.
- Cache distribuída é transferida explicitamente para o Gate 6B, onde observabilidade
  e um consumidor concreto determinam se deve ser implementada.

### Commits sugeridos

- `feat: add durable job and outbox dispatchers`
- `feat: add signed qstash dispatch endpoint`
- `feat: add notification delivery worker`
- `feat: integrate stripe billing flows`
- `feat: add managed image storage and moderated client avatars`
- `feat: add private exercise video processing`
- `test: verify sprint 5 integration and failure semantics`

---

## SPRINT 6: Observabilidade e decisão de resiliência distribuída

### Objectivo

O Sprint 6 divide-se em 6A e 6B. Primeiro mede o sistema real; depois decide e, quando
justificado, implementa cache e rate limiting distribuídos. Esta ordem evita escolher
consumidores, TTLs e invalidação sem evidência.

### Sprint 6A: Observabilidade

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
   - Latência/erros HTTP, duração e volume de queries, pool de ligações, saturação do
     rate limiting local, jobs pendentes/tentativas/dead-letter, webhooks Stripe
     duplicados/falhados, falhas de email e custos/latência das integrações de media

Gate 6A:

- Logs e traces não contêm passwords, tokens, cookies, API keys, payloads Stripe nem
  scores de moderação.
- Métricas permitem identificar endpoints de leitura repetitiva, pressão sobre
  PostgreSQL e necessidade de coordenação entre instâncias.
- Falhas dos exporters não interrompem operações de negócio.

### Sprint 6B: HybridCache, Upstash Redis e rate limiting distribuído

O Gate 6B é obrigatório como decisão, mas a implementação é condicional. Analisa as
medições do 6A e documenta um resultado `Implementar` ou `Não implementar ainda`.

Implementar quando existir pelo menos um destes sinais:

1. Mais de uma instância da API precisa de partilhar limites por actor.
2. Uma query de leitura repetitiva excede o orçamento de latência ou carga definido
   no Gate 6A e mantém semântica segura com cache.
3. O rate limiting local deixa de proteger de forma consistente endpoints com custo
   externo, como login, email ou moderação de avatar.
4. Existe um consumidor concreto com estratégia verificável de chave, TTL,
   invalidação e fallback.

Se a implementação for aprovada:

1. Introduzir portas estreitas por consumidor; não criar um `ICacheService` genérico
   sem semântica da feature.
2. Usar HybridCache como camada local e Upstash Redis como coordenação distribuída.
3. Incluir ambiente e tenant nas chaves, prevenir cache stampede e nunca guardar
   autorização, refresh tokens, billing, jobs ou outro estado autoritativo.
4. Aplicar timeouts curtos e fallback para PostgreSQL ou memória local. Uma falha de
   Redis não bloqueia a operação principal.
5. Implementar rate limiting distribuído apenas nas policies cujo risco o justifique;
   quotas comerciais continuam autoritativas em PostgreSQL.
6. Medir hit ratio, miss, latência, erro, evicção e custo antes e depois da activação.

Se os sinais não existirem, o gate regista a evidência, mantém a implementação local e
agenda nova avaliação no Sprint 9B. O item não desaparece nem é declarado concluído.

Gate 6B:

- A decisão `Implementar` ou `Não implementar ainda` tem métricas e consumidores
  concretos associados.
- Quando implementado, testes provam isolamento de tenant, invalidação, fallback e
  comportamento com Redis indisponível.
- O Sprint 8 configura e valida Redis apenas quando a decisão for `Implementar`.

### Deliverables
- ✓ Logs estruturados em JSON, sem file sink
- ✓ Correlation IDs propagados
- ✓ Sentry + OpenTelemetry ativos
- ✓ `/health/live` e `/health/ready` distintos e operacionais
- ✓ Decisão do Gate 6B registada; Redis implementado apenas quando justificado
- ✓ Testes de observabilidade e, quando aplicável, de cache e rate limiting passam

### Commits
- `feat: add structured logging and correlation IDs`
- `feat: add Sentry and OpenTelemetry instrumentation`
- `feat: add liveness and readiness health endpoints`
- `feat: add distributed cache and rate limiting` apenas se o Gate 6B aprovar

---

## SPRINT 7: Testing + CI/CD (após o Gate 6B)

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

## SPRINT 8: Produção (após o Sprint 7)

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
     Upstash__RedisConnectionString=...  # apenas se o Gate 6B aprovou Redis
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
   - Se Redis foi implementado no 6B, confirmar que a sua falha não bloqueia
     operações principais e que endpoints sensíveis mantêm a protecção local aprovada

### Deliverables
- ✓ Backend deployado em Render (free tier)
- ✓ Neon PostgreSQL em produção com migration aplicada de forma controlada
- ✓ Upstash QStash confirmado em produção
- ✓ Upstash Redis confirmado apenas se o Gate 6B aprovou a implementação
- ✓ Resend, Stripe, Cloudinary integrados
- ✓ Sentry + OpenTelemetry ativos
- ✓ Documentação de deploy e rollback completa

### Commits
- `chore: configure production environment`
- `docs: add deployment runbook and rollback checklist`

---

## SPRINT 9: Backlog pós-MVP governado

### Objectivo

Reavaliar capacidades deliberadamente excluídas do MVP sem as transformar em
compromissos automáticos. Cada sub-slice começa por confirmar os critérios de entrada,
produz uma decisão e só implementa quando existir necessidade aprovada.

### Sprint 9A: Trust & Safety de media e conteúdo

- Revisão humana para resultados `ReviewRequired` do avatar.
- Moderação automática de vídeo para nudez, conteúdo sexual, violência, armas e
  política de relevância.
- Antivírus e scanning adicional de uploads.
- Sistema de denúncias, preservação de evidência e fila de revisão.

Critério de entrada: volume de conteúdo, falsos positivos, incidentes, obrigação de
plataforma ou necessidade operacional que não possa ser tratada pelo fluxo síncrono do
avatar e pela moderação administrativa actual.

### Sprint 9B: Escalabilidade e defesa em profundidade

- Nova avaliação de Redis se o Gate 6B decidiu `Não implementar ainda`.
- PostgreSQL Row-Level Security como camada adicional de isolamento.
- RabbitMQ/MassTransit ou outro broker gerido.
- Extracção de módulos para serviços separados.
- Revogação global imediata de access tokens através de versão de sessão ou security
  stamp validado no servidor.

Critério de entrada: múltiplas instâncias, compliance, throughput, latência, consumers
independentes, necessidade de deploy separado ou incidente que demonstre insuficiência
dos controlos actuais. RabbitMQ e microserviços mantêm os critérios detalhados de
`00_ARCHITECTURE.md §9.5` e §2.1.

### Sprint 9C: Produto, administração e compliance

- Métricas customizáveis por cliente.
- Versionamento de planos de treino e nutrição.
- Relatórios persistidos de cliente.
- `client_consents`, apenas depois de análise legal e de produto própria.
- Superfície administrativa read-only de trainers, removida do Sprint 4 até existir um
  caso administrativo dedicado e auditado.
- Registo de séries pelo próprio cliente, sem reutilizar handlers autorizados apenas
  para trainer.
- Cancelamento de sessão pelo cliente no portal, com política própria para janela de
  cancelamento, impacto no pack e auditoria.

Critério de entrada: pedido de produto aprovado, contrato HTTP definido, impacto de
schema avaliado e testes de autorização/multi-tenancy especificados.

### Sprint 9D: Consolidação de contratos

- Uniformizar `StartDate` e `StartsDate` entre Training e Nutrition através de uma
  decisão Preserve, Alias ou Remove.

Critério de entrada: consumidores identificados, contrato OpenAPI e frontend
inventariados, estratégia de compatibilidade aprovada e migration avaliada caso a
alteração alcance persistência.

### Registo central de trabalho diferido

| ID | Capacidade | Origem | Destino | Estado e condição |
|---|---|---|---|---|
| DEF-BILLING-001 | Billing de escrita | Sprint 4 | Sprint 5B | Agendado; depende do adapter Stripe e do Gate 5A |
| DEF-MEDIA-001 | ReplaceLogo e upload de imagem | Sprint 4 | Sprint 5C | Agendado; depende de Cloudinary e do lifecycle por outbox |
| DEF-MEDIA-002 | Upload técnico de vídeo privado | Sprint 5B original | Sprint 5D | Agendado; separado de Billing e dependente dos Gates 5A e 5C |
| DEF-INFRA-001 | HybridCache e Upstash Redis | Sprint 5 original | Sprint 6B | Decisão obrigatória; implementação condicionada às métricas do 6A |
| DEF-TRUST-001 | Revisão humana de avatar | Sprint 5C | Sprint 9A | Entrar com volume relevante de `ReviewRequired` ou falsos positivos |
| DEF-TRUST-002 | Moderação automática de vídeo | Arquitectura §17.4 | Sprint 9A | Entrar com política, orçamento, fornecedor e processo de revisão aprovados |
| DEF-TRUST-003 | Antivírus e scanning adicional de media | Arquitectura §17.4 | Sprint 9A | Entrar após avaliação de risco ou incidente |
| DEF-TRUST-004 | Denúncias, evidência e fila de revisão | Arquitectura §17.5 | Sprint 9A | Entrar com caso operacional e política de retenção aprovados |
| DEF-SCALE-001 | PostgreSQL RLS | Arquitectura §6.5 | Sprint 9B | Entrar com compliance, acesso SQL externo ou complexidade multi-tenant relevante |
| DEF-SCALE-002 | RabbitMQ/MassTransit | Arquitectura §9.5 | Sprint 9B | Entrar quando um dos critérios de broker for medido |
| DEF-SCALE-003 | Extracção para microserviços | Arquitectura §2.1 | Sprint 9B | Entrar com escala, ownership ou deploy independente comprovado |
| DEF-SEC-001 | Revogação imediata de access tokens | Arquitectura §5.3 | Sprint 9B | Entrar quando a janela máxima de 15 minutos deixar de ser aceitável |
| DEF-PROD-001 | Métricas customizáveis | Arquitectura §17 | Sprint 9C | Entrar com requisito de produto concreto |
| DEF-PROD-002 | Versionamento de planos | Arquitectura §17 | Sprint 9C | Entrar com requisito de histórico/versionamento aprovado |
| DEF-PROD-003 | Relatórios persistidos | Arquitectura §17 | Sprint 9C | Entrar com formato, retenção e consumidores definidos |
| DEF-COMP-001 | Consentimentos do cliente | Arquitectura §17.1 | Sprint 9C | Entrar apenas após análise legal e de produto |
| DEF-ADMIN-001 | Administração read-only de trainers | Matriz HTTP da Fase 4 | Sprint 9C | Entrar com casos de uso administrativos explícitos e auditoria |
| DEF-PORTAL-001 | Registo de séries pelo cliente | Fase 4 | Sprint 9C | Entrar com regra de produto e autorização exclusiva do próprio cliente |
| DEF-PORTAL-002 | Cancelamento de sessão pelo cliente | Fase 4 | Sprint 9C | Entrar com regras de janela, saldo do pack e notificações aprovadas |
| DEF-CONTRACT-001 | Uniformizar `StartDate` e `StartsDate` | Fase 4 | Sprint 9D | Entrar com matriz Preserve/Alias/Remove e consumidores inventariados |

Itens rejeitados não entram neste registo como implementação futura: AutoMapper,
MediatR, `IRepository<T>` genérico e Unit of Work genérica continuam proibidos pela
arquitectura enquanto não existir uma decisão canónica que os substitua.

---

## Summary by Week

| Semana | Sprint | Foco | Entrega |
|--------|--------|------|---------|
| 0 | Sprint 0 | Setup + Decisões | Repo ready, 4 projetos + 5 projetos de teste |
| 1-2 | Sprint 1 | Domain Layer | Entities + Value Objects por feature |
| 3-4 | Sprint 2 | Infrastructure | DbContext + migration `InitialCreate` + stores de jobs/outbox |
| 5-6 | Sprint 3 | Application | Handlers + DTOs + Validators por feature |
| 7-8 | Sprint 4 | API + Moderação + Google | Backend fechado; Fase 5 Google implementada; frontend Google diferido (`QG5-FRONTEND-001`) |
| 9+ | Sprint 5 | Execução durável + Billing + Media | Gates 5A a 5D; duração reestimada no fecho do Sprint 4 |
| Após 5D | Sprint 6 | Observabilidade + decisão Redis | Gate 6A mede; Gate 6B decide e implementa se necessário |
| Após 6B | Sprint 7 | Testing + CI/CD | Suite crítica + architecture tests + CI |
| Após 7 | Sprint 8 | Produção | Deploy Render free, QStash produção, docs |
| Pós-MVP | Sprint 9 | Backlog governado | Trust & Safety, escala, produto, compliance e contratos por critérios de entrada |

---

## Milestones

| Milestone | Sprint | Data | Critério |
|-----------|--------|------|----------|
| Infrastructure Ready | 2 | Fim Semana 4 | DbContext + migration + persistência de jobs/outbox |
| API funcional | 4 | Fim Semana 8 | Endpoints a responder, auth completo |
| Jobs duráveis | 5A | Gate 5A | Dispatcher + outbox + QStash a funcionar |
| Billing SaaS | 5B | Gate 5B | Checkout, Customer Portal e webhook Stripe validados |
| Imagens geridas | 5C | Gate 5C | Logo e avatar moderado com lifecycle completo |
| Vídeo privado | 5D | Gate 5D | Upload e processamento técnico privados |
| Observabilidade | 6A | Após Gate 5D | Logs, Sentry, OpenTelemetry e métricas operacionais |
| Decisão Redis | 6B | Gate 6B | Implementar com consumidor medido ou diferir explicitamente para 9B |
| Testes | 7 | Após Gate 6B | Suite crítica, architecture tests e CI verde |
| Go-live | 8 | Após Sprint 7 | Deploy em produção no Render free tier |
| Backlog pós-MVP | 9 | Após go-live | Reavaliações condicionais com decisão registada |

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
