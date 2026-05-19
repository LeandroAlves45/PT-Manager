"""
Router de autenticação com rate limiting e segurança

Estrutura dos endpoints:
    POST /auth/login                    — login público (não requer token)
    POST /auth/logout                   — logout (requer token, invalida o token actual)
    POST /auth/refresh                  — renova o token sem pedir password
    POST /auth/users                    — criar utilizador (apenas trainers)
    GET  /auth/users                    — listar utilizadores (apenas trainers)
    GET  /auth/users/me                 — ver o próprio perfil (qualquer utilizador autenticado)
    PATCH /auth/users/{id}              — actualizar utilizador (trainer ou o próprio user)
    POST /auth/users/me/change-password — alterar própria password
"""

import logging

from fastapi import APIRouter, Depends, HTTPException, Query, Request, Response, status
from sqlmodel import Session, select
from sqlalchemy import func

from app.db.database import get_session
from app.core.security import get_current_user, require_trainer
from app.core.config import settings
from app.core.rate_limit import limiter, limiter_email_ip, RateLimitConfig
from app.api.schemas.auth import (
    ChangePassword,
    LoginIn,
    TokenOut,
    UserCreate,
    UserRead,
    UserUpdate,
)
from app.api.schemas.pagination_schema import PaginatedUserRead
from app.db.models import User, Client
from app.services.authentication_service import AuthenticationService
from app.services.user_management_service import UserManagementService
from app.repositories.user_repository import UserRepository

logger = logging.getLogger(__name__)
router = APIRouter(prefix="/auth", tags=["Authentication"])

# ============================================================
# LOGIN — público, sem JWT com Rate Limiting
# ============================================================


@router.post("/login", response_model=TokenOut)
@limiter_email_ip.limit(RateLimitConfig.LOGIN)
async def login(
    request: Request,
    response: Response,
    payload: LoginIn,
    session: Session = Depends(get_session),
) -> TokenOut:
    """
    Autentica user e devolve JWT + refresh token (httpOnly cookie).

    Rate limited: 5 tentativas por minuto (P1.2 — brute-force protection)
    """

    try:
        result = AuthenticationService.login(
            email=payload.email,
            password=payload.password,
            session=session,
            device_hint=request.headers.get("User-Agent", "") if request else None,
        )

        # Setar refresh token como httpOnly cookie
        response.set_cookie(
            key="refresh_token",
            value=result["refresh_token"],
            max_age=30 * 24 * 60 * 60,  # 30 dias
            httponly=True,
            secure=settings.environment == "production",
            samesite="lax",
        )

        return TokenOut(
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
            detail="Erro ao fazer login.",
        ) from e


# ============================================================
# REFRESH - renovação silenciosa do token com Rate Limiting
# ============================================================


@router.post("/refresh", response_model=TokenOut)
@limiter.limit(RateLimitConfig.REFRESH)
async def refresh_token(
    request: Request,
    response: Response,
    session: Session = Depends(get_session),
) -> TokenOut:
    """Renova JWT usando refresh token do cookie httpOnly."""

    try:
        refresh_token_string = request.cookies.get("refresh_token") if request else None
        if not refresh_token_string:
            raise HTTPException(
                status_code=status.HTTP_401_UNAUTHORIZED,
                detail="Refresh token em falta. Por favor, faça login novamente.",
            )

        device_hint = request.headers.get("User-Agent", "") if request else None
        result = AuthenticationService.refresh_token(
            refresh_token_string,
            session,
            device_hint=device_hint,
        )

        # Setar novo refresh token (rotation)
        response.set_cookie(
            key="refresh_token",
            value=result["refresh_token"],
            max_age=30 * 24 * 60 * 60,  # 30 dias
            httponly=True,
            secure=settings.environment == "production",
            samesite="lax",
        )

        return TokenOut(
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
            detail="Erro ao renovar token.",
        ) from e


# ============================================================
# LOGOUT com Rate Limiting
# ============================================================


@router.post("/logout", status_code=status.HTTP_200_OK)
@limiter.limit(RateLimitConfig.LOGOUT)
async def logout(
    response: Response,
    session: Session = Depends(get_session),
    current_user=Depends(get_current_user),
) -> dict:
    """Logout — invalida access token + refresh tokens."""

    try:
        AuthenticationService.logout(current_user.id, session)

        # Limpar cookie
        response.delete_cookie(key="refresh_token", httponly=True)

        return {"detail": "Logout bem-sucedido"}

    except Exception as e:
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail="Erro ao fazer logout.",
        ) from e


# ============================================================
# CRIAR USER — Rate limited
# ============================================================


@router.post("/users", status_code=status.HTTP_201_CREATED, response_model=UserRead)
@limiter.limit(RateLimitConfig.CREATE_USER)
async def create_user(
    payload: UserCreate,
    session: Session = Depends(get_session),
    current_trainer=Depends(require_trainer),  # pylint: disable=unused-argument
) -> UserRead:
    """Cria novo user (apenas Personal Trainers)."""

    try:
        user_data = UserManagementService.create_user(
            email=str(payload.email),
            password=payload.password,
            full_name=payload.full_name,
            role=payload.role,
            client_id=payload.client_id if payload.role == "client" else None,
            session=session,
        )
        user = UserRepository.get_by_id(user_data["id"], session)
        return UserRead.model_validate(user)
    except HTTPException:
        raise
    except Exception as e:
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail="Erro ao criar utilizador.",
        ) from e


# ============================================================
# LISTAR USERS — Rate limited
# ============================================================


@router.get("/users", response_model=PaginatedUserRead)
@limiter.limit(RateLimitConfig.LIST_USERS)
async def list_users(
    session: Session = Depends(get_session),
    current_trainer=Depends(require_trainer),  # pylint: disable=unused-argument
    skip: int = Query(0, ge=0),
    limit: int = Query(10, ge=1, le=100),
) -> PaginatedUserRead:
    """Lista users (tenant isolation)."""

    try:
        users = UserRepository.list_users_by_trainer(
            trainer_id=current_trainer.id,
            session=session,
            is_superuser=current_trainer.role == "superuser",
            skip=skip,
            limit=limit,
        )

        # Contar total de users (mesmo filtro que list)
        if current_trainer.role == "superuser":
            total_query = session.exec(
                select(func.count(User.id))  # pylint: disable=not-callable
            ).one()
        else:
            total_query = session.exec(
                select(func.count(User.id))  # pylint: disable=not-callable
                .join(Client, User.client_id == Client.id)
                .where(Client.owner_trainer_id == current_trainer.id)
            ).one()

        return PaginatedUserRead(
            users=[UserRead.model_validate(user) for user in users],
            total=total_query,
            skip=skip,
            limit=limit,
        )
    except Exception as e:
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail="Erro ao listar utilizadores.",
        ) from e


# ============================================================
# ATUALIZAR USER — Tenant isolation com Rate Limiting
# ============================================================


@router.patch("/users/{user_id}", response_model=UserRead)
@limiter.limit(RateLimitConfig.UPDATE_USER)
async def update_user(
    user_id: str,
    payload: UserUpdate,
    session: Session = Depends(get_session),
    current_user=Depends(get_current_user),
) -> UserRead:
    """Atualiza user com tenant isolation."""

    try:
        data = payload.model_dump(exclude_unset=True)
        UserManagementService.update_user(
            user_id=user_id,
            data=data,
            current_user=current_user,
            session=session,
        )
        user = UserRepository.get_by_id(user_id, session)
        return UserRead.model_validate(user)
    except HTTPException:
        raise
    except Exception as e:
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail="Erro ao atualizar utilizador.",
        ) from e


# ============================================================
# CHANGE PASSWORD — Session invalidation com Rate Limiting
# ============================================================


@router.post("/users/me/change-password", status_code=status.HTTP_200_OK)
@limiter.limit(RateLimitConfig.CHANGE_PASSWORD)
async def change_password(
    response: Response,
    payload: ChangePassword,
    session: Session = Depends(get_session),
    current_user=Depends(get_current_user),
) -> dict:
    """
    Altera password -> invalida TODOS os tokens.

    Segurança: Após change-password, user é forçado a fazer login novamente
    em todos os devices.
    """

    try:
        UserManagementService.change_password(
            user_id=current_user.id,
            current_password=payload.current_password,
            new_password=payload.new_password,
            session=session,
        )

        # Limpar cookie
        response.delete_cookie(key="refresh_token", httponly=True)

        return {
            "detail": "Password alterada com sucesso. Por favor, faça login novamente."
        }
    except HTTPException:
        raise
    except Exception as e:
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail="Erro ao alterar password.",
        ) from e


# ============================================================
# PERFIL DO UTILIZADOR AUTENTICADO
# ============================================================


@router.get("/users/me", response_model=UserRead)
async def get_my_profile(
    current_user=Depends(get_current_user),
) -> UserRead:
    """Devolve os dados do próprio utilizador (sem password)"""
    return UserRead.model_validate(current_user)
