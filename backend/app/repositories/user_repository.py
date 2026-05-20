"""Repository para operações de User na base de dados."""

from datetime import datetime
from typing import Optional
from sqlmodel import Session, select
from app.db.models import User, Client
from app.utils.db_errors import commit_or_rollback
from app.utils.time import utc_now_datetime


class UserRepository:
    """Abstrai acesso a dados de User."""

    @staticmethod
    def get_by_client_id(client_id: str, session: Session) -> Optional[User]:
        """Busca User pelo seu client_id."""

        return session.exec(select(User).where(User.client_id == client_id)).first()

    @staticmethod
    def get_by_invite_token_hash(token_hash: str, session: Session) -> Optional[User]:
        """Busca User pelo hash do token de convite."""

        return session.exec(
            select(User).where(User.invite_token_hash == token_hash)
        ).first()

    @staticmethod
    def get_by_email_verification_token_hash(
        token_hash: str, session: Session
    ) -> Optional[User]:
        """Busca User pelo hash do token de verificação de email."""
        return session.exec(
            select(User).where(User.email_verification_token_hash == token_hash)
        ).first()

    @staticmethod
    def get_by_email(email: str, session: Session) -> Optional[User]:
        """Busca User pelo seu email."""
        return session.exec(select(User).where(User.email == email)).first()

    @staticmethod
    def get_by_id(user_id: str, session: Session) -> Optional[User]:
        """Busca User pelo seu id."""
        return session.get(User, user_id)

    @staticmethod
    def create_user(
        email: str,
        hashed_password: str,
        full_name: str,
        role: str,
        client_id: Optional[str],
        session: Session,
    ) -> User:
        """
        Cria novo User com validações de segurança.

        Validações:
        - Email único
        - Se role='client', client_id deve existir e não ter user associado
        - Password já deve estar hashed (hash_password() chamado antes)

        Raises:
            ValueError: Se validações falharem
        """

        # 1. Validar email único
        existing = UserRepository.get_by_email(email, session)
        if existing:
            raise ValueError(f"Email '{email}' já está registado.")

        # 2. Validar client_id se role='client'
        if role == "client" and client_id:
            client = session.get(Client, client_id)
            if not client:
                raise ValueError(f"Cliente {client_id} não encontrado.")

            already_linked = UserRepository.get_by_client_id(client_id, session)
            if already_linked:
                raise ValueError(f"Cliente {client_id} já tem um user associado.")

        # 3. Criar novo User
        new_user = User(
            email=email,
            hashed_password=hashed_password,
            full_name=full_name,
            role=role,
            client_id=client_id if role == "client" else None,
        )

        session.add(new_user)
        commit_or_rollback(session)
        session.refresh(new_user)

        return new_user

    @staticmethod
    def list_users_by_trainer(
        trainer_id: str,
        session: Session,
        is_superuser: bool = False,
        skip: int = 0,
        limit: int = 10,
    ) -> list[User]:
        """
        Lista users conforme role do utilizador com paginacao.

        Seguranca (Tenant Isolation):
        - Superuser: ve todos
        - Trainer: ve apenas users dos seus clientes
        - Client: (nao deve chamar este metodo)

        Paginacao: todos os utilizadores (superuser ou trainer) recebem skip/limit.
        """

        query = select(User)

        if not is_superuser:
            query = query.join(Client, User.client_id == Client.id).where(
                Client.owner_trainer_id == trainer_id
            )

        return session.exec(query.offset(skip).limit(limit)).all()

    @staticmethod
    def get_client_for_user(
        user_id: str,
        session: Session,
    ) -> Optional[Client]:
        """Busca o cliente associado a um user (para tenant isolation)."""

        user = UserRepository.get_by_id(user_id, session)
        if not user or not user.client_id:
            return None

        return session.get(Client, user.client_id)

    @staticmethod
    def save_invite_token(
        user: User,
        token_hash: str,
        expires_at: datetime,
        session: Session,
    ) -> None:
        """Persiste token de convite no User."""

        user.invite_token_hash = token_hash
        user.invite_token_expires_at = expires_at
        user.updated_at = utc_now_datetime()

        session.add(user)
        commit_or_rollback(session)

    @staticmethod
    def activate_with_password(
        user: User,
        hashed_password: str,
        session: Session,
    ) -> None:
        """Ativa conta do user com password e invalida token de convite."""

        user.hashed_password = hashed_password
        user.invite_token_hash = None
        user.invite_token_expires_at = None
        user.is_active = True
        user.updated_at = utc_now_datetime()

        session.add(user)
        commit_or_rollback(session)

    @staticmethod
    def update_user(
        user: User,
        data: dict,
        session: Session,
    ) -> User:
        """Atualiza campos do user (exclude_unset=True)."""

        for key, value in data.items():
            setattr(user, key, value)

        user.updated_at = utc_now_datetime()
        session.add(user)
        commit_or_rollback(session)
        session.refresh(user)

        return user

    @staticmethod
    def change_password(
        user: User,
        hashed_password: str,
        session: Session,
    ) -> None:
        """Altera a password do user."""

        user.hashed_password = hashed_password
        user.updated_at = utc_now_datetime()

        session.add(user)
        commit_or_rollback(session)

    @staticmethod
    def get_all_trainers(session: Session) -> list[User]:
        """Obtém todos os Personal Trainers da base de dados."""
        return session.exec(select(User).where(User.role == "trainer")).all()

    @staticmethod
    def get_trainer_or_raise(trainer_id: str, session: Session) -> User:
        """Obtém um Personal Trainer da base de dados ou lança uma exceção."""

        trainer = session.get(User, trainer_id)
        if not trainer or trainer.role != "trainer":
            raise ValueError("Personal Trainer não encontrado.")
        return trainer
