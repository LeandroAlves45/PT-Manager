"""Repository para operações de Client (perfil de aluno) na base de dados."""

from typing import Optional
from sqlmodel import Session, select
from app.db.models import Client
from app.utils.db_errors import commit_or_rollback
from app.utils.time import utc_now_datetime


class ClientRepository:
    """Abstrai acesso a dados de Client (perfil de aluno).

    Segurança: Usa soft delete com archived_at (não apaga dados históricos).
    """

    @staticmethod
    def get_by_id(client_id: str, session: Session) -> Optional[Client]:
        """Busca Client ativo pelo seu id."""
        client = session.get(Client, client_id)
        if client and client.archived_at is not None:
            return None
        return client

    @staticmethod
    def create_client(
        full_name: str,
        phone: str,
        owner_trainer_id: str,
        session: Session,
    ) -> Client:
        """Cria novo Client (perfil de aluno) para um Personal Trainer.

        Args:
            full_name: Nome do cliente (validado: min 2 chars)
            phone: Telefone (validado: min 7 chars)
            owner_trainer_id: UUID do trainer que o criou
            session: Database session

        Returns:
            Client recém criado

        Raises:
            ValueError: Se dados inválidos
        """
        if not full_name or len(full_name) < 2:
            raise ValueError("full_name deve ter pelo menos 2 caracteres")

        if not phone or len(phone) < 7:
            raise ValueError("phone deve ter pelo menos 7 caracteres")

        new_client = Client(
            full_name=full_name,
            phone=phone,
            owner_trainer_id=owner_trainer_id,
        )
        session.add(new_client)
        commit_or_rollback(session)
        session.refresh(new_client)
        return new_client

    @staticmethod
    def get_by_owner_trainer_id(
        trainer_id: str,
        session: Session,
    ) -> list[Client]:
        """Busca todos os clientes ativos de um trainer.

        Exclui clientes arquivados (soft delete com archived_at != None).

        Args:
            trainer_id: UUID do trainer
            session: Database session

        Returns:
            Lista de Client ordenados por created_at (desc)
        """
        stmt = select(Client).where(
            Client.owner_trainer_id == trainer_id,
            Client.archived_at.is_(None),
        ).order_by(Client.created_at.desc())
        return session.exec(stmt).all()

    @staticmethod
    def archive_client(
        client_id: str,
        session: Session,
    ) -> None:
        """Soft delete de um cliente (marca como arquivado).

        Args:
            client_id: UUID do cliente
            session: Database session

        Raises:
            ValueError: Se cliente não encontrado
        """
        client = session.get(Client, client_id)
        if not client:
            raise ValueError(f"Client {client_id} não encontrado")

        client.archived_at = utc_now_datetime()
        session.add(client)
        commit_or_rollback(session)
