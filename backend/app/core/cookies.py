"""Helpers para cookies de sessão."""

from fastapi import Response
from app.core.config import settings

REFRESH_COOKIE_NAME = "refresh_token"
REFRESH_COOKIE_MAX_AGE = 30 * 24 * 60 * 60  # 30 dias


def set_refresh_token_cookie(response: Response, refresh_token: str) -> None:
    """Define refresh token como httpOnly cookie."""

    response.set_cookie(
        key=REFRESH_COOKIE_NAME,
        value=refresh_token,
        max_age=REFRESH_COOKIE_MAX_AGE,
        httponly=True,
        secure=settings.environment == "production",
        samesite="lax",
        path="/",
    )


def clear_refresh_token_cookie(response: Response) -> None:
    """Remove cookie de refresh token (logout / change-password)."""

    response.delete_cookie(
        key=REFRESH_COOKIE_NAME,
        httponly=True,
        secure=settings.environment == "production",
        samesite="lax",
        path="/",
    )
