"""Endpoints públicos de signup."""

import logging

from fastapi import APIRouter, Depends, Request, Response, status
from sqlmodel import Session

from app.core.cookies import set_refresh_token_cookie
from app.api.schemas.signup import (
    TrainerSignupIn,
    TrainerSignupOut,
    EmailVerificationIn,
    EmailVerificationOut,
    ResendVerificationIn,
    ResendVerificationOut,
)
from app.core.rate_limit import limiter, RateLimitConfig
from app.db.database import get_session
from app.services.signup_service import SignupService

logger = logging.getLogger(__name__)

router = APIRouter(prefix="/auth", tags=["Auth - Signup"])
signup_service = SignupService()

@router.post(
    "/signup/trainer",
    response_model=TrainerSignupOut,
    status_code=status.HTTP_201_CREATED,
)
@limiter.limit(RateLimitConfig.SIGNUP)
async def trainer_signup(
    request: Request,  # pylint: disable=unused-argument
    payload: TrainerSignupIn,
    session: Session = Depends(get_session),
):
    """Signup de novo Personal Trainer.

    Fluxo:
    1. Valida email + password + full_name
    2. Cria User(role="trainer", email_verified=False)
    3. Cria TrainerSubscription(trial de 15 dias)
    4. Envia email de verificacao via Resend
    5. Retorna {id, email, full_name, message}

    O Personal Trainer só recebe access_token após verificar o email em /auth/verify-email.

    Segurança:
    - Rate limit: 3 registos/hora por IP
    - Email único na BD
    - Password hash com bcrypt
    - Token de verificacao com TTL de 15 min
    """
    return signup_service.trainer_signup(
        email=payload.email,
        password=payload.password,
        full_name=payload.full_name,
        session=session,
    )

@router.post("/verify-email", response_model=EmailVerificationOut)
@limiter.limit(RateLimitConfig.VERIFY_EMAIL)
async def verify_email(
    request: Request,  # pylint: disable=unused-argument
    response: Response,
    payload: EmailVerificationIn,
    session: Session = Depends(get_session),
):
    """Verifica o email do trainer e retorna access_token.

    Fluxo:
    1. Valida token (SHA256, TTL 15 min)
    2. Marca User.email_verified=True
    3. Invalida verification token
    4. Retorna {email, is_verified, message}

    Frontend: Após este endpoint, user consegue fazer login com /auth/login.

    Segurança:
    - Token comparison com constant-time para evitar timing attacks
    - Mensagens genericas para erros (não disclose se token existe ou expirou)
    - Rate limit: 10 tentativas/minuto por IP
    """
    result = signup_service.verify_email_token(token=payload.token, session=session)
    set_refresh_token_cookie(response, result["refresh_token"])
    return EmailVerificationOut(
        access_token=result["access_token"],
        email=result["email"],
        is_verified=result["is_verified"],
        message=result["message"],
    )

@router.post("/resend-verification-email", response_model=ResendVerificationOut)
@limiter.limit(RateLimitConfig.RESEND_VERIFICATION)
async def resend_verification_email(
    request: Request,  # pylint: disable=unused-argument
    payload: ResendVerificationIn,
    session: Session = Depends(get_session),
):
    """Reenvia email de verificação para trainer (fallback).

    Sempre devolve a mesma mensagem genérica, independentemente de o email existir
    ou já estar verificado, para evitar enumeração de contas.

    Rate limit: 5 pedidos/hora por IP+email
    """
    return signup_service.resend_verification_email(
        email=payload.email,
        session=session,
    )

