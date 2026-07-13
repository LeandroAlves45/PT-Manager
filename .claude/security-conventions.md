# PT Manager — Convenções de Segurança

## Secrets e Environment Variables

Todas as variáveis sensíveis vivem em `.env` (nunca commitado). Definidas em `backend/app/core/config.py`:

| Variável | Uso |
|----------|-----|
| `DATABASE_URL` | Ligação PostgreSQL/SQLite |
| `API_KEY` | Middleware API Key |
| `SECRET_KEY` | Assinatura JWT |
| `STRIPE_SECRET_KEY` | API Stripe |
| `STRIPE_WEBHOOK_SECRET` | Verificação HMAC webhooks |
| `RESEND_API_KEY` | Envio de emails |
| `CLOUDINARY_*` | Upload de imagens |
| `SENTRY_DSN` | Error monitoring |

**Regra:** nunca hardcode, nunca logar, nunca expor ao frontend.

## Autenticação

### API Key Middleware

- Routers protegidos exigem header `X-API-Key`
- Valor configurado em `API_KEY` env var
- Excepções: `health`, `stripe_webhook`

### JWT

- Access token com expiração (`ACCESS_TOKEN_EXPIRE_MINUTES`, default 60)
- Refresh tokens com grace period (`REFRESH_GRACE_HOURS`, default 4)
- `ActiveToken` regista tokens activos (hash, não plaintext)
- Extrair `trainer_id` de `owner_trainer_id` no payload — **nunca** confiar em IDs do request body

### Roles

| Role | Acesso |
|------|--------|
| `superuser` | Admin global |
| `trainer` | Dados do seu tenant (`trainer_id`) |
| `client` | Portal próprio apenas |

## Multi-Tenant

- **Todas** as queries de dados filtram por `trainer_id`
- Validar ownership antes de update/delete de recursos
- Client não acede a dados de outros clients

## Input Validation

- Pydantic schemas na boundary da API (`api/schemas/`)
- `email-validator` para emails
- Rate limiting via SlowAPI (especialmente endpoints de auth/email)

## Stripe

- Webhook: verificar assinatura com `STRIPE_WEBHOOK_SECRET`
- Idempotência via `ProcessedStripeEvent`
- Live keys **nunca** em código

## HTTP Security

- CORS: origens permitidas via `CORS_ORIGINS` env var
- HTTPS forçado em produção (Render/Vercel)
- Sentry integrado (sem PII nos breadcrumbs)

## Frontend

- Tokens em memória/localStorage via `AuthContext` — não expor secrets de backend
- Axios interceptor para JWT + API Key
- `ProtectedRoute` valida role antes de renderizar pages

## O que os Hooks Protegem

- `.env` e secrets: bloqueio de leitura/escrita
- `backend/app/db/migrations/`: bloqueio de edição
- Scan de secrets em ficheiros editados (AWS keys, `sk-*`, private keys)
