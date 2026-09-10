# Checklist de vulnerabilidades — ASP.NET Core / PT Manager Backend

Referência para **Step 4** e **Step 5**. Versões: **`refs/project-stack.md`**.

Fontes: `AGENTS.md`, `00_ARCHITECTURE.md` §6 (auth/tenant), §10 (Stripe), OWASP API Top 10, ASVS.

**Auditoria full:** incluir sempre **§ Billing** (decisão do projecto).

---

## 1. Mapa de superfície (Step 1)

Localizar e listar no relatório:

| Tipo | Glob / padrão |
|------|----------------|
| Controllers | `backend/src/Api/**/Controllers/**/*.cs` |
| Handlers | `backend/src/Application/Features/**/*Handler.cs` |
| Validators | `backend/src/Application/Features/**/*Validator.cs` |
| Stores / EF | `backend/src/Infrastructure/Persistence/**/*.cs` |
| DbContext | `backend/src/Infrastructure/Data/PtManagerDbContext.cs` |
| Tenant | `ITenantContext`, `TenantWriteValidationInterceptor` |
| Identity / JWT | `backend/src/Infrastructure/Identity/**` |
| Webhooks | `**/Billing/**`, `*Webhook*` |
| Jobs | `backend/src/Infrastructure/Jobs/**` |
| Bypass admin | operações com `IgnoreQueryFilters` |

---

## 2. Autenticação e autorização

### JWT e refresh

- [ ] Access token JWT curto (≈15 min, configurável)
- [ ] Refresh token opaco, rotativo, guardado **apenas como hash**
- [ ] Validação: assinatura, issuer, audience, expiração
- [ ] Refresh em cookie `HttpOnly`, `Secure`, `SameSite` adequado
- [ ] Rate limiting em login, signup, password reset, convites

### Controllers e handlers

- [ ] Endpoints sensíveis com `[Authorize]` ou equivalente explícito
- [ ] `[AllowAnonymous]` só em rotas públicas documentadas (`/health`, auth pública, webhook Stripe com assinatura)
- [ ] Handlers validam actor/role via `ActorAuthorization` ou similar — **não** só no controller
- [ ] Roles: `superuser`, `trainer`, `client` — sem escalação via body (`role` no request)

### Multi-tenant (crítico PT Manager)

- [ ] `ITenantContext` resolve trainer do utilizador autenticado ou contexto interno validado
- [ ] **Nunca** confiar em `trainer_id` / `TrainerId` de body, query ou route para autorização
- [ ] Global Query Filters aplicados no `PtManagerDbContext`
- [ ] `TenantWriteValidationInterceptor` valida escritas cross-tenant
- [ ] `IgnoreQueryFilters` restrito, auditado, bypass admin explícito
- [ ] Testes cross-tenant negativos existem para features tenant-owned

### IDOR / BOLA

- [ ] Lookups por ID incluem filtro de tenant ou ownership
- [ ] Cliente não acede a recursos de outro trainer/cliente
- [ ] Superuser bypass documentado e restrito

---

## 3. Injeção

### EF Core

- [ ] Proibir concatenação de input em SQL
- [ ] `FromSqlRaw` / `ExecuteSqlRaw` só com parâmetros — ver `refs/false-positives.md`
- [ ] Preferir LINQ tipado e `Where` parametrizado
- [ ] Segundo vector: dados maliciosos **armazenados** e exibidos depois (stored XSS via API)

### Outros

- [ ] `Process.Start`, paths de ficheiro com input do utilizador
- [ ] `HttpClient` com URL controlada pelo utilizador → SSRF (allowlist)

---

## 4. Validação e erros

- [ ] FluentValidation (ou validators explícitos) na Application antes de persistir
- [ ] Contrato HTTP: tipos, campos obrigatórios, limites de tamanho na API
- [ ] Problem Details sem stack trace, schema interno ou paths de servidor em produção
- [ ] Logs sem passwords, tokens, refresh, PII desnecessária

---

## 5. Jobs, webhooks e contexto sem HTTP

### QStash / jobs

- [ ] Job transporta `TrainerId` persistido e validado
- [ ] Dispatcher cria scope e `ITenantContext` antes do handler
- [ ] Jobs não assumem tenant de `HttpContext`

### Webhooks genéricos

- [ ] Autenticação do caller (QStash signature, etc.)
- [ ] Idempotência e deduplicação

---

## 6. Billing / Stripe (obrigatório em auditoria full)

Complementar com skill **`payment-security`**.

- [ ] Endpoint webhook: **raw body** preservado para verificação de assinatura
- [ ] `Stripe-Signature` validada com secret do ambiente (`ConstructEvent` ou gateway equivalente)
- [ ] Deduplicação por `event.id` — eventos duplicados não reprocessam estado
- [ ] Idempotência em mutações de checkout/subscription
- [ ] Metadata recebida da Stripe **não** concede autorização por si só — reconciliar com IDs persistidos
- [ ] Eventos fora de ordem: reconciliação com Stripe, não só sequência de entrega
- [ ] Resposta 2xx para eventos autenticados não suportados (evitar retry infinito)
- [ ] Erro de persistência → resposta de erro para retry Stripe
- [ ] Logs sanitizados — sem PAN, CVV, secrets; Stripe request ID OK
- [ ] Rate limiting no endpoint webhook
- [ ] Outbox transacional para side effects (notificações, etc.)

---

## 7. CORS, rate limit e config

- [ ] CORS com allowlist via env — não `AllowAnyOrigin` com credenciais
- [ ] Rate limiting configurado (Redis/Upstash) em auth e endpoints sensíveis
- [ ] Secrets em env / User Secrets — não em `appsettings.Production.json` commitado
- [ ] `/health/ready` vs `/health/live` — readiness não expõe dados sensíveis

Ver **`refs/security-headers.md`** para CSP/HSTS (Render).

---

## 8. Fluxo de dados (Step 5)

```
HTTP Request (body, query, route, headers, cookies)
  → validação contrato + FluentValidation
  → autenticação JWT / Identity
  → ITenantContext (fail-closed)
  → autorização (role, ownership, ActorAuthorization)
  → handler → store → EF Core / Stripe / HttpClient / outbox
```

Vulnerabilidades cross-file: controller com `[Authorize]` mas handler que ignora tenant; store que aceita `trainerId` do DTO.

---

## 9. Comandos pós-scan sugeridos

```bash
cd backend
dotnet build PTManager.sln --configuration Release
dotnet test PTManager.sln --configuration Release --filter "Category=CrossTenant"
dotnet list package --vulnerable --include-transitive
```
