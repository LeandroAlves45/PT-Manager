# Falsos positivos — não reportar sem evidência

Carregar no **Step 6 (Self-Verification)**. Um achado só entra no relatório se passar por estas regras.

---

## Regra geral

Antes de reportar, responder **sim** a todas:

1. Existe **caminho de exploit** plausível (não só “padrão suspeito”)?
2. A entrada é **controlada pelo atacante** (request, upload, header, query, body)?
3. Não há **sanitização/validação/auth/tenant** no mesmo request ou upstream?
4. O código está em **caminho de produção** (não mock, teste, `backend-python/`, `.agents/`)?

Se qualquer resposta for “não” ou “incerto” → **descartar** ou **LOW + confiança LOW**.

---

## ASP.NET Core / EF Core

| Padrão | Por que NÃO é achado automático |
|--------|----------------------------------|
| `[AllowAnonymous]` em `/health`, login, refresh, webhook Stripe | Rotas públicas por design |
| Controller sem `[Authorize]` visível | Policy global, filter, ou endpoint público documentado |
| `FromSqlRaw` com placeholders `{0}` ou parâmetros | Query parametrizada |
| `FromSqlInterpolated` com FormattableString | API segura EF |
| LINQ `Where` com variável validada | ORM parametrizado |
| `IgnoreQueryFilters` em operação admin | Bypass explícito superuser — verificar restrição de role |
| Handler sem check explícito no ficheiro | `ActorAuthorization`, store com tenant obrigatório |
| `ITenantContext` injectado mas não usado no snippet | Rastrear store e Global Query Filters |
| Problem Details em dev com detalhe | Verificar `Development` vs `Production` |
| Test doubles com tokens fake | Excluir `*Tests*` |

**Reportar SQLi** quando: concatenação de string em SQL, `FromSqlRaw` com interpolação não parametrizada, ou input não validado em raw query.

**Reportar IDOR/tenant** quando: lookup por ID sem filtro de tenant/ownership **e** Global Query Filter bypassed ou ausente no caminho.

---

## Multi-tenant PT Manager

| Padrão | Por que NÃO é achado automático |
|--------|----------------------------------|
| `TrainerId` em entidade de domínio | Modelo de dados, não input de request |
| `trainer_id` em DTO de **resposta** | Serialização snake_case |
| Global Query Filter no DbContext | Mitigação central — confirmar entidade tenant-owned |
| Cross-tenant test que **espera** falha | Teste de segurança, não vulnerabilidade |

**Reportar** quando: body/query/route `trainer_id` usado em `Where`, store ou handler sem validar contra `ITenantContext`.

---

## Stripe / Billing

| Padrão | Por que NÃO é achado automático |
|--------|----------------------------------|
| `[AllowAnonymous]` no webhook endpoint | Auth via `Stripe-Signature` |
| Metadata Stripe lida **depois** de reconciliar customer/subscription persistidos | Fluxo correcto |
| Evento duplicado ignorado por dedup `event.id` | Idempotência |
| 2xx para event type não suportado mas autenticado | Evita retry infinito — ver arquitectura |

**Reportar** quando: processamento sem verificação de assinatura, ou autorização só por metadata sem reconciliação.

---

## React / Vite

| Padrão | Por que NÃO é achado automático |
|--------|----------------------------------|
| `dangerouslySetInnerHTML` com CSS de config interna (shadcn `chart.tsx`) | Sem input do utilizador |
| `{user.name}` em JSX | React escapa |
| `ProtectedRoute` sem validar JWT no cliente | Auth no backend — verificar API |
| `localStorage` em `*.test.jsx` ou `test/setup.js` | Testes |
| `import.meta.env.VITE_*` sem secret literal | Env injection OK |

**Reportar XSS** quando: HTML de fonte não confiável no DOM sem sanitização.

**Reportar sessão** quando: access/refresh token em `localStorage`/`sessionStorage` em produção.

---

## Segredos

| Padrão | Por que NÃO é achado automático |
|--------|----------------------------------|
| `Configuration["Stripe:SecretKey"]` | Binding config, não literal |
| Placeholders em `appsettings.Development.json` | Dev template |
| `.env.example` tracked com valores fake | Template |
| Ficheiro `.env` no `.gitignore` mas não tracked | OK |

**Reportar** literais: `sk_live_`, `whsec_` real, private keys, connection strings com password.

---

## Dependências

| Padrão | Por que NÃO é achado automático |
|--------|----------------------------------|
| Versão “antiga” sem CVE | **INFO** |
| `npm audit` moderate em devDependency não usada em runtime | Avaliar import tree |
| Patch .NET atrás do latest sem advisory | INFO |

---

## Ficheiros e pastas — excluir do scan profundo

- `node_modules/`, `dist/`, `bin/`, `obj/`, `coverage/`
- `backend-python/` (fora de escopo por defeito)
- `**/*Tests/**`, `*.Tests.cs`, `*.test.jsx`, `*.spec.*`
- `.agents/skills/**`, `.claude/skills/**`, `.cursor/skills/**`
- Migrations EF existentes (não editar — auditar apenas queries raw em código app)
- Lockfiles linha a linha (usar `npm audit` / `dotnet list package --vulnerable`)

---

## Severidade — quando rebaixar

| Situação inicial | Ajuste |
|------------------|--------|
| XSS em comentário TODO | Descartar |
| IDOR se recurso é público por regra de negócio | INFO ou descartar |
| “Falta middleware Next.js” | **Descartar** — PT Manager não usa Next |
| “Falta Server Action auth” | **Descartar** — não aplicável |
| Ausência CSP/HSTS no repo | **INFO** — ver `refs/security-headers.md` |
| CORS `*` em webhook **só** POST com assinatura HMAC | Verificar assinatura antes de reportar CORS |
| `console.log` em catch frontend | LOW — HIGH só se logar token/password |
| Projeto atrás da latest sem CVE | INFO |

Documentar descartes na secção **“Falsos positivos descartados”** do relatório.
