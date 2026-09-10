# Pipeline ASP.NET Core — auth, tenant e middleware

Carregar no **Step 4** ao revisar auth na borda, `[AllowAnonymous]`, CORS, rate limiting e tenant resolution.

**PT Manager não usa Next.js** — não procurar `middleware.ts` nem `proxy.ts` no frontend.

---

## 1. Onde vive a segurança na borda

| Camada | Responsabilidade |
|--------|------------------|
| ASP.NET middleware pipeline | JWT bearer, CORS, rate limit, exception handler |
| Filters / authorization | `[Authorize]`, policies, roles |
| Handlers (Application) | Validação, `ITenantContext`, regras de negócio |
| EF Core | Global Query Filters, interceptors |
| Frontend `ProtectedRoute` | UX apenas — **não** é controlo de segurança |

---

## 2. Ordem típica do pipeline (verificar no `Program.cs` / extensions)

1. Exception handling / Problem Details
2. HTTPS redirection (produção)
3. CORS (allowlist)
4. Authentication (JWT bearer)
5. Authorization
6. Rate limiting (se configurado)
7. Controllers / endpoints

**Não reportar** “falta middleware custom” se auth está no pipeline standard e handlers validam tenant.

---

## 3. `[AllowAnonymous]` — quando é OK

| Rota | Motivo |
|------|--------|
| `/health`, `/health/ready`, `/health/live` | Probes Render/K8s |
| Login, refresh, registo, reset password | Auth pública |
| Webhook Stripe | Protegido por **assinatura HMAC**, não JWT |
| Convite / activação (token opaco) | Validar token no handler |

**Reportar** quando rota anónima expõe dados tenant-owned ou mutações sem auth alternativa forte.

---

## 4. JWT vs refresh cookie

- Access token: header `Authorization: Bearer` — validado no middleware
- Refresh token: cookie HttpOnly — endpoint dedicado; rotação e hash em DB
- **Não** exigir refresh no header Authorization se o design usa cookie HttpOnly

---

## 5. Multi-tenant no pipeline

- `ITenantContext` scoped por request/job
- Resolvido **após** autenticação (ou contexto interno validado para jobs/webhooks)
- Fail-closed: operação tenant-owned sem tenant → erro, não “tenant null = todos”

Jobs QStash: construir `ITenantContext` explicitamente — sem `HttpContext`.

Webhooks Stripe: resolver trainer de IDs **persistidos**; metadata não autoriza sozinha.

---

## 6. CORS e cookies

- `AllowAnyOrigin()` + credenciais → **HIGH** se cookies de sessão/refresh cruzam origens
- Allowlist via env alinhada a Vercel (frontend) + Render (API)

---

## 7. Rate limiting

Esperado em:

- Login, signup, password reset
- Webhook Stripe (abuso)
- Endpoints de email / convites

Ausência isolada → **LOW/INFO** se não houver evidência de abuso; escalar se auth endpoints expostos sem limite.

---

## 8. Falsos positivos

| Padrão | Por que NÃO é achado automático |
|--------|----------------------------------|
| Controller sem `[Authorize]` visível | Policy global, convention, ou filter |
| Handler sem check explícito | Pode usar `ActorAuthorization` ou store que exige tenant |
| Health endpoint anónimo | Por design |
| Sem middleware custom de tenant | Tenant via `ITenantContext` + EF filters |

**Reportar bypass** quando mutação ou leitura sensível executa sem JWT **e** sem mecanismo equivalente (webhook assinado, job com tenant validado).

---

## 9. Integração

| Tema | Ficheiro |
|------|----------|
| Checklist backend | `refs/aspnet-checklist.md` |
| Billing webhooks | `refs/aspnet-checklist.md` §6 + `payment-security` |
| Headers | `refs/security-headers.md` |
| Falsos positivos | `refs/false-positives.md` |
