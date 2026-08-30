# PT Manager — Backend C# Checklist

Versão 2.0 | Agosto 2026 | .NET 10 / ASP.NET Core / EF Core 10 — Clean Architecture

Guia de verificação a consultar antes de implementar ou rever cada feature. Este documento é derivado e deve manter-se consistente com `00_ARCHITECTURE.md` (fonte de verdade), `01_DATABASE_SCHEMA.md` e `03_DEVELOPER_GUIDE.md`. Em caso de conflito, esses documentos prevalecem sobre este checklist.

---

## 1. Clean Architecture & Camadas

### 1.1 Dependências entre projectos

- [ ] `Domain` não referencia `Application`, `Infrastructure` nem `Api`
- [ ] `Application` referencia apenas `Domain`
- [ ] `Infrastructure` referencia `Application` e `Domain`, implementa as portas declaradas pela Application
- [ ] `Api` referencia `Application`; só referencia `Infrastructure` no composition root (`Program.cs`)
- [ ] Architecture tests (`tests/ArchitectureTests`) impedem estas violações automaticamente
- [ ] `Program.cs` chama `AddApplication()` / `AddInfrastructure()` — nenhum tipo concreto da Infrastructure é referenciado fora do composition root

### 1.2 Organização por feature

- [ ] Application e Api organizadas por feature funcional (Clients, Sessions, TrainingPlans, Nutrition, Supplements, Assessments, Billing, Notifications, Administration, Authentication) — não por pasta técnica horizontal
- [ ] Cada caso de uso tem um handler explícito e único (criar, actualizar, arquivar são handlers distintos)
- [ ] Nenhum MediatR introduzido — dispatch de handlers é explícito
- [ ] Nenhum AutoMapper introduzido — mapping Entity → DTO é explícito

### 1.3 Abstracções

- [ ] Nenhum `IRepository<T>` genérico que replique `DbSet<T>`
- [ ] Portas da Application (`ITenantContext`, `ICacheService`, `IEmailSender`, `IPaymentGateway`, `IClock`, repositórios/query services específicos) representam necessidades do caso de uso, não CRUD genérico
- [ ] `DbContext` é a unidade transaccional; uma abstracção adicional de Unit of Work só existe se resolver uma necessidade concreta não coberta pelo EF Core
- [ ] Controllers: recebem/validam contrato HTTP, obtêm utilizador via contexto aprovado, chamam um único handler, convertem `Result`/`Result<T>` em resposta HTTP — nunca executam queries EF Core nem regras de negócio

---

## 2. Multi-Tenancy & Isolamento de Dados

### 2.1 ITenantContext

- [ ] `ITenantContext` é scoped e contém trainer efectivo, utilizador, role, origem da execução (HTTP/QStash/Stripe) e flag de operação administrativa aprovada
- [ ] Tenant em falta provoca falha fechada em operações tenant-owned — `null` nunca significa acesso global
- [ ] `trainer_id` recebido em body, query string ou route parameter **nunca** define o tenant efectivo
- [ ] `ITenantContext` é resolvido a partir de `ITenantContextInitializer` — nunca a partir de `HttpContext` directamente dentro do `DbContext` ou `OnModelCreating`

### 2.2 Global Query Filters (EF Core)

- [ ] Entidades tenant-owned usam Global Query Filters centralizados no `PtManagerDbContext`, ligados a `ITenantContext`
- [ ] `IEntityTypeConfiguration<T>` configura mapping/constraints/índices — não captura tenant
- [ ] Dados globais e privados (catálogos) seguem a política: `OwnerTrainerId is null OR OwnerTrainerId == trainerEfetivo`, e exigem trainer efectivo presente (sem tenant, zero linhas — incluindo globais)
- [ ] Entidades filhas de agregado têm navegação POCO dependente-para-raiz e filtro equivalente via raiz — protege queries directas às filhas sem duplicar `owner_trainer_id`
- [ ] `IgnoreQueryFilters` proibido em código funcional normal; uso administrativo exige caso de uso dedicado + política de autorização própria + auditoria + teste cross-tenant

### 2.3 Escritas e integridade

- [ ] Trainer efectivo é atribuído na criação de entidades tenant-owned (nunca aceite do payload)
- [ ] Alterações que tentem trocar o tenant são rejeitadas
- [ ] `TenantWriteValidationInterceptor` valida entidades adicionadas/modificadas; validações com I/O correm em `SavingChangesAsync` assíncrono — a variante síncrona falha explicitamente
- [ ] FKs compostas impedem relações entre tenants onde o schema o permitir (ex.: `ClientSupplementAssignment` valida que `Client` pertence ao mesmo tenant)

### 2.4 Jobs, webhooks, cache e superuser

- [ ] Job tenant-owned transporta `TrainerId` persistido; dispatcher cria scope + valida trainer + constrói `ITenantContext` antes do handler
- [ ] Webhook Stripe resolve trainer a partir de identificadores persistidos e validados — metadata da Stripe nunca concede autorização por si só
- [ ] Cache keys tenant-owned seguem `{environment}:trainer:{trainer_id}:{feature}:{resource}`
- [ ] Superuser não obtém acesso global através de tenant vazio — operações globais usam handlers/políticas/contexto administrativo explícitos
- [ ] Row-Level Security do PostgreSQL **não** é usado no MVP (decisão adiada — ver `00_ARCHITECTURE.md §6.5`)

### 2.5 Testes multi-tenant obrigatórios

- [ ] Trainer não lê/actualiza/elimina dados de outro trainer
- [ ] IDs manipulados no body não alteram o tenant; escritas com tenant adulterado são rejeitadas
- [ ] Catálogos globais/privados respeitam a política definida
- [ ] Client acede apenas ao próprio agregado
- [ ] Job executa apenas no tenant persistido
- [ ] Superuser só faz bypass via caso de uso administrativo
- [ ] Cache keys não colidem entre tenants

---

## 3. Autenticação & Autorização

### 3.1 ASP.NET Core Identity

- [ ] Hash de passwords, políticas de password, lockout, verificação de email e estado activo/suspenso usam ASP.NET Core Identity — nenhuma implementação própria portada do Python

### 3.2 Access & Refresh Tokens

- [ ] Access token JWT de duração curta (15 min, configurável)
- [ ] Refresh token opaco (30 dias, configurável); apenas o **hash** é persistido no PostgreSQL
- [ ] Cada refresh roda o token (rotation)
- [ ] Reutilização de um refresh token já rodado revoga a família inteira
- [ ] Logout revoga a sessão no servidor
- [ ] Refresh token em cookie `HttpOnly`, `Secure`, `SameSite=None` (enquanto frontend/API estiverem em sites diferentes), path limitado aos endpoints de autenticação
- [ ] Access token mantido em memória no frontend — nunca em localStorage
- [ ] Lista de origens CORS aprovadas é explícita (produção Vercel + preview deployments), nunca wildcard

### 3.3 CSRF & validação de Origin

- [ ] Endpoints que alteram/renovam sessão via cookie (refresh, logout) validam estritamente o header `Origin`
- [ ] Token anti-CSRF associado à sessão, enviado em header próprio, obrigatório nesses endpoints
- [ ] Pedidos sem origem aprovada ou sem token anti-CSRF válido são rejeitados
- [ ] Testes funcionais cobrem pedidos cross-site não autorizados
- [ ] CORS não é tratado como substituto de protecção CSRF

### 3.4 Claims, políticas e revalidação

- [ ] JWT normal valida assinatura, issuer, audience, expiração — sem round-trip à BD
- [ ] Refresh consulta sempre PostgreSQL e valida utilizador, sessão, email verificado e estado de suspensão
- [ ] Operações de risco elevado (administração global, alterações de billing) revalidam o estado actual do utilizador no PostgreSQL, mesmo com access token ainda válido
- [ ] Políticas distinguem: autenticação, role, tenant, ownership do recurso, estado da subscrição
- [ ] Uma API key incorporada no frontend nunca é tratada como controlo de segurança

### 3.5 Autorização por role

- [ ] Endpoints trainer/superuser/client usam `[Authorize(Roles = "...")]` — nenhum `if (user.Role == "trainer")` inline
- [ ] Cliente não acede a rotas `/trainer/*`; trainer não acede a rotas administrativas

### 3.6 Testes de autenticação obrigatórios

- [ ] Login válido/inválido, utilizador suspenso, email não verificado
- [ ] Refresh com rotação; reutilização de refresh token detectada e família revogada
- [ ] Revogação no logout
- [ ] CORS e envio do cookie
- [ ] Validação de Origin e token anti-CSRF; rejeição de refresh/logout de origem não autorizada
- [ ] Expiração e janela residual máxima do access token

---

## 4. Validação de Inputs & Contrato HTTP

### 4.1 Duas camadas de validação

- [ ] API valida estrutura do contrato HTTP: tipos, campos obrigatórios, limites de tamanho
- [ ] Application valida o caso de uso e regras de negócio, de forma explícita e **assíncrona** — aplica-se também a jobs, webhooks e futuros adapters (não apenas a Controllers)
- [ ] FluentValidation é usado nas duas camadas; custom validators em pasta própria por feature
- [ ] Nenhum campo numérico do frontend é confiado sem range check; nenhum enum aceite sem verificação de valores válidos

### 4.2 Contrato preservado

- [ ] Prefixo `/api/v1` explícito em todas as rotas
- [ ] JSON em **`snake_case`** (contrato preservado do backend Python) — não CamelCase
- [ ] Cada endpoint está classificado na matriz de migração: Preserve / Alias / Remove
- [ ] Bugs de segurança e rotas quebradas do backend Python **não** são contrato a preservar
- [ ] OpenAPI gerado pelo backend C# é a fonte de verdade do contrato novo

### 4.3 Erros e Result pattern

- [ ] Application retorna `Result` / `Result<T>` para falhas esperadas — nunca lança excepção para fluxo de controlo normal
- [ ] Erro esperado contém: código estável, categoria (Validation, NotFound, Conflict, Unauthorized, Forbidden, PaymentRequired, ExternalDependency), descrição segura para o cliente, metadados estritamente necessários
- [ ] API converte `Result` de erro em Problem Details (RFC 7807); `detail` continua disponível para compatibilidade com o interceptor do frontend
- [ ] Nenhuma resposta expõe stack traces, secrets ou detalhes internos
- [ ] Excepções representam apenas falhas inesperadas/indisponibilidade técnica — middleware global converte para resposta segura e envia a Sentry
- [ ] Nenhum bare `catch (Exception ex)` sem re-throw ou tratamento específico

### 4.4 Operações assíncronas

- [ ] Toda a I/O é assíncrona, recebe e propaga `CancellationToken`
- [ ] Nenhum `.Result`, `.Wait()` ou sync-over-async
- [ ] Timeouts definidos para PostgreSQL, Redis e chamadas HTTP externas
- [ ] Trabalho CPU-bound e bibliotecas síncronas são tratados explicitamente (não assumem que `async` resolve bloqueio)

---

## 5. Data Access Layer & Performance (EF Core)

### 5.1 Queries de leitura

- [ ] Projecção (`Select` para DTO) preferida a carregar entidades completas
- [ ] `AsNoTracking()` usado quando não há alteração
- [ ] Includes explícitos apenas quando necessários — nenhum lazy loading (`UseLazyLoadingProxies` **não** é usado)
- [ ] Paginação obrigatória em colecções potencialmente grandes (limite explícito)
- [ ] Nenhum N+1: se acedes `Meals` no DTO, tens `.Include(mp => mp.Meals)` explícito
- [ ] `SELECT *` evitado — projecção explícita sempre que possível

### 5.2 Migrations EF Core

- [ ] Migrations **sempre geradas** via `dotnet ef migrations add` — nunca escritas à mão
- [ ] Nenhuma migration já aplicada num ambiente partilhado é editada — correcções usam migration nova
- [ ] `InitialCreate` é a única migration criada a partir do schema aprovado (sem histórico convertido do Python)
- [ ] Migrations futuras seguem expand-contract quando é necessária compatibilidade durante o deploy
- [ ] API **não** executa `Database.Migrate()` automaticamente no arranque — migration corre como passo de release controlado (o Render free não tem pre-deploy command)
- [ ] CHECK constraints, FKs com `ON DELETE` explícito (`CASCADE`/`RESTRICT`) e colunas `GENERATED` estão na BD, não apenas em validação C#

### 5.3 Índices & GENERATED columns

- [ ] Índices em colunas filtradas/ordenadas: `owner_trainer_id`, `client_id`, `created_at`; compostos quando a query é multi-coluna
- [ ] `EXPLAIN ANALYZE` confirma uso de índice em queries críticas antes de merge
- [ ] `kcal GENERATED ALWAYS AS (...) STORED` documentado com comentário explicativo na migration; EF Core marcado como read-only (`.HasComputedColumnSql()` / `[Computed]`)

---

## 6. Async/Await & Threading

- [ ] Métodos com I/O são `async Task`/`async Task<T>`
- [ ] Nenhum `.Result`/`.Wait()` (deadlock em ASP.NET Core)
- [ ] Nenhum fire-and-forget sem tracking (`_ = SomeAsyncMethod()`)
- [ ] `CancellationToken` propagado ponta-a-ponta em handlers, repositórios e chamadas externas
- [ ] Modificação de estado partilhado protegida por lock/collection thread-safe
- [ ] `DbContext` é scoped por operação — nunca partilhado entre threads/paralelismo sem novo scope

---

## 7. Dependency Injection & Testabilidade

- [ ] Nenhuma classe instanciada com `new` dentro de handlers — vem do DI container
- [ ] Serviços externos (Stripe, Resend, Cloudinary, Redis) têm portas na Application e adapters injectáveis na Infrastructure
- [ ] Configuração vem de `IConfiguration`/`IOptions<T>` injectado — nunca `Environment.GetEnvironmentVariable()` directo no código de negócio
- [ ] Nenhum método `static` que dificulte mocking em lógica de negócio
- [ ] Interfaces pequenas e focadas (ISP)
- [ ] Testes usam doubles/mocks para portas externas — nunca mock da BD real (usar Testcontainers)

---

## 8. Cache & Rate Limiting (Upstash Redis)

### 8.1 HybridCache

- [ ] `ICacheService` implementado sobre `HybridCache`: camada local em memória + Upstash Redis distribuído
- [ ] Protecção contra cache stampede activa
- [ ] TTL explícito e invalidação documentada por feature
- [ ] Redis guarda apenas dados reconstruíveis — nunca: subscrições autoritativas, ownership de recursos, refresh tokens autoritativos, estado exclusivo de jobs, decisões de autorização não revalidáveis

### 8.2 Política de falha

- [ ] Falha de Redis nunca impede operações principais — fallback para fonte de verdade ou cache local
- [ ] Rate limiting geral é fail-open; endpoints sensíveis (login, recuperação de conta) usam fallback local quando Redis indisponível
- [ ] Timeouts curtos + retry limitado a falhas transitórias
- [ ] Métricas de hit/miss/erro/latência; prefixos de cache separados por ambiente

### 8.3 Rate limiting

- [ ] `Microsoft.AspNetCore.RateLimiting` nativo (sem package externo)
- [ ] Política dedicada mais restritiva para login/signup/recuperação de conta
- [ ] Headers `X-RateLimit-*` retornados; `429 Too Many Requests` quando excedido

---

## 9. Jobs Duráveis, Outbox & QStash

### 9.1 Decisão de arquitectura

- [ ] Nenhum RabbitMQ/MassTransit/Hangfire/BackgroundService de longa duração — Render free não tem workers persistentes
- [ ] Upstash QStash chama `POST /api/internal/jobs/dispatch` periodicamente (intervalo configurável, ~20 min no MVP) para activar o dispatcher
- [ ] Endpoint interno: sem auth de utilizador, valida assinatura QStash, limita tamanho do body, processa batch limitado, propaga correlation ID, não expõe detalhes dos jobs na resposta

### 9.2 Estado do job

- [ ] `DurableJob` contém: id, trainer (quando aplicável), tipo+versão, payload, data de execução UTC, estado, tentativas, próxima tentativa, idempotency key, correlation ID, erro sanitizado, expiração do lease, owner do lease
- [ ] Estados: `Pending`, `Processing`, `Completed`, `Failed`, `DeadLetter`

### 9.3 Dispatcher

- [ ] Reclamação transaccional de jobs vencidos (`SELECT ... FOR UPDATE SKIP LOCKED`)
- [ ] Lease com owner token opaco novo por claim + duração limitada — evita processamento duplicado em paralelo
- [ ] Scope + `ITenantContext` criados por job antes de chamar o handler
- [ ] Entrega at-least-once — handlers são idempotentes
- [ ] Job preso em `Processing` com lease expirado volta a ficar elegível; lease é renovado antes de expirar em processamento demorado
- [ ] Falhas permanentes movem o job para `DeadLetter`

### 9.4 Outbox

- [ ] Outbox liga alterações PostgreSQL a efeitos secundários (email pós-pagamento, aviso de falha, notificações internas) sem transacção distribuída
- [ ] Item da outbox só é concluído depois do efeito correspondente ser confirmado

### 9.5 Testes de jobs obrigatórios

- [ ] Assinatura QStash inválida; job ainda não vencido; reclamação concorrente
- [ ] Retry transitório; falha permanente; idempotência
- [ ] Tenant inexistente ou suspenso
- [ ] Recuperação de job preso em `Processing`; transição para `DeadLetter`
- [ ] Owner e expiração do lease; renovação de lease em processamento demorado

---

## 10. Stripe

### 10.1 Âmbito

- [ ] Stripe gere **exclusivamente** a subscrição SaaS do trainer ao PT Manager
- [ ] Packs de sessões vendidos a clientes são domínio interno do trainer, com snapshot comercial local — **sem** customer/price/payment-intent/subscription IDs da Stripe

### 10.2 Chamadas iniciadas pela aplicação

- [ ] Checkout e Customer Portal criados via `IPaymentGateway`
- [ ] Idempotency key estável por operação de negócio; retries usam a mesma key e parâmetros
- [ ] Timeout definido; rate limiting e erros transitórios tratados
- [ ] Nenhuma transacção PostgreSQL aberta durante a chamada externa
- [ ] Stripe request ID registado sem dados sensíveis

### 10.3 Webhook

- [ ] Lê raw body sem modificar; valida `Stripe-Signature` com secret do ambiente
- [ ] Processa apenas event types explicitamente suportados; evento autenticado não suportado é logado sanitizado e devolve 2xx
- [ ] Deduplica por `event.id`
- [ ] Resolve trainer a partir de relações persistidas — nunca confia em metadata não validada
- [ ] Actualiza estado local numa transacção; regista efeitos secundários na outbox na **mesma** transacção
- [ ] Devolve 2xx apenas depois do commit durável; devolve erro (permite retry Stripe) se a persistência falhar
- [ ] Versão explícita da API Stripe configurada; subscreve apenas os event types necessários
- [ ] Handler reconcilia com a Stripe quando a ordem de eventos afecta a decisão (eventos podem chegar fora de ordem)

### 10.4 Testes Stripe obrigatórios

- [ ] Assinatura válida/inválida; raw body alterado
- [ ] Event type desconhecido autenticado → 2xx sem efeito
- [ ] Evento duplicado; eventos fora de ordem
- [ ] Falha antes do commit; falha depois de criar a outbox
- [ ] Retry de chamada Stripe com a mesma idempotency key

---

## 11. Serviços Externos (Email, Media)

### 11.1 Email (Resend)

- [ ] `IEmailSender` injectado — implementação concreta (Resend) fica na Infrastructure
- [ ] Envio de email sempre assíncrono, nunca síncrono no request path — passa pela outbox/dispatcher de jobs
- [ ] HTML template usado, não apenas plaintext
- [ ] Falha de envio logada estruturadamente com prefixo `[NOTIFICATIONS]`/`[EMAIL]`

### 11.2 Cloudinary (Media)

- [ ] Upload directo (browser → Cloudinary) quando aplicável — evita passar binários pela API
- [ ] `public_id` armazenado para delete/update; timeout de upload definido
- [ ] Fallback para imagem default quando recurso não existe
- [ ] Nenhuma imagem armazenada na BD — apenas URL/`public_id`

### 11.3 Avaliação de SDKs

- [ ] Packages/SDKs de integrações futuras avaliados apenas no sprint do primeiro consumidor (não instalados antecipadamente)
- [ ] Quando um SDK não está activamente mantido, preferir `HttpClient` tipado

---

## 12. Observabilidade

### 12.1 Logs estruturados

- [ ] `ILogger` com output JSON para console em produção (sem file sink — filesystem efémero no Render)
- [ ] Correlation ID em cada pedido; Trace ID associado aos logs
- [ ] Event IDs estáveis para eventos relevantes
- [ ] Placeholders nomeados (`logger.LogInformation("[NUTRITION] Plan {MealPlanId} created", id)`) — nunca string interpolation directa no template
- [ ] Redaction automática de passwords, tokens, cookies, API keys e dados pessoais desnecessários
- [ ] Prefixos de domínio: `[NUTRITION]`, `[BILLING]`, `[SESSIONS]`, `[AUTH]`, `[DB]`, `[JOBS]`, `[STARTUP]`

### 12.2 Erros, traces e métricas

- [ ] Sentry recebe erros não tratados e contexto sanitizado
- [ ] OpenTelemetry instrumenta ASP.NET Core, `HttpClient`, EF Core/Npgsql, runtime .NET, jobs e integrações externas (Activities próprias)
- [ ] Métricas mínimas: latência/erros HTTP; duração/falhas de queries; pool de ligações; cache hit ratio e falhas Redis; jobs pendentes/tentativas/DeadLetter; webhooks Stripe duplicados/falhados; falhas de email

### 12.3 Health checks

- [ ] `GET /health/live` — confirma apenas que o processo responde, sem consultar serviços externos
- [ ] `GET /health/ready` — confirma que a aplicação usa PostgreSQL; Redis/QStash/Stripe/Resend/Cloudinary **não** bloqueiam readiness
- [ ] Endpoints devolvem apenas estado agregado — detalhes internos ficam em logs/telemetria

---

## 13. API Design & Contratos

### 13.1 Versioning

- [ ] `/api/v1/` explícito em todas as rotas
- [ ] Mudança de campo/formato de resposta = nova versão ou entrada na matriz Preserve/Alias/Remove
- [ ] Nenhuma breaking change sem comunicação/documentação prévia

### 13.2 Response & Request Models

- [ ] JSON em `snake_case` (compatibilidade com frontend existente)
- [ ] Status codes correctos: 201 Created, 204 No Content, 409 Conflict, 402 Payment Required (bloqueios de subscrição), etc.
- [ ] Nenhuma resposta expõe stack traces ou detalhes internos

---

## 14. Database & Schema Design

Ver `01_DATABASE_SCHEMA.md` para o schema aprovado. Este checklist cobre apenas padrões transversais.

- [ ] PRIMARY KEY (UUID) em todas as tabelas
- [ ] UNIQUE constraints onde aplicável (ex.: índice único parcial para plano de treino activo por cliente)
- [ ] CHECK constraints na BD (não apenas em validação C#)
- [ ] NOT NULL onde apropriado; FKs com `ON DELETE` explícito
- [ ] `calculation_snapshot` JSONB obrigatório em `MealPlan`; agregado deriva alvos relacionais do snapshot para impedir divergências
- [ ] Snapshots comerciais (ex.: `ClientSessionPack`) não são reescritos quando o catálogo é alterado/desactivado

---

## 15. Testing & Code Quality

### 15.1 Estrutura de testes

- [ ] `tests/Unit/Domain.UnitTests` — invariantes e value objects sem mocks
- [ ] `tests/Unit/Application.UnitTests` — handlers com doubles das portas externas
- [ ] `tests/Integration/Infrastructure.IntegrationTests` — PostgreSQL real via Testcontainers, `MigrateAsync` (nunca `EnsureCreatedAsync`)
- [ ] `tests/FunctionalTests/Api.FunctionalTests` — `WebApplicationFactory`; cobre rotas, serialização `snake_case`, autenticação, autorização, Problem Details, compatibilidade de contrato
- [ ] `tests/ArchitectureTests` — impede violações de camadas (ver secção 1.1) e uso de `IgnoreQueryFilters` sem autorização

### 15.2 Padrões

- [ ] Padrão AAA (Arrange, Act, Assert); nomes descritivos (`GivenX_WhenY_ThenZ`)
- [ ] Nenhum teste acede BD real fora de Integration/Functional — usar Testcontainers, não in-memory provider do EF Core
- [ ] Redis testado com container em cenários específicos de cache; suite principal prova que a app funciona com Redis indisponível
- [ ] `dotnet test` corre sem warnings; `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` no `.csproj`

---

## 16. Deployment & Configuration

### 16.1 Ambiente

- [ ] `appsettings.json` não tem secrets; `appsettings.Production.json` é git-ignored
- [ ] Secrets vêm de variáveis de ambiente em produção (Render)
- [ ] ConnectionString e API keys (Stripe, Resend, Cloudinary, Upstash) vêm de `IConfiguration`
- [ ] Central Package Management (`Directory.Packages.props`) — versão exacta adicionada apenas quando existe consumidor real

### 16.2 Container

- [ ] Dockerfile restaura projectos antes de copiar source (cache de build)
- [ ] Publica explicitamente `Api`; usa imagem runtime; corre como utilizador não root
- [ ] Escuta a porta fornecida pelo Render; propaga shutdown e `CancellationToken`
- [ ] Exclui `backend-python/`, secrets, testes e artefactos locais do build context

### 16.3 Limitações do plano gratuito (aceites para MVP)

- [ ] Render free suspende a API após inactividade — cold start esperado
- [ ] Sem múltiplas instâncias, sem workers em background, sem pre-deploy command
- [ ] Migration corre como passo de release controlado, não automaticamente no arranque
- [ ] QStash com intervalo ≥ 20 min para preservar scale-to-zero de Render e Neon

---

## 17. Security Hardening

- [ ] Nenhuma query SQL construída com string concatenation — sempre parametrizada via EF Core
- [ ] OWASP Top 10 revisto antes de cada release relevante (SQL Injection, XSS, CSRF — ver secção 3.3)
- [ ] Nenhum secret em `appsettings.json`, `.env` ou logs
- [ ] HTTPS obrigatório em produção; CORS whitelist específica (não wildcard)
- [ ] Content-Security-Policy e `X-Frame-Options: DENY` configurados

---

## 18. Pre-Deployment Checklist

- [ ] `dotnet test` — todos os testes (Domain, Application, Integration, Functional, Architecture) a passar
- [ ] `dotnet build /p:TreatWarningsAsErrors=true` sem warnings
- [ ] `dotnet format` sem alterações pendentes
- [ ] Modelo EF Core sem alterações pendentes (`dotnet ef migrations has-pending-model-changes`)
- [ ] Migrations aplicadas contra staging DB
- [ ] `/health/live` e `/health/ready` respondem correctamente
- [ ] Sentry DSN configurado; OpenTelemetry exporter confirmado
- [ ] Rate limiting activo; CORS configurado correctamente
- [ ] JWT/refresh token secrets fortes; cookie de refresh com flags correctas
- [ ] QStash endpoint interno valida assinatura correctamente
- [ ] OpenAPI/Swagger actualizado
- [ ] Rollback procedure documentada

---

## Referências

Documentos de arquitectura:
- `00_ARCHITECTURE.md` — arquitectura aprovada v3.0 (fonte de verdade)
- `01_DATABASE_SCHEMA.md` — schema alvo
- `02_SPRINTS_ROADMAP.md` — plano de sprints
- `03_DEVELOPER_GUIDE.md` — guia de desenvolvimento

Stack aprovado (ver `00_ARCHITECTURE.md §14` para lista completa):
- .NET 10 LTS / C# 14
- ASP.NET Core Controllers + `Microsoft.AspNetCore.OpenApi`
- EF Core 10 + `Npgsql.EntityFrameworkCore.PostgreSQL`
- ASP.NET Core Identity + `Microsoft.AspNetCore.Authentication.JwtBearer`
- FluentValidation (core, sem `FluentValidation.AspNetCore`)
- HybridCache + Upstash Redis
- `Microsoft.AspNetCore.RateLimiting` (nativo)
- `ILogger` + Sentry + OpenTelemetry
- xUnit + WebApplicationFactory + Testcontainers
- Stripe.net, Resend SDK, Cloudinary

**Fora do baseline** (não instalar): `Microsoft.EntityFrameworkCore.Npgsql` (usar `Npgsql.EntityFrameworkCore.PostgreSQL`), `IRepository<T>` genérico, MediatR, AutoMapper, MassTransit/RabbitMQ, Serilog file sink, `FluentValidation.AspNetCore`.

---

*Última actualização: Agosto 2026 | Leandro Alves — PT Manager*
