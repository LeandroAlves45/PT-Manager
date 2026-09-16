# Contrato HTTP para o Frontend

*2026-09-15 — verificado contra `backend/src/Api` (controllers, `Configuration/`,
`Http/`, `Middlewares/`, `Contracts/`). Em caso de divergência, o código vence.*

## 1. Base

| Item | Valor | Fonte |
|---|---|---|
| Prefixo | `/api/v1` | controllers `[Route]` |
| Formato | JSON, **snake_case** em propriedades, chaves de dicionário e enums | `DependencyInjection.cs` (`JsonNamingPolicy.SnakeCaseLower`) |
| Query strings | snake_case (`page_number`, `client_id`, `from_date`) | `PageParameters.cs`, `api-surface.v1.txt` |
| IDs | UUID em path (`{clientId:guid}`) | controllers |
| OpenAPI | `/openapi/v1.json` + Scalar, **só em Development** | `Program.cs` (`MapOpenApi`, `MapScalarApiReference`) |
| Correlation | Header `X-Correlation-ID` aceite e devolvido; `correlation_id` nos erros | `CorrelationIdMiddleware.cs`, `ApiResultMapper.cs` |

## 2. CORS e origem

Configurado em `ApiCorsPolicy.cs` / `ApiCorsOptions.cs`:

- Origens: `Cors:AllowedOrigins` — **só HTTPS**, sem path, query ou wildcard, sem duplicados
  (validado no arranque; `appsettings.json` traz lista vazia).
- Métodos: `GET, POST, PUT, PATCH, DELETE`.
- Headers de pedido permitidos: `Authorization`, `Content-Type`, `X-CSRF-Token`,
  `X-Correlation-ID`.
- Headers expostos: `X-Correlation-ID`, `Retry-After`, `Location`.
- `AllowCredentials()` ativo → o frontend usa `credentials: 'include'`.
- `AuthController` e `GoogleAuthController` têm `[RequireOrigin]`: pedido sem `Origin`,
  com vários ou com origem não autorizada recebe **403**.

Implicação para desenvolvimento: o Vite corre em HTTPS e a sua origem é adicionada a
`Cors:AllowedOrigins` nos User Secrets de Development.

## 3. Autenticação

### Tokens

| Token | Onde vive | Duração | Uso |
|---|---|---|---|
| Access token (JWT) | Memória do SPA | 15 min | `Authorization: Bearer <token>` |
| Refresh token | Cookie `__Secure-ptm-refresh`, `HttpOnly`, `Secure`, `Path=/api/v1/auth`, `SameSite` configurável (default `Lax`) | 30 dias (`RefreshSessionLifetime`), rodado a cada refresh com deteção de reutilização | Enviado automaticamente só para `/api/v1/auth/*` |
| CSRF token | Memória do SPA | Ligado à sessão | Header `X-CSRF-Token` em `refresh` e `logout` |

Claims relevantes do JWT: `sub`, `role` (`superuser` · `trainer` · `client`), `trainer_id`
(`ApiClaimNames.cs`).

### Resposta de sessão (`login`, `refresh`, `google/sign-in`)

```json
{
  "user_id": "uuid",
  "trainer_id": "uuid | null",
  "role": "trainer",
  "access_token": "jwt",
  "access_token_expires_at": "2026-09-15T10:15:00Z",
  "csrf_token": "opaque"
}
```

(`SessionResponse` em `Contracts/Authentication/AuthenticationContracts.cs`.)

### Fluxos

| Fluxo | Pedido | Resposta |
|---|---|---|
| Login | `POST /auth/login` `{ email, password }` | 200 sessão + cookie |
| Signup trainer | `POST /auth/signup` `{ email, password, full_name }` | 201 `{ user_id, trainer_id, email, trial_ends_at }` (sem sessão; confirma email) |
| Restaurar sessão no arranque | `POST /auth/csrf` (cookie) → `POST /auth/refresh` com `X-CSRF-Token` | 200 `{ csrf_token }` → 200 sessão |
| Refresh | `POST /auth/refresh` com `X-CSRF-Token` | 200 sessão + novo cookie; 401 se inválida |
| Logout | `POST /auth/logout` com `X-CSRF-Token` | 204; cookie apagado só depois da revogação |
| Confirmar email | `POST /auth/confirm-email` `{ token }` | — |
| Reenviar confirmação | `POST /auth/resend-confirmation` (autenticado) | — |
| Recuperar password | `POST /auth/password-reset/request` `{ email }` → `/complete` `{ token, new_password, confirm_new_password }` | — |
| Mudar password | `POST /auth/change-password` `{ current_password, new_password, confirm_new_password }` | — |
| Convidar cliente | `POST /auth/invite-client` (trainer) `{ client_id, email }` | — |
| Aceitar convite | `POST /auth/accept-invite` (client) `{ token, transfer_approved }` | — |
| Google | `POST /auth/google/challenge` → `POST /auth/google/sign-in`; linking em `/auth/google/link/challenge` → `/auth/google/link` | sessão / ligação |

Política de password: 8 a 128 caracteres (Identity é a autoridade).

Riscos de integração a tratar na Fase 6A (ver `../frontend/00_ARQUITETURA_FRONTEND.md` §5):
refresh concorrente entre separadores e cookies cross-site em produção.

## 4. Erros — ProblemDetails

`Content-Type: application/problem+json`. Mapeamento de `ErrorCategory`
(`Http/ApiResultMapper.cs`):

| Categoria | HTTP |
|---|---|
| Validation | 400 (+ `errors[]` com `field`, `code`, `message`) |
| Unauthorized | 401 (+ `WWW-Authenticate: Bearer`) |
| PaymentRequired | 402 |
| Forbidden | 403 |
| NotFound | 404 |
| Conflict | 409 |
| Rate limit | 429 (+ `Retry-After`) |
| ExternalDependency | 503 |
| Outros | 500 |

Forma:

```json
{
  "status": 409,
  "title": "codigo_estavel_do_erro",
  "detail": "Descrição legível",
  "instance": "/api/v1/…",
  "correlation_id": "…"
}
```

`title` é o código estável (`Error.Code`) e é a chave para mensagens PT-PT no frontend.
Os códigos vivem em `backend/src/Application/Features/*/*Errors.cs`.

## 5. Paginação

Pedido: `page_number` (base 1, default 1) e `page_size` (default 50); valores ≤ 0 caem nos
defaults. Filtros comuns: `search`, `activity` (ativos/arquivados), `client_id`, intervalos
de datas.

Resposta (`PagedResponse<T>`):

```json
{ "items": [], "total_count": 0, "page_number": 1, "page_size": 50 }
```

## 6. Rate limiting (`Configuration/ApiRateLimiting.cs`)

Janela fixa; excedido → 429 com `Retry-After`.

| Policy | Limite | Chave |
|---|---|---|
| Global (autenticado) | 300 / min | utilizador |
| Global (anónimo) | 60 / min | IP |
| `auth_login` | 10 / min | IP |
| `auth_signup` | 3 / hora | IP |
| `auth_refresh` | 30 / 5 min | IP |
| `auth_logout` | 10 / min | IP |
| `auth_csrf_bootstrap` | 10 / min | IP |
| `auth_password_reset_request` | 3 / hora | IP |
| `auth_password_reset_complete` | 5 / 15 min | IP |
| `auth_email_confirmation` | 10 / 15 min | IP |
| `auth_email_confirmation_resend` | 3 / hora | IP |
| `auth_invite_client` | 10 / hora | utilizador |
| `auth_google_sign_in` | 10 / min | IP |
| `auth_google_link` | 5 / 15 min | utilizador + IP |
| `auth_change_password` | 5 / 15 min | utilizador + IP |
| `admin_moderation` | 30 / min | utilizador |
| `media_upload` | 10 / hora | utilizador |

Implicação: o arranque (`csrf` + `refresh`) conta contra limites por IP; evitar refresh em
loop e reloads automáticos.

## 7. Uploads de imagem

`PUT /trainer-settings/logo` e `PUT /portal/my-profile/avatar`:
`multipart/form-data`, pedido máximo **6 MiB**, até 4 campos de formulário
(`Http/FormFileMediaUpload.cs`), policy `media_upload`. O avatar passa por moderação
síncrona (resultado `Approved` publica; `ReviewRequired`/`Unavailable` preservam o anterior).
`logo_url`/avatar `null` → o frontend mostra o asset padrão.

Vídeo de exercício (Fase 5D, R2): upload direto para URL pré-assinada, fora deste
contrato até o Gate 5D fechar.

## 8. Feature flags do backend que afetam a UI

| Secção | Default | Efeito se `Enabled=false` |
|---|---|---|
| `Stripe` | `false` | Checkout/portal indisponíveis → UI de billing mostra estado "não configurado" |
| `Cloudinary` / `Vision` | `false` | Logo/avatar falham com erro explícito (503) |
| `QStash` | `false` | Jobs/emails não despachados automaticamente em dev |
| `R2` (5D) | `false` | Upload de vídeo indisponível |

## 9. Headers de resposta sensíveis

`AuthController` usa `[SensitiveResponse]` (sem cache HTTP). `SecurityHeadersMiddleware`
aplica CSP e headers de segurança; o frontend não depende de headers para além de
`X-Correlation-ID`, `Retry-After` e `Location`.
