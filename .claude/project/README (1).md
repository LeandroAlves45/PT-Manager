# PT Manager Backend — C# / .NET 10 Migration

*Reescrita completa do backend de Python para C# com Clean Architecture*

---

## Executive Summary

PT Manager backend está sendo reescrito de Python (FastAPI) para C# (.NET 10 LTS), modular monolith com Clean Architecture, com objetivo de alcançar um MVP em produção em 12 semanas.

**Timeline:** Julho 2026 — Outubro 2026
**Stack:** .NET 10 · C# 14 · PostgreSQL 16 (Neon) · Entity Framework Core 10 · Upstash Redis · Upstash QStash
**Architecture:** Modular monolith, Clean Architecture (Domain → Application → Infrastructure → Api)
**Team:** Solo developer (Alves)

---

## Documentos de Referência

Este repositório contém a documentação completa:

| Documento | Conteúdo |
|-----------|----------|
| **00_ARCHITECTURE.md** | Arquitectura sistema (v3.0), stack tecnológico, padrões, decisões de MVP |
| **01_DATABASE_SCHEMA.md** | Schema PostgreSQL alvo (28 tabelas, spec para o modelo EF Core) |
| **02_SPRINTS_ROADMAP.md** | Planeamento 12 semanas, 8 sprints, tarefas detalhadas |
| **03_DEVELOPER_GUIDE.md** | Setup local, workflow diário, troubleshooting |
| **README (1).md** | Este ficheiro (overview) |

`04_PRODUCTION_CHECKLIST.md` ainda não existe — a criar antes do Sprint 8 (Produção), com o runbook e o procedimento de rollback referidos em `02_SPRINTS_ROADMAP.md`.

---

## Timeline Sumário

```
SPRINT 0   (Semana 0 — 3 dias)  Setup + Decisões
SPRINT 1   (Semanas 1-2)        Domain Layer (Entities + Value Objects por feature)
SPRINT 2   (Semanas 3-4)        Infrastructure + EF Core (DbContext, migration, Repos)
SPRINT 3   (Semanas 5-6)        Application (Handlers por feature, DTOs, Validators)
SPRINT 4   (Semanas 7-8)        API Controllers + Auth (40 Endpoints)
SPRINT 5   (Semana 9)           Jobs Duráveis + Outbox (QStash, Resend, Stripe, Cloudinary)
SPRINT 6   (Semana 10)          Observabilidade (ILogger, OpenTelemetry, Sentry)
SPRINT 7   (Semana 11)          Testing + CI/CD (~170 tests + Architecture Tests, GitHub Actions)
SPRINT 8   (Semana 12)          Production Setup (Deploy Render free tier, QStash produção)
```

**Data estimada de go-live:** Outubro 2026

---

## Estructura Directórios

```
backend/
├── src/
│   ├── Api/              ← HTTP Controllers, Middlewares
│   ├── Domain/           ← Entities, Value Objects, Interfaces
│   ├── Application/      ← Handlers (por feature), DTOs, Validators
│   └── Infrastructure/   ← Repositórios, EF Core, External Services
├── tests/
│   ├── Domain.UnitTests/
│   ├── Application.UnitTests/
│   ├── Infrastructure.IntegrationTests/
│   ├── Api.FunctionalTests/
│   └── ArchitectureTests/   ← ~200 testes no total
├── .github/workflows/       (na raiz do monorepo)
│   ├── ci.yml           ← GitHub Actions (backend + frontend)
│   └── deploy.yml       ← Deploy (backend + frontend)
├── Dockerfile
├── Directory.Build.props
├── Directory.Packages.props
└── PTManager.sln
```

---

## Decisões Técnicas Confirmadas

Ver justificação completa e trade-offs em `00_ARCHITECTURE.md`.

### .NET 10 LTS
- **Razão:** Longo suporte (até Nov 2028), true multi-threading (sem GIL) vs Python
- **C# 14:** Obrigatório em .NET 10, type-safety completo

### ASP.NET Core 10 — Controllers
- **Controllers vs Minimal APIs:** Controllers (mais familiar para transição vinda do Python)
- **Framework:** Async/await nativo, built-in DI, middleware pipeline

### Entity Framework Core 10
- **ORM:** Global Query Filters ligados a `ITenantContext` (nunca a `HttpContext` direto)
- **Multi-tenancy:** Enforçado em database level (impossível esquecer o filtro numa query)

### Upstash QStash — sem RabbitMQ/MassTransit no MVP
- **Razão:** o plano gratuito do Render suspende a API sem tráfego e não tem background workers — um broker dedicado (RabbitMQ) ou um worker in-process (Hangfire/Quartz) não seria fiável nesse ambiente
- **Solução:** QStash chama um endpoint interno assinado a cada vinte minutos (intervalo escolhido para preservar a suspensão gratuita do Render e do scale-to-zero do Neon, `00_ARCHITECTURE.md §9.1`), que ativa o dispatcher de `durable_jobs`/`outbox_messages` em Postgres (at-least-once, idempotente)
- **RabbitMQ** fica documentado como reavaliação futura — só com sinais concretos de múltiplos consumers, throughput ou latência incompatíveis com polling (`00_ARCHITECTURE.md §9.5`)

### PostgreSQL 16 (Neon)
- **Database:** Sem mudanças de motor face ao Python
- **Backups:** Neon automáticos, serverless scaling
- **IDs:** `uuid` nativo (não `varchar(36)` como no Python) — ver `01_DATABASE_SCHEMA.md` Decisão 1

### FluentValidation (core)
- **Validação:** Chamada explicitamente pelos handlers (sem `.AspNetCore` nem pipeline automático), async validators, mais expressivo que Pydantic

### ILogger + OpenTelemetry + Sentry
- **Logging:** Structured JSON, correlation IDs — sem Serilog file sink (filesystem efémero no Render)
- **Monitoring:** OpenTelemetry (traces/métricas) + Sentry (erros) em produção

---

## Dependências Python → C# Mapeamento

| Python | C# | Notas |
|--------|----|----|
| FastAPI | ASP.NET Core 10 Controllers | |
| SQLModel | EF Core 10 (Npgsql provider) | |
| Pydantic v2 | FluentValidation (core) | Chamado explicitamente pelos handlers |
| Python logging | `ILogger` + OpenTelemetry | Sem Serilog file sink |
| Resend SDK | Resend.NET ou `HttpClient` tipado | |
| Stripe SDK | Stripe.net | |
| Cloudinary SDK | CloudinaryDotNet | |
| APScheduler + Celery (planeado) | Upstash QStash + dispatcher `durable_jobs` | Sem RabbitMQ/MassTransit no MVP |
| pytest | xUnit | + WebApplicationFactory, Testcontainers |
| Redis-py | Provider Redis compatível com HybridCache | Upstash Redis |

---

## Entregáveis por Sprint

| Sprint | Semanas | Entrega Principal | Validação |
|--------|---------|------------------|-----------|
| 0 | 0 | Repo setup, decisões doc | Compila |
| 1 | 1-2 | Entities + Value Objects por feature | Testes unitários passam |
| 2 | 3-4 | DbContext, migration `InitialCreate`, Repositórios | Testes integração contra PostgreSQL |
| 3 | 5-6 | Handlers por feature, DTOs, Validators | Testes unitários passam |
| 4 | 7-8 | 40 Endpoints, Auth JWT+refresh, Multi-tenancy | Testes integração dos endpoints |
| 5 | 9 | Dispatcher QStash, Outbox, Resend, Stripe, Cloudinary | Reclamação de jobs, retry, idempotência testados |
| 6 | 10 | ILogger, OpenTelemetry, Sentry, Correlation IDs | Logs estruturados em produção |
| 7 | 11 | ~170 testes + Architecture Tests, GitHub Actions CI/CD | Todos testes passam, pipeline verde |
| 8 | 12 | Deploy Render (free), QStash produção, Docs | Go-live validado, rollback testado |

---

## Requisitos Locais

```
.NET 10.0 SDK                    https://dotnet.microsoft.com
PostgreSQL 16 ou Neon            https://neon.tech
Docker                           https://docker.com
Visual Studio 2026 ou VS Code    https://visualstudio.microsoft.com
Git                              https://git-scm.com
```

---

## Quick Start Local

```bash
# 1. Clone
git clone https://github.com/seu-repo/ptmanager.git
cd backend

# 2. Restore
dotnet restore

# 3. Setup Database
createdb ptmanager_dev
dotnet ef database update --project src/Infrastructure

# 4. Run
dotnet run --project src/Api
# http://localhost:5000/swagger

# 5. Tests
dotnet test
```

---

## Git Workflow (Global)

Workflow compartilhado entre backend e frontend:

```
.github/workflows/
├── ci.yml          ← Test backend + frontend em paralelo
└── deploy.yml      ← Deploy backend + frontend em paralelo
```

**Branch naming:**
- `feature/auth-jwt-refresh`
- `bugfix/meal-plan-calculation`
- `test/add-repository-tests`

**Commit messages:**
- `feat: add JWT token refresh endpoint`
- `fix: correct macro calculation`
- `test: add validation tests`

---

## Segurança

### Multi-tenancy
- Global Query Filters: impossível cross-tenant access
- `owner_trainer_id` em todas business entities
- JWT sempre valida identidade

### Authentication
- Access token JWT de curta duração (15 min), refresh token opaco (30 dias) com rotação e deteção de reuso
- Apenas o hash do refresh token é persistido (`refresh_tokens.token_hash`)
- Role-based access (superuser, trainer, client)
- Um eventual header de API key legado nunca é tratado como autenticação (`00_ARCHITECTURE.md §5.3`)

### Secrets
- Nenhum API key em source
- Environment variables em Render
- Senha hashing: bcrypt
- CORS origins locked down

---

## Performance Targets

| Métrica | Target | Método |
|---------|--------|--------|
| API response p95 | < 500ms | Apache Bench |
| Database queries | < 100ms (p95) | EF Core profiler |
| Startup time | < 2s | dotnet run timing |
| Memory footprint | < 500MB | Profiler check |
| Throughput | > 1000 req/s | Load test |

---

## Observabilidade

### Logging
- `ILogger` structured JSON (sem file sink — filesystem efémero no Render)
- Correlation ID per request
- Prefixos de domínio: [AUTH], [NUTRITION], [BILLING], [JOBS]

### Monitoring
- Sentry error tracking + OpenTelemetry traces/métricas
- Health endpoints: `GET /health/live`, `GET /health/ready`
- Database connectivity check (readiness apenas)

### Metrics (Pós-Launch)
- Request count, latency percentiles
- Jobs pendentes, tentativas e dead-letter (`durable_jobs`)
- Database connection pool utilization
- Cache hit ratio e falhas Redis

---

## Roadmap Futuro (Pós-Sprint 8)

Após produção:

- **Phase 9:** Reavaliar RabbitMQ apenas se surgirem sinais concretos (múltiplos consumers, throughput incompatível com polling — `00_ARCHITECTURE.md §9.5`)
- **Phase 10:** Machine learning (recomendações nutrição)
- **Phase 11:** GraphQL API (alternativa REST)
- **Phase 12:** Mobile app (React Native)

---

## Recursos

### Documentação Oficial
- [.NET 10 Docs](https://learn.microsoft.com/en-us/dotnet/)
- [ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/)
- [EF Core](https://learn.microsoft.com/en-us/ef/core/)
- [C# 14](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-14)

### Livros Recomendados
- "Clean Architecture" — Robert C. Martin
- "Domain-Driven Design" — Eric Evans
- "The Pragmatic Programmer" — Hunt & Thomas

### Ferramentas
- Visual Studio 2026
- VS Code + C# Dev Kit
- Docker Desktop
- Postman / Insomnia (API testing)

---

## FAQ

**Q: Por que não ficar em Python?**
A: Python + FastAPI não escala bem com GIL para concorrência real. C# + .NET 10 resolve isto nativamente com true multi-threading. Performance +2-4x, startup 10x mais rápido.

**Q: E se algo quebra em produção?**
A: Rollback procedure a documentar em `04_PRODUCTION_CHECKLIST.md` antes do Sprint 8. Database backup automático via Neon, código revert via Git, Render permite reverter para o deploy anterior.

**Q: Quanto tempo demora?**
A: 12 semanas (3 meses) seguindo roadmap. Pode ser mais rápido com mais developers, mas atualmente é solo.

**Q: Qual o custo?**
A: MVP assenta inteiramente em free tiers — Render Free, Neon Free, Upstash Free (Redis + QStash), Sentry Free. Custo total: $0/mês, com as limitações aceites documentadas em `00_ARCHITECTURE.md §13.2` (cold start, sem múltiplas instâncias, sem workers dedicados).

**Q: Database vai perder dados?**
A: Não. Migrations feitas incrementalmente, backup antes de cada deploy. Zero data loss garantido.

**Q: Como se trata a multi-tenancy?**
A: Global Query Filters em EF Core — todos queries filtram automaticamente por `owner_trainer_id`. Impossível esquecer filtro.

---

## Contacto & Suporte

**Project Lead:** Alves (solo developer)
**Documentation:** Este repositório
**Issues:** GitHub Issues (backend repo)
**CI/CD Status:** GitHub Actions

---

## Versão

- **Versão:** 2.0 (alinhado com `00_ARCHITECTURE.md` v3.0)
- **Data:** Julho 2026
- **Status:** Em Desenvolvimento (Sprint 0)
- **Próxima Revisão:** Agosto 2026 (após Sprint 1)

---

*PT Manager Backend Migration — .NET 10 LTS Edition*
