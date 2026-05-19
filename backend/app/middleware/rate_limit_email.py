"""
Middleware para adicionar email ao rate limiting key.
Extrai o email do body JSON para endpoints de login.
"""

import json
from fastapi import Request
from starlette.middleware.base import BaseHTTPMiddleware


class RateLimitEmailMiddleware(BaseHTTPMiddleware):
    """Middleware que extrai email do body JSON para rate limiting."""

    async def dispatch(self, request: Request, call_next):
        """Intercepta POST /auth/login para extrair email e armazenar em request.scope."""

        if request.method == "POST" and "/auth/login" in request.url.path:
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
