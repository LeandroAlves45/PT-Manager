"""Serviço de autenticação - login, logout, refresh com SEGURANÇA."""

import hashlib
import logging
import secrets
from datetime import timedelta, timezone
from fastapi import HTTPException, status
from sqlmodel import Session
from app.core.config import settings
from app.core.security import (
    create_access_token,
    verify_password,
)
from app.repositories.user_repository import UserRepository
from app.repositories.active_token_repository import ActiveTokenRepository
from app.repositories.refresh_token_repository import RefreshTokenRepository
from app.utils.time import utc_now_datetime

logger = logging.getLogger(__name__)


class AuthenticationService:
    """
    Orquestra login, logout e refresh com proteção contra brute-force, CSRF, session hijacking.
    """

    @staticmethod
    def login(
        email: str,
        password: str,
        session: Session,
        device_hint: str = None,
    ) -> dict:
        """
        Autentica user e cria tokens.

        Segurança:
        - Rate limiting (feito em routes via decorator)
        - Email/password erro genérico (evita user enumeration)
        - Access token em JWT (curto, 60 min)
        - Refresh token em httpOnly cookie (opaco, 30 dias, rotável)

        Fluxo:
        1. Busca user por email
        2. Valida password (constant-time via verify_password)
        3. Valida que conta está ativa
        4. Cria JWT (com iat claim)
        5. Calcula hash do JWT
        6. Persiste hash em active_tokens (não o JWT completo)
        7. Gera refresh token opaco
        8. Calcula hash do refresh token
        9. Persiste hash em refresh_tokens
        10. Retorna access_token + instrução de setar cookie

        Raises:
            HTTPException: 401 se email/password inválido ou conta inativa
        """

        # 1. Busca user por email
        user = UserRepository.get_by_email(email, session)
        if not user:
            # Não logar detalhes que revelem enumeração de emails
            logger.warning(
                "[AUTH] Login falhou: credenciais inválidas",
            )
            raise HTTPException(
                status_code=status.HTTP_401_UNAUTHORIZED,
                detail="Email ou password inválido",
            )

        # 2. Valida password (constant-time via verify_password)
        if not verify_password(password, user.hashed_password):
            # Mesma mensagem para evitar user enumeration via logs
            logger.warning(
                "[AUTH] Login falhou: credenciais inválidas",
            )
            raise HTTPException(
                status_code=status.HTTP_401_UNAUTHORIZED,
                detail="Email ou password inválido",
            )

        # 3. Valida que conta está ativa
        if not user.is_active:
            logger.warning(
                "[AUTH] Login falhou: conta inativa para o user_id: %s",
                user.id[:8],
            )
            raise HTTPException(
                status_code=status.HTTP_401_UNAUTHORIZED,
                detail="Conta inativa. Entra em contacto com o suporte.",
            )

        # 3b. Trainer deve ter email verificado
        if user.role == "trainer" and not user.email_verified:
            logger.warning(
                "[AUTH] Login falhou: email não verificado para user_id=%s",
                user.id[:8],
            )
            raise HTTPException(
                status_code=status.HTTP_403_FORBIDDEN,
                detail="Email não verificado. Verifique a sua caixa de entrada.",
            )

        # 4. Cria JWT (com iat claim)
        expire_delta = timedelta(minutes=settings.access_token_expire_minutes)
        jwt_token = create_access_token(
            subject=user.id,
            role=user.role,
            full_name=user.full_name,
            client_id=user.client_id if user.role == "client" else None,
            expires_delta=expire_delta,
        )

        # 5. Calcula hash do JWT
        jwt_hash = hashlib.sha256(jwt_token.encode()).hexdigest()
        expires_at = utc_now_datetime() + expire_delta

        # 6. Persiste hash em active_tokens (não o JWT completo)
        try:
            ActiveTokenRepository.save_or_replace(
                user_id=user.id,
                token_hash=jwt_hash,
                expires_at=expires_at,
                session=session,
            )

            # 7,8. Gera refresh token opaco
            refresh_token_string = secrets.token_urlsafe(32)
            refresh_token_hash = hashlib.sha256(
                refresh_token_string.encode()
            ).hexdigest()

            # 9. Persistir refresh token (com hash)
            refresh_expires = utc_now_datetime() + timedelta(days=30)
            RefreshTokenRepository.save(
                user_id=user.id,
                token_hash=refresh_token_hash,
                expires_at=refresh_expires,
                device_hint=device_hint,
                session=session,
            )
        except Exception as e:
            # Se qualquer persistência falhar, não retornar tokens
            logger.error(
                "[AUTH] Falha ao persistir tokens para user_id=%s: %s",
                user.id[:8],
                str(e),
            )
            raise HTTPException(
                status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
                detail="Erro ao criar sessão. Por favor, tenta novamente.",
            ) from e

        logger.info(
            "[AUTH] Login bem-sucedido: user_id=%s, role=%s",
            user.id[:8],
            user.role,
        )

        return {
            "access_token": jwt_token,
            "refresh_token": refresh_token_string,
            "role": user.role,
            "user_id": user.id,
            "full_name": user.full_name,
        }

    @staticmethod
    def logout(user_id: str, session: Session) -> None:
        """
        Logout -> revoga access token + todos os refresh tokens.

        Segurança: Revoga TODOS os refresh tokens (logout em todos os devices).
        """

        # Revoga access token
        ActiveTokenRepository.revoke_by_user_id(user_id, session)

        # Revoga todos os refresh tokens
        RefreshTokenRepository.revoke_all_by_user_id(user_id, session)

        logger.info(
            "[AUTH] Logout bem-sucedido: user_id=%s",
            user_id[:8],
        )

    @staticmethod
    def refresh_token(
        refresh_token_string: str,
        session: Session,
        device_hint: str = None,
    ) -> dict:
        """
        Renova JWT sem pedir password.

        Segurança:
        - Refresh token não é JWT (é opaco, gerado via secrets.token_urlsafe)
        - Comparação constant-time via hash SHA-256
        - Grace period: permite refresh até 24h após expiração
        - Rotation: cada refresh emite novo refresh token, revoga anterior (anti-replay)

        Fluxo:
        1. Calcular hash do refresh token recebido
        2. Validar que hash existe em BD (se fez logout, não existe)
        3. Validar que não foi revogado
        4. Validar que não expirou
        5. Carregar user e validar que está ativo
        6. Emitir novo JWT access token
        7. Atualizar hash em active_tokens
        8. ROTATION: Revogar refresh token antigo, emitir novo
        9. Retornar novo access_token + novo refresh_token

        Raises:
            HTTPException: 401 se refresh token inválido, revogado ou fora da janela de grace
        """

        if not refresh_token_string:
            raise HTTPException(
                status_code=status.HTTP_401_UNAUTHORIZED,
                detail="Refresh token em falta. Por favor, faça login novamente.",
            )

        # 1. Calcular hash do refresh token recebido
        refresh_hash = hashlib.sha256(refresh_token_string.encode()).hexdigest()

        # 2-3. Validar que refresh token existe e não foi revogado
        refresh_token_db = RefreshTokenRepository.get_by_hash(
            refresh_hash,
            session,
            device_hint=device_hint,
        )

        if not refresh_token_db:
            logger.warning(
                "[AUTH] Refresh falhou: token inválido ou revogado",
            )
            raise HTTPException(
                status_code=status.HTTP_401_UNAUTHORIZED,
                detail="Refresh token inválido ou revogado. Por favor, faça login novamente.",
            )

        # 4. Validar expiração com grace period
        now = utc_now_datetime()
        grace_deadline = refresh_token_db.expires_at.replace(
            tzinfo=timezone.utc
        ) + timedelta(hours=settings.refresh_grace_hours)

        if now > grace_deadline:
            # Fora da janela de grace -> força novo login
            RefreshTokenRepository.revoke_by_hash(refresh_hash, session)
            logger.warning(
                "[AUTH] Refresh falhou: refresh token fora da janela de grace"
            )
            raise HTTPException(
                status_code=status.HTTP_401_UNAUTHORIZED,
                detail="Sessão expirada. Por favor, faça login novamente.",
            )

        # 5. Carregar user e validar que está ativo e email verificado
        user = UserRepository.get_by_id(refresh_token_db.user_id, session)
        if not user or not user.is_active:
            logger.warning(
                "[AUTH] Refresh falhou: user inativo ou removido para user_id=%s",
                refresh_token_db.user_id[:8],
            )
            raise HTTPException(
                status_code=status.HTTP_401_UNAUTHORIZED,
                detail="Conta inativa ou removida. Entra em contacto com o suporte.",
            )

        if user.role == "trainer" and not user.email_verified:
            RefreshTokenRepository.revoke_by_hash(refresh_hash, session)
            logger.warning(
                "[AUTH] Refresh rejeitado: Personal Trainer email não verificado user_id=%s",
                user.id[:8],
            )
            raise HTTPException(
                status_code=status.HTTP_403_FORBIDDEN,
                detail="Email não verificado. Verifique a sua caixa de entrada.",
            )

        # 6. Emitir novo JWT access token
        expire_delta = timedelta(minutes=settings.access_token_expire_minutes)
        new_jwt_token = create_access_token(
            subject=user.id,
            role=user.role,
            full_name=user.full_name,
            client_id=user.client_id if user.role == "client" else None,
            expires_delta=expire_delta,
        )

        # 7. Calcular hash do novo JWT e atualizar active_tokens
        try:
            new_jwt_hash = hashlib.sha256(new_jwt_token.encode()).hexdigest()
            new_expires_at = now + expire_delta
            ActiveTokenRepository.save_or_replace(
                user_id=user.id,
                token_hash=new_jwt_hash,
                expires_at=new_expires_at,
                session=session,
            )

            # 8. ROTATION: Revogar refresh token antigo, emitir novo
            RefreshTokenRepository.revoke_by_hash(refresh_hash, session)

            new_refresh_string = secrets.token_urlsafe(32)
            new_refresh_hash = hashlib.sha256(new_refresh_string.encode()).hexdigest()
            new_refresh_expires = now + timedelta(days=30)

            RefreshTokenRepository.save(
                user_id=user.id,
                token_hash=new_refresh_hash,
                expires_at=new_refresh_expires,
                device_hint=device_hint,
                session=session,
            )
        except Exception as e:
            # Se rotação falhar, não retornar novos tokens
            logger.error(
                "[AUTH] Falha ao renovar tokens (refresh rotation) para user_id=%s: %s",
                user.id[:8],
                str(e),
            )
            raise HTTPException(
                status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
                detail="Erro ao renovar sessão. Por favor, tenta novamente.",
            ) from e

        logger.info(
            "[AUTH] Token renovado (refresh rotation) para user_id=%s",
            user.id[:8],
        )

        return {
            "access_token": new_jwt_token,
            "refresh_token": new_refresh_string,
            "role": user.role,
            "user_id": user.id,
            "full_name": user.full_name,
        }
