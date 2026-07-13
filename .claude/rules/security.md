---
paths:
  - "backend/app/api/**"
  - "backend/app/services/**"
  - "backend/app/core/**"
  - "backend/app/middleware/**"
---

# Security — PT Manager

- Validate all user input at the API boundary (Pydantic schemas). Never trust request parameters.
- Use SQLModel/SQLAlchemy parameterized queries. Never concatenate user input into SQL.
- Multi-tenant: all data queries must filter by `trainer_id` from authenticated JWT.
- JWT and API Key required on protected routes. Extract `trainer_id` from token, not request body.
- Never log secrets, tokens, passwords, or PII.
- Stripe webhook: verify HMAC signature with `STRIPE_WEBHOOK_SECRET`.
- Rate-limit authentication and email endpoints (SlowAPI).
- Set appropriate CORS origins via environment variables.
