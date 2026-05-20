"""Serviço de signup de Personal Trainer com verificação de email."""

import hashlib
import logging
import secrets
from datetime import timedelta

from fastapi import HTTPException
from sqlmodel import Session

from app.core.config import settings
from app.core.security import create_access_token, hash_password
from app.db.models import TrainerSubscription
from app.repositories.user_repository import UserRepository
from app.repositories.active_token_repository import ActiveTokenRepository
from app.repositories.refresh_token_repository import RefreshTokenRepository
from app.services.email_service import EmailService
from app.utils.time import utc_now_datetime
from app.utils.db_errors import commit_or_rollback

logger = logging.getLogger(__name__)


class SignupService:
    """Orquestra signup de novo Personal Trainer."""

    def __init__(self):
        self.email_service = EmailService()

    @staticmethod
    def _generate_verification_token() -> tuple[str, str]:
        """Gera token de verificacao + SHA256 hash.

        Returns:
            (token_raw, token_hash): Token aleatorio de 32 chars + SHA256 hash
        """
        token_raw = secrets.token_urlsafe(24)
        token_hash = hashlib.sha256(token_raw.encode()).hexdigest()
        return token_raw, token_hash

    TRAINER_SIGNUP_GENERIC_MESSAGE = (
        "Se o registo for válido, enviámos um email de verificação. "
        "Verifique a sua caixa de entrada."
    )

    def trainer_signup(
        self,
        email: str,
        password: str,
        full_name: str,
        session: Session,
    ) -> dict:
        """Signup de novo Personal Trainer com email verification obrigatório.

        Fluxo:
        1. Cria User(role="trainer", email_verified=False)
        2. Cria TrainerSubscription(status="trialing", trial_end=+15d)
        3. Gera verification token (SHA256, 15 min TTL)
        4. Envia email via EmailService
        5. Retorna user_id e message

        Args:
            email: Email do trainer (unico)
            password: Password (8-128 chars, sera hashed)
            full_name: Nome completo do trainer
            session: Database session

        Returns:
            { "id": user_id, "email": email, "full_name": full_name,
            "role": "trainer", "message": "..." }

        Raises:
            HTTPException 400: Email ja existe
            HTTPException 500: Erro ao enviar email
        """
        try:
            # 1. Criar User com email_verified=False
            hashed_password = hash_password(password)
            user = UserRepository.create_user(
                email=email,
                hashed_password=hashed_password,
                full_name=full_name,
                role="trainer",
                client_id=None,
                session=session,
            )

            # 2. Criar TrainerSubscription (trial de 15 dias)
            trial_end = utc_now_datetime() + timedelta(days=15)
            subscription = TrainerSubscription(
                trainer_user_id=user.id,
                status="trialing",
                trial_end=trial_end,
            )
            session.add(subscription)
            commit_or_rollback(session)

            # 3. Gerar verification token
            token_raw, token_hash = self._generate_verification_token()

            # 4. Guardar token hash em campo dedicado
            user.email_verification_token_hash = token_hash
            user.email_verification_token_expires_at = utc_now_datetime() + timedelta(
                minutes=15
            )
            session.add(user)
            commit_or_rollback(session)

            # 5. Enviar email
            verification_link = (
                f"{settings.frontend_url}/verify-email?token={token_raw}"
            )
            self.email_service.send_verification_email(
                trainer_email=email,
                trainer_name=full_name,
                verification_link=verification_link,
            )

            logger.info(
                "Registo de Personal Trainer bem-sucedido: user_id=%s",
                user.id[:8],
            )

            return {
                "id": user.id,
                "email": user.email,
                "full_name": user.full_name,
                "role": "trainer",
                "message": self.TRAINER_SIGNUP_GENERIC_MESSAGE,
            }

        except ValueError as e:
            error_msg = str(e)
            if "já está registado" in error_msg:
                logger.info(
                    "Signup do Personal Trainer ignorado: email já registado",
                )
                return {
                    "id": "",
                    "email": email,
                    "full_name": full_name,
                    "role": "trainer",
                    "message": self.TRAINER_SIGNUP_GENERIC_MESSAGE,
                }
            raise HTTPException(status_code=409, detail=error_msg) from e
        except Exception as e:
            logger.error("Iniciar sessão do Personal Trainer falhou: %s", str(e))
            raise HTTPException(
                status_code=500,
                detail="Erro ao enviar email de verificação. Por favor, tente novamente.",
            ) from e

    def verify_email_token(
        self,
        token: str,
        session: Session,
    ) -> dict:
        """Valida token de verificação de email e marca como verificado.

        Fluxo:
        1. Calcula SHA256(token)
        2. Busca User com email_verification_token_hash == SHA256(token)
        3. Valida token nao expirou (15 min TTL)
        4. Marca email_verified=True + email_verified_at=now()
        5. Invalida token (set email_verification_token_hash=None)
        6. Retorna access_token + refresh_token (httpOnly cookie)

        Args:
            token: Token de verificacao (raw, nao hash)
            session: Database session

        Returns:
            { "email": email, "is_verified": True, "message": "..." }

        Raises:
            HTTPException 400: Token invalido ou expirado
        """
        try:
            # 1. Calcular SHA256(token)
            token_hash = hashlib.sha256(token.encode()).hexdigest()

            # 2. Buscar User
            user = UserRepository.get_by_email_verification_token_hash(
                token_hash, session
            )

            if not user:
                raise HTTPException(
                    status_code=400,
                    detail="Token de verificação inválido ou expirado.",
                )

            if user.role != "trainer" or user.email_verified:
                raise HTTPException(
                    status_code=400,
                    detail="Token de verificação inválido ou expirado.",
                )

            # 3. Validar token nao expirou
            if (
                not user.email_verification_token_expires_at
                or utc_now_datetime() > user.email_verification_token_expires_at
            ):
                raise HTTPException(
                    status_code=400,
                    detail="Token de verificação expirado. Peça um novo.",
                )

            # 4. Marcar email_verified=True
            user.email_verified = True
            user.email_verified_at = utc_now_datetime()
            user.email_verification_token_hash = None
            user.email_verification_token_expires_at = None

            session.add(user)
            commit_or_rollback(session)

            # 6. Gerar access token e refresh token
            expires_delta = timedelta(minutes=settings.access_token_expire_minutes)
            jwt_token = create_access_token(
                subject=user.id,
                role=user.role,
                full_name=user.full_name,
                client_id=None,
                expires_delta=expires_delta,
            )

            # 7. Persistir access token em active_tokens
            jwt_hash = hashlib.sha256(jwt_token.encode()).hexdigest()
            expires_at = utc_now_datetime() + expires_delta
            ActiveTokenRepository.save_or_replace(
                user_id=user.id,
                token_hash=jwt_hash,
                expires_at=expires_at,
                session=session,
            )

            # 8. Gerar refresh token opaco
            refresh_token_string = secrets.token_urlsafe(32)
            refresh_token_hash = hashlib.sha256(
                refresh_token_string.encode()
            ).hexdigest()

            # 9. Persistir refresh token hash
            refresh_expires = utc_now_datetime() + timedelta(days=30)
            RefreshTokenRepository.save(
                user_id=user.id,
                token_hash=refresh_token_hash,
                expires_at=refresh_expires,
                device_hint=None,
                session=session,
            )

            logger.info("Email verificado para o utilizador %s", user.id)

            return {
                "access_token": jwt_token,
                "refresh_token": refresh_token_string,
                "email": user.email,
                "is_verified": True,
                "message": "Email verificado com sucesso. Pode agora iniciar sessão.",
            }

        except HTTPException:
            raise
        except Exception as e:
            logger.error("Erro ao verificar email: %s", str(e))
            raise HTTPException(
                status_code=500,
                detail="Erro ao verificar email. Por favor, tente novamente.",
            ) from e

    RESEND_VERIFICATION_MESSAGE = (
        "Se esta conta precisar de verificação, foi enviado um email. "
        "Verifique a sua caixa de entrada."
    )

    def resend_verification_email(self, email: str, session: Session) -> dict:
        """Reenvia email de verificação para trainer (sem revelar estado da conta).

        Sempre devolve a mesma mensagem genérica para evitar enumeração de emails.
        """
        user = UserRepository.get_by_email(email, session)

        should_send = (
            user is not None
            and user.role == "trainer"
            and not user.email_verified
            and (utc_now_datetime() - user.created_at) <= timedelta(hours=48)
        )

        if should_send:
            token_raw, token_hash = self._generate_verification_token()
            user.email_verification_token_hash = token_hash
            user.email_verification_token_expires_at = utc_now_datetime() + timedelta(
                minutes=15
            )
            session.add(user)
            commit_or_rollback(session)

            verification_link = (
                f"{settings.frontend_url}/verify-email?token={token_raw}"
            )
            self.email_service.send_verification_email(
                trainer_email=user.email,
                trainer_name=user.full_name,
                verification_link=verification_link,
            )
            logger.info(
                "Email de verificação reenviado: user_id=%s",
                user.id[:8],
            )

        return {"message": self.RESEND_VERIFICATION_MESSAGE}
