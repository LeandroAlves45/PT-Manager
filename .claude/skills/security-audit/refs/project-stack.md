# Stack do projeto — PT Manager (multi-camada)

**Regra:** cada auditoria usa o **repositório aberto**, não um stack fixo global. Monorepo `backend/` + `frontend/`.

Carregar no **Step 1** antes de `language.md` e checklists.

**Fora de escopo:** `backend-python/` (salvo pedido explícito).

---

## 1. Onde ler versões (ordem de prioridade)

| Fonte | Uso |
| ----- | --- |
| `backend/**/packages.lock.json` | Pacotes NuGet **efetivamente restaurados** |
| `backend/Directory.Build.props` | `TargetFramework`, `LangVersion` |
| `backend/**/*.csproj` | Referências directas |
| `frontend/package-lock.json` | npm **instalado** (preferir a ranges do `package.json`) |
| `frontend/package.json` | Intervalo semver — só se lockfile ausente |

Comandos úteis:

```bash
# Backend
cd backend
dotnet --version
dotnet list package --include-transitive | head -50
dotnet list package --vulnerable --include-transitive

# Frontend
cd frontend
npm ls react react-dom vite axios --depth=0
npm audit --omit=dev
```

**Foco:** comportamento e CVEs da versão **instalada**.  
**Última lançada:** só **INFO**, nunca CRITICAL/HIGH só por estar desatualizado.

---

## 2. Stack canónica PT Manager

| Camada | Tecnologia | Notas de auditoria |
|--------|------------|-------------------|
| Backend API | ASP.NET Core, .NET 10, C# 14 | Controllers finos, handlers por feature |
| Auth | ASP.NET Identity, JWT curto, refresh opaco (hash) | Access em memória no frontend |
| Multi-tenant | `ITenantContext`, Global Query Filters | Fail-closed; nunca `trainer_id` do request |
| Persistência | EF Core, PostgreSQL (Neon) | Migrations EF; sem SQL concatenado |
| Cache | Redis (Upstash) | Reconstruível; **não** fonte de auth/billing |
| Jobs | QStash + outbox PostgreSQL | Tenant explícito no scope |
| Billing | Stripe (backend) | Raw body, assinatura, dedup `event.id` |
| Frontend | React 19, Vite 7, Tailwind 4 | SPA — **sem Next.js** |
| Deploy | Render (API), Vercel (SPA) | Headers podem estar na plataforma |

---

## 3. Mapa de superfície (filesystem)

### Backend (`backend/src/`)

```
Api/           → Controllers, contratos HTTP, composition root
Application/   → Handlers, validators, portas (Features/*)
Infrastructure/→ EF, Identity, Stripe, jobs, stores
Domain/        → Entidades, value objects, regras
```

Paths críticos:

| Tipo | Glob / padrão |
|------|----------------|
| Endpoints HTTP | `Api/**/Controllers/**/*.cs`, `Api/**/Endpoints/**/*.cs` |
| Handlers | `Application/Features/**/*Handler.cs` |
| Validators | `Application/Features/**/*Validator.cs` |
| Tenant | `**/ITenantContext*`, `TenantWriteValidationInterceptor.cs` |
| Auth | `Infrastructure/Identity/**`, JWT middleware |
| Webhooks Stripe | `**/Billing/**`, `ProcessPaymentWebhook*` |
| Jobs | `Infrastructure/Jobs/**` |
| EF config | `Infrastructure/Data/**`, `PtManagerDbContext.cs` |

### Frontend (`frontend/src/`)

```
api/           → axios, chamadas HTTP
context/       → AuthContext (JWT em memória)
pages/         → rotas
components/    → UI (incl. ProtectedRoute)
```

| Tipo | Glob / padrão |
|------|----------------|
| HTTP client | `api/axiosConfig.js`, `api/**/*.js` |
| Auth state | `context/AuthContext.jsx` |
| Rotas protegidas | `components/ProtectedRoute.jsx`, router config |
| Env exposto | `import.meta.env.VITE_*` |

---

## 4. Gates por camada

### Backend (.NET 10)

- `Result` / `Result<T>` para falhas esperadas; Problem Details na API
- `CancellationToken` em I/O assíncrono
- Sem `IRepository<T>` genérico — stores por feature
- Testes cross-tenant negativos em features tenant-owned

### Frontend (React 19 + Vite 7)

- **Não** procurar `middleware.ts`, `proxy.ts`, Server Actions, `next.config`
- Auth real está no backend; `ProtectedRoute` é UX, não segurança
- Tokens sensíveis: memória OK; localStorage para JWT/refresh → **HIGH**

### Billing (sempre em auditoria full)

Carregar `refs/aspnet-checklist.md` § Billing + skill `payment-security`:

- Webhook: raw body, `Stripe-Signature`, dedup, idempotência, outbox
- Metadata Stripe não concede autorização sozinha
- Sem PAN/CVV em logs ou DB

---

## 5. Dependências — escopo duplo

### Frontend

```bash
cd frontend && npm audit --omit=dev
```

Pacotes críticos frequentes: `react`, `react-dom`, `vite`, `axios`, `@sentry/react`.

### Backend

```bash
cd backend && dotnet list package --vulnerable --include-transitive
```

Pacotes críticos frequentes: `Microsoft.AspNetCore.*`, `Npgsql`, `Stripe.net`, `System.IdentityModel.Tokens.Jwt`.

---

## 6. Bloco obrigatório no relatório

```
Stack detectado (instalado):
  Backend:  net10.0, LangVersion 14, EF Core <packages.lock.json>
  Frontend: react@<lockfile>  vite@<lockfile>  (sem next)
  Auth:     ASP.NET Identity + JWT + refresh HttpOnly cookie
  DB:       PostgreSQL (Neon)
  Billing:  Stripe (backend) — checklist incluído
  Deploy:   Render (API) + Vercel (SPA)

Escopo excluído: backend-python/ (referência histórica)
Última versão registry (INFO, opcional):
  react@<npm view> — Δ N patches (sem CVE → não elevar severidade)
```

---

## 7. Headers (CSP / HSTS)

Não auditar como gap crítico só porque o repo não define CSP. Seguir **`refs/security-headers.md`**: severidade máxima INFO/LOW; headers podem estar no Render/Vercel.

---

## 8. O que NÃO fazer

- Não auditar como Next.js (não existe `next` no frontend)
- Não exigir `middleware.ts` / Server Actions
- Não reportar HIGH por “.NET desatualizado” sem CVE/advisory
- Não confiar em `trainer_id` de query/body como padrão seguro
- Não misturar lockfiles de `backend/` e `frontend/` sem escopo explícito
- Não auditar `backend-python/` por defeito

---

## 9. Escopo parcial

Se o utilizador passar path:

| Path | Ler lockfile de | Checklist |
|------|-----------------|-----------|
| `backend/src/Api` | `backend/` | aspnet |
| `backend/src/Application/Features/Billing` | `backend/` | aspnet + Billing |
| `frontend/src` | `frontend/` | react-vite |
| raiz | ambos | aspnet + react-vite + Billing |
