"""Serviço de gestão de utilizadores -> CRUD com validações de segurança."""

import logging

from fastapi import HTTPException, status
from sqlmodel import Session

from app.core.security import hash_password, verify_password
from app.db.models import User
from app.repositories.user_repository import UserRepository
from app.repositories.active_token_repository import ActiveTokenRepository
from app.repositories.refresh_token_repository import RefreshTokenRepository

logger = logging.getLogger(__name__)


class UserManagementService:
    """Orquestra CRUD de users com tenant isolation e session invalidation."""

    @staticmethod
    def create_user(
        email: str,
        password: str,
        full_name: str,
        role: str,
        client_id: str = None,
        session: Session = None,
    ) -> dict:
        """
        Cria novo user com validações e transações seguras.

        Segurança:
        - Email único (validado em BD via constraint)
        - Password hashed via bcrypt
        - Se role='client', client_id deve existir
        - Transação automática: commit via UserRepository, rollback em caso de erro

        Fluxo:
        1. Hash da password
        2. Valida email único e client_id (se aplicável)
        3. Cria user em BD com transação segura (commit_or_rollback)
        4. Converte erros BD em HTTPException apropriadas

        Raises:
            HTTPException: 409 (email/client existe), 404 (client não encontrado), 400 (validação)
        """

        try:
            hashed_pwd = hash_password(password)

            new_user = UserRepository.create_user(
                email=email,
                hashed_password=hashed_pwd,
                full_name=full_name,
                role=role,
                client_id=client_id,
                session=session,
            )

            logger.info(
                "[AUDIT] User criado: email=%s..., role=%s",
                email[:3],
                role,
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

            if "não encontrado" in error_msg:
                raise HTTPException(
                    status_code=status.HTTP_404_NOT_FOUND,
                    detail="Client não encontrado",
                ) from e

            if "já tem user associado" in error_msg:
                raise HTTPException(
                    status_code=status.HTTP_409_CONFLICT,
                    detail="Client já tem um utilizador associado",
                ) from e

            raise HTTPException(
                status_code=status.HTTP_400_BAD_REQUEST,
                detail=error_msg,
            ) from e

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

        # Validar acesso: Clients só o próprio
        if current_user.role == "client" and current_user.id != user_id:
            logger.warning(
                "[AUTH] Tentativa de edição não autorizada por client user_id=%s em user_id=%s",
                current_user.id[:8],
                user_id[:8],
            )
            raise HTTPException(
                status_code=status.HTTP_403_FORBIDDEN,
                detail="Sem permissão para atualizar este utilizador",
            )

        # Validar acesso: Personal Trainers só users dos seus tenants
        elif current_user.role == "trainer" and current_user.id != user_id:
            user_to_update = UserRepository.get_by_id(user_id, session)
            if not user_to_update:
                raise HTTPException(
                    status_code=status.HTTP_404_NOT_FOUND,
                    detail="Utilizador não encontrado",
                )

            # Se é client. verificar que o cliente pertence ao Personal Trainer
            if user_to_update.role == "client" and user_to_update.client_id:
                client = UserRepository.get_client_for_user(user_id, session)
                if not client or client.owner_trainer_id != current_user.id:
                    logger.warning(
                      "[AUTH] Trainer user_id=%s tentou editar client fora do seu tenant",
                      current_user.id[:8],
                    )
                    raise HTTPException(
                      status_code=status.HTTP_403_FORBIDDEN,
                      detail="Sem permissão para atualizar este utilizador",
                    )

        elif current_user.role != "superuser":
            # Não é client, trainer nem superuser
            raise HTTPException(
                status_code=status.HTTP_403_FORBIDDEN,
                detail="Sem permissão para atualizar este utilizador",
            )

        # Buscar user
        user = UserRepository.get_by_id(user_id, session)
        if not user:
            raise HTTPException(
                status_code=status.HTTP_404_NOT_FOUND,
                detail="Utilizador não encontrado",
            )

        # Atualizar user
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
