# Padrões por linguagem — C# / ASP.NET Core / React / Vite

Carregar no **Step 1** **depois** de `refs/project-stack.md`.

Complementa `refs/aspnet-checklist.md`, `refs/react-vite-checklist.md` e `refs/false-positives.md`.

---

## C# / ASP.NET Core — sinks de alto risco

Investigar quando **input do utilizador** possa alcançar:

```csharp
// SQL — CRITICAL se concatenação
context.Database.ExecuteSqlRaw($"SELECT ... WHERE id = {userId}")
context.Set<T>().FromSqlRaw($"SELECT ... {input}")

// Seguro — parametrizado
context.Database.ExecuteSqlRaw("SELECT ... WHERE id = {0}", userId)
context.Set<T>().FromSqlInterpolated($"...") // FormattableString parametrizado

// Process / ficheiros
Process.Start(userInput)
File.ReadAllText(basePath + userFilename) // path traversal

// HTTP — SSRF
await httpClient.GetAsync(userSuppliedUrl)
```

### EF Core (PT Manager)

```csharp
// Seguro — LINQ + Global Query Filters
await db.Clients.Where(c => c.Id == id).FirstOrDefaultAsync(ct);

// IDOR se id vem do cliente sem tenant/ownership
await db.Clients.IgnoreQueryFilters().Where(c => c.Id == id) // bypass — auditar

// Tenant write validation
// TenantWriteValidationInterceptor — confirmar escritas respeitam trainer efectivo
```

### Multi-tenant

```csharp
// CRITICAL — confiar em trainer do request
var trainerId = request.TrainerId; // body/query/route
await store.GetClients(trainerId);

// Correcto — ITenantContext
var trainerId = tenantContext.TrainerId; // fail-closed se obrigatório
```

---

## Auth — JWT e Identity

```csharp
[Authorize(Roles = "trainer")]
public async Task<IActionResult> CreateClient(...)

[AllowAnonymous] // só rotas públicas documentadas
public async Task<IActionResult> Login(...)
```

Sinais de problema:

- Endpoint mutável sem `[Authorize]` e sem auth alternativa (webhook assinado)
- `trainer_id` no DTO usado para filtrar dados
- Role definível pelo cliente no body
- Refresh token guardado em plaintext

---

## Stripe webhooks (.NET)

```csharp
// Raw body + assinatura antes de processar
var stripeEvent = EventUtility.ConstructEvent(json, signatureHeader, webhookSecret);
// ou gateway IPaymentWebhookAuthenticator

// HIGH — processar sem verificar assinatura
var payload = JsonSerializer.Deserialize<StripeEvent>(body);
```

Ver skill `payment-security` e `00_ARCHITECTURE.md` §10.

---

## Problem Details e logs

```csharp
// HIGH em produção — vazar internals
return Problem(detail: ex.ToString());

// OK
return Result.Failure(error).ToProblemDetails();
```

---

## React (19) + Vite 7

### XSS — reportar quando

```jsx
<div dangerouslySetInnerHTML={{ __html: userContent }} />

// NÃO reportar — CSS de config interna (shadcn chart)
<style dangerouslySetInnerHTML={{ __html: themeCssFromConfig }} />

// React escapa — NÃO é XSS
<p>{user.bio}</p>
```

### Sessão no cliente

```javascript
// HIGH — token acessível a XSS
localStorage.setItem('accessToken', token)
sessionStorage.setItem('jwt', token)

// OK — memória (AuthContext state)
setAccessToken(token) // não persistir
```

### Env Vite

```javascript
// CRITICAL — secret no bundle
const key = import.meta.env.VITE_STRIPE_SECRET_KEY

// OK — publishable
const pk = import.meta.env.VITE_STRIPE_PUBLISHABLE_KEY
```

---

## Axios / API client

- Bearer token de memória no interceptor
- Refresh via cookie HttpOnly — endpoint backend, não expor refresh ao JS
- Não enviar `trainer_id` no body esperando que backend ignore — auditar ambos os lados

---

## Validação

- Backend: FluentValidation nos handlers/commands
- Frontend: validação UX — **não** substitui validação server-side
- Contrato HTTP: snake_case, `/api/v1` — erros com `detail` preservado

---

## Headers (CSP / HSTS)

Não tratar ausência no repo como falha grave. Ver **`refs/security-headers.md`**.

---

## Integração com outras refs

| Tema | Ficheiro |
|------|----------|
| Stack / versões | `refs/project-stack.md` |
| Backend checklist | `refs/aspnet-checklist.md` |
| Frontend checklist | `refs/react-vite-checklist.md` |
| Pipeline auth | `refs/aspnet-middleware.md` |
| Falsos positivos | `refs/false-positives.md` |
| Segredos | `refs/secrets.md` |
| Relatório | `refs/report.md` |
