"""
Módulo de segurança — autenticação, autorização e guards de acesso.

Camadas de segurança por ordem de execução em cada pedido:
    1. require_api_key          — valida o header X-API-Key (rotas que o incluem em main.py)
    2. get_current_user         — valida o JWT Bearer e devolve o utilizador da DB
    3. require_trainer /        — verifica o role do utilizador (Personal Trainer)
       require_superuser /
       require_client
    4. require_active_subscription — verifica se o Personal Trainer tem subscrição activa

Dependências disponíveis:
    get_current_user            — qualquer utilizador autenticado
    require_superuser           — apenas superusers
    require_trainer             — Personal Trainers e superusers (sem verificação de subscrição)
    require_active_subscription — Personal Trainers com subscrição activa (ou isentos de billing)
    require_client              — apenas clientes
    require_api_key             — validação da API key (onde configurado em main.py)
"""

from __future__ import annotations

import hashlib
import logging
import secrets
from datetime import datetime, timedelta, timezone
from typing import Optional

import jwt
from fastapi import Depends, Header, HTTPException, status
from fastapi.security import HTTPAuthorizationCredentials, HTTPBearer
from jwt.exceptions import PyJWTError as JWTError
from passlib.context import CryptContext
from sqlmodel import Session

from app.core.config import settings
from app.db.database import get_session
from app.db.models.user import User
from app.repositories.active_token_repository import ActiveTokenRepository
from app.services.subscription_service import SubscriptionService

logger = logging.getLogger(__name__)

# --------------------------------------------
# Hash de passwords - bcrypt via passlib
# --------------------------------------------

pwd_context = CryptContext(schemes=["bcrypt"], deprecated="auto")

bearer_scheme = HTTPBearer()
ALGORITHM = "HS256"


def hash_password(plain_password: str) -> str:
    """Converte a password em hash usando bcrypt."""
    return pwd_context.hash(plain_password)


def verify_password(plain_password: str, hashed_password: str) -> bool:
    """Verifica se a password em texto plano corresponde ao hash armazenado."""
    return pwd_context.verify(plain_password, hashed_password)


def create_access_token(
    subject: str,
    role: str,
    full_name: str,
    client_id: Optional[str] = None,
    expires_delta: Optional[timedelta] = None,
) -> str:
    """Cria um token JWT com as claims necessárias para autenticação e controlo de acesso.

    Claims incluídas no payload:
        sub       — user ID (subject padrão JWT)
        role      — "superuser" | "trainer" | "client"
        full_name — nome completo para exibição no frontend sem pedido extra à API
        exp       — timestamp de expiração
        iat       — timestamp de emissão
        cid       — client_id (apenas para role="client", para o portal do cliente)
    """
    expire = datetime.now(timezone.utc) + (
        expires_delta or timedelta(minutes=settings.access_token_expire_minutes)
    )
    iat = datetime.now(timezone.utc)
    payload = {
        "sub": subject,
        "role": role,
        "full_name": full_name,
        "iat": iat,
        "exp": expire,
    }

    if client_id is not None:
        payload["cid"] = client_id

    return jwt.encode(payload, settings.secret_key, algorithm=ALGORITHM)


async def get_current_user(
    credentials: HTTPAuthorizationCredentials = Depends(bearer_scheme),
    session: Session = Depends(get_session),
) -> User:
    """Valida o JWT Bearer e devolve o utilizador autenticado da base de dados."""
    token = credentials.credentials
    credentials_exception = HTTPException(
        status_code=status.HTTP_401_UNAUTHORIZED,
        detail="Token de autenticação inválido ou expirado.",
        headers={"WWW-Authenticate": "Bearer"},
    )

    try:
        payload = jwt.decode(token, settings.secret_key, algorithms=[ALGORITHM])
        user_id: str | None = payload.get("sub")
        if user_id is None:
            logger.warning("[AUTH] Token sem 'sub' claim")
            raise credentials_exception
    except JWTError as exc:
        logger.warning("[AUTH] Token JWT inválido ou expirado")
        raise credentials_exception from exc

    token_hash = hashlib.sha256(token.encode()).hexdigest()
    active_token = ActiveTokenRepository.get_by_user_id_and_token_hash(
        user_id, token_hash, session
    )
    if not active_token:
        logger.warning(
            "[AUTH] Token não encontrado em active_tokens para user_id=%s", user_id
        )
        raise credentials_exception

    user = session.get(User, user_id)
    if user is None or not user.is_active:
        logger.warning("[AUTH] User não encontrado ou inactivo: user_id=%s", user_id)
        raise credentials_exception

    return user


async def require_superuser(
    current_user: User = Depends(get_current_user),
) -> User:
    """Garante que o utilizador autenticado é um superuser."""
    if current_user.role != "superuser":
        logger.warning(
            "[AUTHZ] Acesso superuser negado para role=%s, user_id=%s",
            current_user.role,
            current_user.id,
        )
        raise HTTPException(
            status_code=status.HTTP_403_FORBIDDEN,
            detail="Acesso negado: requer privilégios de superuser.",
        )
    return current_user


async def require_trainer(
    current_user: User = Depends(get_current_user),
) -> User:
    """Garante Personal Trainer ou superuser (sem verificar subscricao)."""
    if current_user.role not in {"trainer", "superuser"}:
        logger.warning(
            "[AUTHZ] Acesso trainer negado para role=%s, user_id=%s",
            current_user.role,
            current_user.id,
        )
        raise HTTPException(
            status_code=status.HTTP_403_FORBIDDEN,
            detail="Acesso restrito a Personal Trainers.",
        )
    return current_user


async def require_active_subscription(
    current_user: User = Depends(require_trainer),
    session: Session = Depends(get_session),
) -> User:
    """Personal Trainer com subscrição activa, superuser, ou isento de billing."""
    if current_user.role == "superuser":
        return current_user

    if getattr(current_user, "is_exempt_from_billing", False):
        return current_user

    subscription = SubscriptionService.get_subscription(session, current_user.id)
    if not SubscriptionService.has_active_access(subscription):
        logger.warning(
            "[BILLING] Subscrição inactiva para trainer user_id=%s, status=%s",
            current_user.id,
            subscription.status if subscription else "None",
        )
        raise HTTPException(
            status_code=status.HTTP_402_PAYMENT_REQUIRED,
            detail=(
                "Acesso negado: requer subscrição activa. "
                "Por favor, aceda à página de billing para renovar ou actualizar a subscrição."
            ),
        )
    return current_user


async def require_client(
    current_user: User = Depends(get_current_user),
) -> User:
    """Garante que o utilizador autenticado é um cliente."""
    if current_user.role != "client":
        logger.warning(
            "[AUTHZ] Acesso client negado para role=%s, user_id=%s",
            current_user.role,
            current_user.id,
        )
        raise HTTPException(
            status_code=status.HTTP_403_FORBIDDEN,
            detail="Acesso restrito a clientes.",
        )
    return current_user


def require_api_key(
    x_api_key: str | None = Header(default=None, alias="X-API-Key")
) -> None:
    """Valida a API Key enviada no header X-API-Key."""
    if not settings.api_key:
        logger.error("[API_KEY] API key não configurada no servidor")
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail="API key não configurada no servidor.",
        )

    if not secrets.compare_digest(x_api_key or "", settings.api_key):
        logger.warning("[API_KEY] Tentativa com API key inválida ou ausente")
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="API key inválida.",
        )
