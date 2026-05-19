"""Serviço de negócio para o fluxo de convite de clientes."""

import logging
import secrets
import hashlib
from datetime import timedelta, timezone
from fastapi import HTTPException, status
from sqlmodel import Session, select

from app.db.models import User, Client, ActiveToken, TrainerSettings
from app.core.security import create_access_token, hash_password
from app.core.config import settings
from app.repositories.user_repository import UserRepository
from app.services.email_service import EmailService
from app.utils.db_errors import commit_or_rollback
from app.utils.time import utc_now_datetime

logger = logging.getLogger(__name__)


def _sha256(raw_token: str) -> str:
    """Calcula SHA-256 hash de um token."""

    return hashlib.sha256(raw_token.encode()).hexdigest()


def _get_frontend_base_url() -> str:
    """Obtém a base URL do frontend."""
    origins = settings.cors_origins.split(",")  # pylint: disable=no-member
    return origins[0].strip().rstrip("/")


class InviteService:
    """Orquestra o fluxo de convite de clientes."""

    @staticmethod
    def generate_invite(
        client_id: str,
        current_user: User,
        session: Session,
    ) -> str:
        """
        Gera link de convite para um cliente.

        Fluxo:
        1. Valida que cliente existe e pertence ao Personal Trainer
        2. Valida que cliente tem conta User
        3. Gera token aleatório (32 bytes)
        4. Persiste hash do token (SHA-256)
        5. Envia email (best-effort)
        6. Retorna link de convite

        Raises:
            HTTPException: Se cliente não encontrado, acesso negado, ou sem User account
        """

        # 1. Validar que cliente existe
        client = session.get(Client, client_id)
        if not client or client.archived_at is not None:
            raise HTTPException(
                status_code=status.HTTP_404_NOT_FOUND,
                detail="Cliente não encontrado ou arquivado",
            )

        # 2. Validar ownership (cliente pertence ao Personal Trainer)
        if client.owner_trainer_id != current_user.id:
            raise HTTPException(
                status_code=status.HTTP_403_FORBIDDEN,
                detail="Acesso negado: Cliente não pertence ao Personal Trainer",
            )

        # 3. Validar que cliente tem conta User
        user = UserRepository.get_by_client_id(client_id, session)
        if not user:
            raise HTTPException(
                status_code=status.HTTP_404_NOT_FOUND,
                detail="Este cliente ainda não tem conta de utilizador",
            )

        # 4. Gerar token de convite
        raw_token = secrets.token_urlsafe(32)
        token_hash = _sha256(raw_token)
        expires_at = utc_now_datetime() + timedelta(days=settings.invite_expiry_days)

        # 5. Persistir token
        UserRepository.save_invite_token(user, token_hash, expires_at, session)

        # 6. Construir link de convite
        base = _get_frontend_base_url()
        invite_link = f"{base}/invite/{raw_token}"

        # 7. Enviar email de convite (best-effort — falha não bloqueia o link)
        InviteService._send_invite_email(
            client=client,
            current_user=current_user,
            invite_link=invite_link,
            session=session,
        )

        return invite_link

    @staticmethod
    def validate_invite_token(
        token: str,
        session: Session,
    ) -> dict:
        """
        Valida token de convite.

        Retorna:
            {
                "valid": bool,
                "client_name": str,
                "message": str,
            }

        Checks:
        - Token hash existe na BD
        - Token não está expirado
        """

        token_hash = _sha256(token)
        user = UserRepository.get_by_invite_token_hash(token_hash, session)

        # Token inválido ou já utilizado
        if not user or not user.client_id:
            return {
                "valid": False,
                "client_name": "",
                "message": "Token inválido ou já utilizado.",
            }

        # Validar expiração do token
        now = utc_now_datetime()
        if (
            user.invite_token_expires_at is None
            or now > user.invite_token_expires_at.replace(tzinfo=timezone.utc)
        ):
            return {
                "valid": False,
                "client_name": "",
                "message": "Token expirado. "
                "Por favor peça um novo convite ao seu Personal Trainer.",
            }

        # Token válido, obter nome do cliente
        client = session.get(Client, user.client_id)
        client_name = client.full_name if client else ""

        return {
            "valid": True,
            "client_name": client_name,
            "message": "",
        }

    @staticmethod
    def set_password_via_invite(
        token: str,
        new_password: str,
        session: Session,
    ) -> dict:
        """
        Cliente define password via token de convite.

        Fluxo:
        1. Valida token (existence + expiração)
        2. Hash da password
        3. Ativa conta (is_active=True)
        4. Invalida token (one-time use)
        5. Cria JWT
        6. Persiste ActiveToken

        Retorna:
            {
                "access_token": str,
                "role": str,
                "user_id": str,
                "full_name": str,
            }

        Raises:
            HTTPException: Se token inválido, expirado, ou erro na criação de JWT
        """

        token_hash = _sha256(token)
        user = UserRepository.get_by_invite_token_hash(token_hash, session)

        # Validar que token existe e user tem client
        if not user or not user.client_id:
            raise HTTPException(
                status_code=status.HTTP_400_BAD_REQUEST,
                detail="Token inválido ou já utilizado.",
            )

        # Validar expiração do token
        now = utc_now_datetime()
        if (
            user.invite_token_expires_at is None
            or now > user.invite_token_expires_at.replace(tzinfo=timezone.utc)
        ):
            raise HTTPException(
                status_code=status.HTTP_400_BAD_REQUEST,
                detail="Token expirado. Por favor peça um novo convite ao seu Personal Trainer.",
            )

        # Hash da password e ativa conta
        hashed_password = hash_password(new_password)
        UserRepository.activate_with_password(user, hashed_password, session)

        # Criar JWT
        expire_delta = timedelta(minutes=settings.access_token_expire_minutes)
        jwt_token = create_access_token(
            subject=user.id,
            role=user.role,
            full_name=user.full_name,
            client_id=user.client_id,
            expires_delta=expire_delta,
        )

        # Persistir ActiveToken
        InviteService._save_active_token(user.id, jwt_token, expire_delta, session)

        return {
            "access_token": jwt_token,
            "role": user.role,
            "user_id": user.id,
            "full_name": user.full_name,
        }

    @staticmethod
    def _send_invite_email(
        client: Client,
        current_user: User,
        invite_link: str,
        session: Session,
    ) -> None:
        """
        Envia email de convite (best-effort).
        Falha no email NÃO bloqueia a criação do token.
        """

        if not client.email:
            logger.warning(
                "[INVITE] ❌ Cliente %s não tem email registado", client.id[:8]
            )
            return

        try:
            trainer_settings = session.exec(
                select(TrainerSettings).where(
                    TrainerSettings.trainer_user_id == current_user.id
                )
            ).first()

            app_name = (
                (trainer_settings.app_name or "PT Manager")
                if trainer_settings
                else "PT Manager"
            )
            trainer_logo_url = current_user.logo_url or ""

            EmailService.send_invite_email(
                to_email=client.email,
                client_name=client.full_name,
                invite_link=invite_link,
                trainer_name=current_user.full_name,
                app_name=app_name,
                trainer_logo_url=trainer_logo_url,
                expires_in_days=settings.invite_expiry_days,
            )
            logger.info("[INVITE] Email de convite enviado para %s", client.email)

        except Exception as email_error:  # pylint: disable=broad-exception-caught
            logger.warning(
                "[INVITE] Email não enviado para %s: %s. Token criado com sucesso.",
                client.email,
                email_error,
            )

    @staticmethod
    def _save_active_token(
        user_id: str,
        jwt_token: str,
        expire_delta: timedelta,
        session: Session,
    ) -> None:
        """Remove token anterior e insere novo ActiveToken."""

        existing = session.exec(
            select(ActiveToken).where(ActiveToken.user_id == user_id)
        ).first()

        if existing:
            session.delete(existing)
            session.flush()

        expires_at = utc_now_datetime() + expire_delta
        session.add(
            ActiveToken(user_id=user_id, token=jwt_token, expires_at=expires_at)
        )
        commit_or_rollback(session)
