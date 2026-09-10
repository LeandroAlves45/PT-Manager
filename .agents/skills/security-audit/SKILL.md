---
name: security-audit
description: 'Auditoria de segurança PT Manager — ASP.NET Core (.NET 10) + React/Vite, multi-tenant, Stripe, PostgreSQL. Usa versões instaladas (packages.lock.json + npm lockfile), dotnet/npm audit, anti-falso-positivo. Use em "auditar segurança", IDOR, tenant, webhooks Stripe, JWT, ou "/security-audit [path]". backend-python fora de escopo.'
---

# Security Audit — PT Manager

Scanner orientado a **pesquisador de segurança**: contexto, fluxo de dados e mitigações na **stack real do repositório** — não templates de outro framework.

**Idioma do relatório:** português de Portugal (termos OWASP/CVE podem ficar em inglês).

**Monorepo:** `backend/` (.NET 10, C# 14) + `frontend/` (React 19, Vite 7). Cada execução lê lockfiles **do path auditado**.

**Fora de escopo por defeito:** `backend-python/` (referência histórica — só auditar com pedido explícito).

## Princípios

1. **Versão instalada > última lançada** — CVEs na versão do lockfile; “há versão mais nova” é só **INFO** sem advisory
2. **Evidência > padrão** — não reportar só por `FromSqlRaw`, `AllowAnonymous`, ausência de CSP em `vercel.json`, ou `ProtectedRoute` no frontend sem auth no servidor
3. **Rastrear imports e pipeline** — auth em filters, `ITenantContext`, handlers, interceptors
4. **Multi-tenant fail-closed** — nunca confiar em `trainer_id` do request; validar cross-tenant
5. **Auto-verificação** — `refs/false-positives.md` em todo achado
6. **Patches só como proposta** — nunca aplicar no repo automaticamente
7. **Billing incluído** — auditoria completa inclui sempre checklist Stripe (`payment-security` + `refs/aspnet-checklist.md` § Billing)

## Relação com outras skills

| Pedido | Skill |
|--------|-------|
| Auditoria completa do repo | **security-audit** (esta) |
| Review de PR / diff | `security-reviewer` |
| Billing / PCI / webhooks Stripe (detalhe) | `payment-security` |
| Dúvida pontual API auth | `security` |

Fontes canónicas: `AGENTS.md`, `.claude/project/00_ARCHITECTURE.md` (§6 auth/tenant, §10 Stripe), `.cursor/rules/security.md`.

## Fluxo de execução (ordem fixa)

### Step 1 — Escopo, stack e versões

1. Path informado → só esse escopo; senão → raiz (respeitar `backend/` e `frontend/`)
2. **Obrigatório:** `refs/project-stack.md` — versões instaladas, superfície detectada
3. Comandos recomendados:
   ```bash
   # Backend
   cd backend && dotnet list package --vulnerable --include-transitive

   # Frontend
   cd frontend && npm ls react react-dom vite --depth=0
   ```
   Lockfiles: `backend/**/packages.lock.json`, `frontend/package-lock.json`
4. Carregar `refs/language.md` + mapear superfície:
   - `backend/` → `refs/aspnet-checklist.md` §1
   - `frontend/` → `refs/react-vite-checklist.md` §1
5. Se escopo inclui billing ou auditoria full → `refs/aspnet-checklist.md` § Billing + skill `payment-security`

### Step 2 — Dependências

```bash
# Frontend (runtime)
cd frontend && npm audit --omit=dev

# Backend
cd backend && dotnet list package --vulnerable --include-transitive
```

- CVE/GHSA apenas para pacotes **deste** lockfile
- Versão citada = **instalada**, não range do manifest
- Sem CVE → no máximo **INFO**
- Não reportar devDependencies sem uso em runtime

### Step 3 — Segredos e exposição

- `refs/secrets.md`
- `git ls-files` para ficheiros sensíveis **tracked**
- CI/CD (`.github/workflows/`), `appsettings*.json`, Render/Vercel env
- Respeitar ficheiros protegidos de `AGENTS.md` (nunca ler `.env` real)

### Step 4 — Scan profundo (código)

| Escopo | Checklist |
|--------|-----------|
| `backend/` | `refs/aspnet-checklist.md` |
| `frontend/` | `refs/react-vite-checklist.md` |
| Raiz / full | Ambos + Billing (§ aspnet) |

| Categoria | Foco PT Manager |
|-----------|-----------------|
| Injeção | EF Core raw SQL, XSS React, SSRF em HttpClient |
| AuthZ/AuthN | JWT, refresh cookie, roles, `[Authorize]`, handlers |
| Multi-tenant | `ITenantContext`, Global Query Filters, IDOR/BOLA cross-tenant |
| Dados | PII em logs, Problem Details sem stack trace |
| Crypto | refresh token hash, JWT exp, segredos fracos |
| Lógica | Stripe webhooks, QStash jobs, rate limit, idempotência |
| Config | CORS, `VITE_*`, headers (Render/Vercel) |

**Não** auditar `backend-python/` salvo pedido explícito.

### Step 5 — Fluxo entre ficheiros

```
Entrada HTTP (body, query, route, headers, cookies)
  → validação (FluentValidation / validators)
  → autenticação (JWT, Identity)
  → tenant (ITenantContext — fail-closed)
  → autorização (roles, ownership, ActorAuthorization)
  → sink (EF Core, HttpClient, HTML, Cloudinary, Stripe)
```

Frontend: UI protegida **não** substitui auth no backend.

Jobs/webhooks: tenant explícito no scope — ver `00_ARCHITECTURE.md` §6.4.

### Step 6 — Anti-falso-positivo

`refs/false-positives.md` — descartar ou rebaixar; listar descartes no relatório.

### Step 7 — Relatório

`refs/report.md` — incluir bloco **Stack detectado** de `project-stack.md` §8.

### Step 8 — Patches (CRITICAL e HIGH)

Before/after; frase: **"Revise cada patch antes de aplicar. Nada foi alterado no repositório."**

**Não** incluir patches genéricos de CSP/HSTS (hardening opcional → INFO em `refs/security-headers.md` §7).

## Guia de severidade

| Nível    | Significado |
| -------- | ----------- |
| CRITICAL | Exploração imediata (SQLi, RCE, bypass auth, secret live no Git, cross-tenant data leak) |
| HIGH     | Exploit claro (IDOR, XSS stored, webhook sem assinatura, refresh token em localStorage) |
| MEDIUM   | Condições ou encadeamento (CORS misconfig + cookies, race em checkout) |
| LOW      | Boas práticas pontuais |
| INFO     | Sem CVE; versão atrás da latest; CSP/HSTS não custom |

## Regras de saída

- Tabela resumo primeiro
- Achados por **categoria**
- Path + linha + snippet
- **Stack detectado (instalado)** sempre no cabeçalho
- Secção **Billing/Stripe** em auditorias full
- Comparar com latest só em INFO
- Falsos positivos descartados quando houver

## Referências

| Ficheiro | Uso |
| -------- | --- |
| `refs/project-stack.md` | Step 1 — versões e gates (obrigatório) |
| `refs/language.md` | Padrões C# / ASP.NET Core / React-Vite |
| `refs/aspnet-checklist.md` | Steps 4–5 backend + Billing |
| `refs/react-vite-checklist.md` | Steps 4–5 frontend |
| `refs/aspnet-middleware.md` | Pipeline ASP.NET, auth, tenant |
| `refs/secrets.md` | Step 3 |
| `refs/false-positives.md` | Step 6 |
| `refs/security-headers.md` | CSP/HSTS — Render + Vercel |
| `refs/report.md` | Step 7 |

## Documentação externa

- ASP.NET Core security: Microsoft Learn (versão alinhada ao `TargetFramework`)
- Stripe webhooks: skill `payment-security` + `00_ARCHITECTURE.md` §10
- OWASP API Security Top 10, ASVS

## Atalhos grep (pistas, não achados)

```
FromSqlRaw / ExecuteSqlRaw / SqlQueryRaw
trainer_id / TrainerId (body, query, route)
AllowAnonymous
ITenantContext
TenantWriteValidationInterceptor
ConstructEvent / Stripe-Signature
localStorage / sessionStorage
dangerouslySetInnerHTML
sk_live_ / whsec_
IgnoreQueryFilters
```

Confirmar contexto antes de reportar.
