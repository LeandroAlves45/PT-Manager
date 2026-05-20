"""
Middleware para adicionar email ao rate limiting key.
Extrai o email do body JSON para endpoints de login e reenvio de verificação.
"""

import json
from fastapi import Request
from starlette.middleware.base import BaseHTTPMiddleware

_RATE_LIMIT_EMAIL_PATHS = (
    "/auth/login",
    "/auth/resend-verification-email",
)


class RateLimitEmailMiddleware(BaseHTTPMiddleware):
    """Middleware que extrai email do body JSON para rate limiting."""

    async def dispatch(self, request: Request, call_next):
        """Intercepta POSTs de auth para extrair email e armazenar em request.scope."""

        if request.method == "POST" and any(
            path in request.url.path for path in _RATE_LIMIT_EMAIL_PATHS
        ):
            body = await request.body()
            if body:
                try:
                    data = json.loads(body.decode())
                except (json.JSONDecodeError, UnicodeDecodeError):
                    data = None
                if isinstance(data, dict):
                    email = data.get("email", "")
                    if email:
                        request.scope["email"] = email

        return await call_next(request)
