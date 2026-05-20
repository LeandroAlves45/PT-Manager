"""
Router do fluxo de convite de clientes.

Este router tem dois grupos de endpoints com níveis de acesso distintos:

  PROTEGIDOS (require_active_subscription):
    POST /clients/{client_id}/generate-invite
      - Personal Trainer gera o link de convite para um cliente

  PÚBLICOS (apenas API-Key, sem JWT):
    GET  /invite/validate/{token}
      - Valida o token e devolve o nome do cliente (para a página /invite/:token)
    POST /invite/set-password/{token}
      - Cliente define a sua password e recebe um JWT imediatamente

Segurança do token:
    1. Backend gera 32 bytes aleatorios via secrets.token_urlsafe(32)
    2. Armazena SHA-256(token) na BD - nunca o raw token
    3. O raw token viaja apenas no URL de convite (HTTPS)
    4. Na validação, o backend recalcula SHA-256(token_recebido) e compara com o hash guardado
    5. Após utilização, o hash é apagado da BD (one-time use)
    6. Tokens expiram ao fim de 7 dias
"""

import logging

from fastapi import APIRouter, Depends, HTTPException, Request, Response, status
from sqlmodel import Session

from app.db.database import get_session
from app.core.security import require_active_subscription
from app.core.config import settings
from app.core.cookies import set_refresh_token_cookie
from app.api.routes.auth import limiter, RateLimitConfig
from app.db.models import User
from app.services.invite_service import InviteService
from app.api.schemas.invite import (
    InviteGenerateResponse,
    InviteValidateResponse,
    InviteSetPassword,
    InviteLoginResponse,
)

logger = logging.getLogger(__name__)

router = APIRouter(tags=["Invite"])


# ============================================================
# ENDPOINT PROTEGIDO — apenas Personal Trainers com subscrição ativa
# ============================================================


@router.post(
    "/clients/{client_id}/generate-invite",
    response_model=InviteGenerateResponse,
    status_code=status.HTTP_201_CREATED,
)
@limiter.limit(RateLimitConfig.INVITE_GENERATE_LIMIT)
async def generate_invite(
    request: Request,  # pylint: disable=unused-argument
    client_id: str,
    session: Session = Depends(get_session),
    current_user: User = Depends(require_active_subscription),
) -> InviteGenerateResponse:
    """Gera link de convite para um cliente."""

    try:
        invite_link = InviteService.generate_invite(
            client_id=client_id,
            current_user=current_user,
            session=session,
        )

        return InviteGenerateResponse(
            invite_link=invite_link,
            expires_in_days=settings.invite_expiry_days,
        )

    except HTTPException:
        raise
    except Exception as e:
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail="Erro ao gerar convite.",
        ) from e


# ============================================================
# ENDPOINTS PUBLICOS — sem JWT, apenas API-Key
# ============================================================


@router.get("/invite/validate/{token}", response_model=InviteValidateResponse)
@limiter.limit(RateLimitConfig.INVITE_VALIDATE_LIMIT)
async def validate_invite_token(
    request: Request,  # pylint: disable=unused-argument
    token: str,
    session: Session = Depends(get_session),
) -> InviteValidateResponse:
    """
    Valida o token de convite.
    Usado na página /invite/:token para mostrar o nome do cliente.
    Endpoint público - não requer JWT.
    """

    try:
        result = InviteService.validate_invite_token(token, session)

        return InviteValidateResponse(
            valid=result["valid"],
            client_name=result["client_name"],
            message=result["message"],
        )

    except Exception as e:
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail="Erro ao validar token.",
        ) from e


@router.post("/invite/set-password/{token}", response_model=InviteLoginResponse)
@limiter.limit(RateLimitConfig.INVITE_SET_PASSWORD_LIMIT)
async def set_password_via_invite(
    request: Request,  # pylint: disable=unused-argument
    response: Response,
    token: str,
    payload: InviteSetPassword,
    session: Session = Depends(get_session),
) -> InviteLoginResponse:
    """
    Cliente define a sua password usando o token de convite.
    A password é definida usando o token de convite.
    Após definir a password, recebe um JWT para acesso imediato ao dashboard.
    Endpoint público - não requer JWT.
    """

    try:
        result = InviteService.set_password_via_invite(
            token=token,
            new_password=payload.new_password,
            session=session,
        )

        set_refresh_token_cookie(response, result["refresh_token"])

        return InviteLoginResponse(
            access_token=result["access_token"],
            role=result["role"],
            user_id=result["user_id"],
            full_name=result["full_name"],
        )

    except HTTPException:
        raise
    except Exception as e:
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail="Erro ao definir password.",
        ) from e
