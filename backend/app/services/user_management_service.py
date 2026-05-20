"""Serviço de gestão de utilizadores -> CRUD com validações de segurança."""

import logging

from fastapi import HTTPException, status
from sqlmodel import Session

from app.core.security import hash_password, verify_password
from app.db.models import User, Client
from app.repositories.user_repository import UserRepository
from app.repositories.active_token_repository import ActiveTokenRepository
from app.repositories.refresh_token_repository import RefreshTokenRepository
from app.utils.db_errors import commit_or_rollback
from app.utils.time import utc_now_datetime

logger = logging.getLogger(__name__)


class UserManagementService:
    """Orquestra CRUD de users com tenant isolation e session invalidation."""

    @staticmethod
    def _assert_can_update_user(
        current_user: User,
        target: User,
        session: Session,
    ) -> None:
        """Valida permissão para PATCH /auth/users/{id} (deny by default)."""
        if current_user.role == "superuser":
            return

        if current_user.id == target.id:
            return

        if current_user.role == "client":
            logger.warning(
                "[AUTH] Client user_id=%s tentou editar user_id=%s",
                current_user.id[:8],
                target.id[:8],
            )
            raise HTTPException(
                status_code=status.HTTP_403_FORBIDDEN,
                detail="Sem permissão para atualizar este utilizador",
            )

        if current_user.role == "trainer":
            if target.role != "client":
                logger.warning(
                    "[AUTH] Trainer user_id=%s tentou editar user role=%s",
                    current_user.id[:8],
                    target.role,
                )
                raise HTTPException(
                    status_code=status.HTTP_403_FORBIDDEN,
                    detail="Sem permissão para atualizar este utilizador",
                )

            client = UserRepository.get_client_for_user(target.id, session)
            if not client or client.owner_trainer_id != current_user.id:
                logger.warning(
                    "[AUTH] Trainer user_id=%s tentou editar client fora do tenant",
                    current_user.id[:8],
                )
                raise HTTPException(
                    status_code=status.HTTP_403_FORBIDDEN,
                    detail="Sem permissão para atualizar este utilizador",
                )
            return

        raise HTTPException(
            status_code=status.HTTP_403_FORBIDDEN,
            detail="Sem permissão para atualizar este utilizador",
        )

    @staticmethod
    def list_users(
        current_user: User,
        session: Session,
    ) -> list[dict]:
        """
        Lista users conforme role do utilizador.

        Segurança (Tenant Isolation):
        - Superuser: vê todos
        - Trainer: vê apenas users dos seus clientes
        - Client: não deve chamar este método (bloqueado em routes)
        """

        users = UserRepository.list_users_by_trainer(
            trainer_id=current_user.id,
            session=session,
            is_superuser=current_user.role == "superuser",
        )

        return [
            {
                "id": user.id,
                "email": user.email,
                "full_name": user.full_name,
                "role": user.role,
            }
            for user in users
        ]

    @staticmethod
    def update_user(
        user_id: str,
        data: dict,
        current_user: User,
        session: Session,
    ) -> dict:
        """
        Atualiza user com validação de acesso e transações seguras.

        Regras de acesso (Tenant Isolation):
        - Clients: só editam o próprio perfil
        - Trainers: editam users dos seus clientes (tenants)
        - Superusers: editam qualquer user

        Segurança:
        - Valida permissões antes de qualquer BD access
        - Atualiza em BD com transação segura (commit_or_rollback)
        - Log de auditoria para cada atualização

        Raises:
            HTTPException: 403 (sem permissão), 404 (user não encontrado)
        """
        user = UserRepository.get_by_id(user_id, session)
        if not user:
            raise HTTPException(
                status_code=status.HTTP_404_NOT_FOUND,
                detail="Utilizador não encontrado",
            )

        UserManagementService._assert_can_update_user(current_user, user, session)

        updated_user = UserRepository.update_user(user, data, session)

        logger.info(
            "[AUDIT] User actualizado: user_id=%s por user_id=%s",
            user_id[:8],
            current_user.id[:8],
        )

        return {
            "id": updated_user.id,
            "email": updated_user.email,
            "full_name": updated_user.full_name,
            "role": updated_user.role,
        }

    @staticmethod
    def change_password(
        user_id: str,
        current_password: str,
        new_password: str,
        session: Session,
    ) -> None:
        """
        Altera password com transações seguras e invalidação de sessões.

        Segurança:
        - Valida password atual (constant-time comparison via bcrypt)
        - Atualiza password em BD (transação segura via commit_or_rollback)
        - Invalida TODOS os tokens ativos (forced logout em todos os devices)
        - Password mínimo 8, máximo 128 chars

        Fluxo:
        1. Valida que user existe
        2. Verifica password atual
        3. Hash da nova password
        4. Atualiza password em BD (transação segura)
        5. Revoga todos os access tokens
        6. Revoga todos os refresh tokens (logout total)

        Raises:
            HTTPException: 404 se user não encontrado, 400 se password errada
        """

        # Busca user
        user = UserRepository.get_by_id(user_id, session)
        if not user:
            raise HTTPException(
                status_code=status.HTTP_404_NOT_FOUND,
                detail="Utilizador não encontrado",
            )

        # Valida password atual
        if not verify_password(current_password, user.hashed_password):
            logger.warning(
                "[AUTH] Tentativa de mudança de password com password errada para user_id=%s",
                user_id[:8],
            )
            raise HTTPException(
                status_code=status.HTTP_400_BAD_REQUEST,
                detail="Password atual errada",
            )

        # Revogar todos os tokens ativos
        ActiveTokenRepository.revoke_by_user_id(user_id, session)
        RefreshTokenRepository.revoke_all_by_user_id(user_id, session)

        # Alterar password
        hashed_pwd = hash_password(new_password)
        UserRepository.change_password(user, hashed_pwd, session)

        logger.info(
            "[AUDIT] Password alterada para user_id=%s",
            user_id[:8],
        )

    @staticmethod
    def create_client_user_for_trainer(
        email: str,
        password: str,
        full_name: str,
        client_id: str,
        current_trainer: User,
        session: Session,
    ) -> dict:
        """
        Personal Trainer cria User role=client ligado a Client do seu tenant.
        Personal Trainers registam-se via signup; superuser não usa este endpoint.
        """

        if current_trainer.role == "superuser":
            raise HTTPException(
                status_code=status.HTTP_403_FORBIDDEN,
                detail="Superusers não criam utilizadores por este endpoint",
            )

        if current_trainer.role != "trainer":
            raise HTTPException(
                status_code=status.HTTP_403_FORBIDDEN,
                detail="Acesso restrito a Personal Trainers.",
            )

        client = session.get(Client, client_id)
        if not client or client.archived_at is not None:
            raise HTTPException(
                status_code=status.HTTP_404_NOT_FOUND,
                detail="Client não encontrado ou arquivado",
            )

        if client.owner_trainer_id != current_trainer.id:
            raise HTTPException(
                status_code=status.HTTP_403_FORBIDDEN,
                detail="Sem permissão para criar conta para este cliente.",
            )

        existing_user = UserRepository.get_by_client_id(client_id, session)
        if existing_user:
            raise HTTPException(
                status_code=status.HTTP_409_CONFLICT,
                detail="Client já tem um utilizador associado",
            )

        try:
            hashed_pwd = hash_password(password)
            new_user = UserRepository.create_user(
                email=email,
                hashed_password=hashed_pwd,
                full_name=full_name,
                role="client",
                client_id=client_id,
                session=session,
            )

            new_user.email_verified = True
            new_user.email_verified_at = utc_now_datetime()
            session.add(new_user)
            commit_or_rollback(session)
            session.refresh(new_user)

            logger.info(
                "[AUDIT] Conta client criada: user_id=%s por trainer_id=%s",
                new_user.id[:8],
                current_trainer.id[:8],
            )

            return {
                "id": new_user.id,
                "email": new_user.email,
                "full_name": new_user.full_name,
                "role": new_user.role,
            }

        except ValueError as e:
            error_msg = str(e)
            logger.warning("[AUTH] Falha ao criar user: %s", error_msg)
            if "já está registado" in error_msg:
                raise HTTPException(
                    status_code=status.HTTP_409_CONFLICT,
                    detail="Email já está registado",
                ) from e
            raise HTTPException(
                status_code=status.HTTP_400_BAD_REQUEST,
                detail=error_msg,
            ) from e
