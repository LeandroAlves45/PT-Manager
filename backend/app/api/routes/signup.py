"""Endpoints públicos de signup."""

import logging
import hashlib
import secrets
from datetime import timedelta

from fastapi import APIRouter, Depends, HTTPException, Request, status
from sqlmodel import Session

from app.core.config import settings
from app.api.schemas.signup import (
    TrainerSignupIn,
    TrainerSignupOut,
    ClientSignupIn,
    ClientSignupOut,
    EmailVerificationIn,
    EmailVerificationOut,
)
from app.core.rate_limit import limiter, RateLimitConfig
from app.db.database import get_session
from app.services.signup_service import SignupService
from app.repositories.user_repository import UserRepository
from app.services.email_service import EmailService
from app.utils.db_errors import commit_or_rollback
from app.utils.time import utc_now_datetime

logger = logging.getLogger(__name__)

router = APIRouter(prefix="/auth", tags=["Auth - Signup"])
signup_service = SignupService()


@router.post("/signup/trainer",
response_model=TrainerSignupOut,
status_code=status.HTTP_201_CREATED
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
async def verify_email(
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
    """
    return signup_service.verify_email_token(token=payload.token, session=session)


@router.post("/resend-verification-email")
@limiter.limit("5/hour")
async def resend_verification_email(
    request: Request,  # pylint: disable=unused-argument
    email: str,
    session: Session = Depends(get_session),
):
    """Reenvía email de verificacao para trainer (fallback).

    Fluxo:
    1. Busca User com esse email (role="trainer", email_verified=False)
    2. Valida que nao expirou demasiado tempo desde o signup (48h max)
    3. Gera novo verification token
    4. Envia email
    5. Retorna {message: "Email sent"}

    Rate limit: 5 emails/hora por IP
    """


    user = UserRepository.get_by_email(email, session)

    if not user or user.role != "trainer" or user.email_verified:
        raise HTTPException(
            status_code=400,
            detail="Verification email not needed for this account.",
        )

    # Validar que signup nao foi ha muito tempo
    signup_age = utc_now_datetime() - user.created_at
    if signup_age > timedelta(hours=48):
        raise HTTPException(
            status_code=400,
            detail="Verification link expired. Please signup again.",
        )

    # Gerar novo token
    token_raw = secrets.token_urlsafe(24)
    token_hash = hashlib.sha256(token_raw.encode()).hexdigest()

    user.invite_token_hash = token_hash
    user.invite_token_expires_at = utc_now_datetime() + timedelta(minutes=15)
    session.add(user)
    commit_or_rollback(session)

    # Enviar email
    email_service = EmailService()
    verification_link = f"{settings.frontend_url}/verify-email?token={token_raw}"
    email_service.send_verification_email(
        trainer_email=user.email,
        trainer_name=user.full_name,
        verification_link=verification_link,
    )

    logger.info("Verification email resent to %s***", email[:3])

    return {"message": "Verification email sent. Check your inbox."}


@router.post("/signup/client", response_model=ClientSignupOut, status_code=status.HTTP_201_CREATED)
@limiter.limit(RateLimitConfig.SIGNUP_CLIENT)
async def client_signup(
    request: Request,  # pylint: disable=unused-argument
    payload: ClientSignupIn,
    session: Session = Depends(get_session),
):
    """
    Signup de novo Client via invite token de trainer.

    Fluxo:
    1. Valida invite_token (SHA256, 7 dias TTL)
    2. Busca trainer (owner) e valida subscrição ativa
    3. Cria User(role="client", email_verified=True direto)
    4. Cria Client(full_name, phone, owner_trainer_id)
    5. Invalida invite_token
    6. Retorna {id, email, full_name, client_id, message}

    Segurança:
    - Invite token gerado pelo trainer (prova de ownership)
    - Trainer com subscrição ativa
    - Rate limit: 10 signups/hora por IP (mais restrictivo que trainer para evitar spam)
    - Email único na BD
    """
    return signup_service.client_signup(
        email=payload.email,
        password=payload.password,
        full_name=payload.full_name,
        phone=payload.phone,
        invite_token=payload.invite_token,
        session=session,
    )
