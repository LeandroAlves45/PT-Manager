---
paths:
  - "backend/app/api/**"
  - "backend/app/services/**"
---

# Error Handling — PT Manager

- Use `HTTPException` in routes with correct status codes (400 validation, 401 auth, 403 forbidden, 404 not found, 409 conflict, 500 unexpected).
- Never swallow errors silently. Log with context about what operation failed.
- Never expose stack traces, internal paths, or raw database errors in production responses.
- Sentry captures unexpected errors in production — do not duplicate with generic catch-all.
- Include correlation context in error logs when available.
- Services should raise domain-specific exceptions or return error results; routes map to HTTP responses.
