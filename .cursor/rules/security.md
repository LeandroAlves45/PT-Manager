---
paths:
  - "backend/src/Api/**"
  - "backend/src/Application/**"
  - "backend/src/Infrastructure/**"
---

# Security — PT Manager

- Validar todo o input do utilizador na fronteira da API. Nunca confiar em parâmetros de request.
- Usar EF Core com queries parametrizadas. Nunca concatenar input do utilizador em SQL.
- Multi-tenant: todas as queries filtram por `trainer_id` obtido do JWT autenticado via `ITenantContext`, nunca do request body, query string ou route.
- Usar ASP.NET Core Identity. Access tokens JWT curtos; refresh tokens opacos, rotativos, guardados apenas como hash.
- Access token em memória no frontend; refresh token em cookie `HttpOnly`, `Secure`, `SameSite` adequado.
- `ITenantContext` deve falhar de forma fechada (fail closed) quando o tenant for obrigatório.
- Nunca registar em log segredos, tokens, passwords ou PII.
- Webhook Stripe: verificar assinatura HMAC com o segredo do webhook, exigir raw body, deduplicar por `event.id`, idempotência e outbox transacional.
- Rate-limit em endpoints de autenticação e email.
- Definir origens CORS apropriadas via variáveis de ambiente.
- Operações administrativas com bypass de tenant são explícitas, restritas e auditadas.
