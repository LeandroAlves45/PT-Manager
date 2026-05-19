"""
Serviços de negócio relacionados a packs (pacotes de sessões).

Responsabilidades:
  - Compra de packs com snapshot de sessions_total para auditoria
  - Queries para obter pack ativo de um cliente
  - Validações de cliente e pack_type
"""

from __future__ import annotations
import logging
from datetime import date
from typing import Optional
from fastapi import HTTPException, status
from sqlmodel import Session, select

from app.db.models.client import Client
from app.db.models.pack import ClientPack, PackType
from app.utils.db_errors import commit_or_rollback
from app.utils.time import utc_now

logger = logging.getLogger(__name__)


class PackService:
    """
    Serviços de negócio relacionados a pacotes.

    Aqui é onde fica:
    - Compra do pack (com snapshot sessions_total do pack_type)
    - Queries de "pack ativo"
    """

    @staticmethod
    def purchase_pack(
        session: Session,
        client_id: str,
        pack_type_id: str,
        purchase_at: Optional[date] = None,
    ) -> ClientPack:
        """
        Compra um pack com validações completas.
        Cria snapshot de sessions_total para auditoria.
        """

        client = session.get(Client, client_id)
        if not client:
            logger.warning(
                "[PACK] ⚠️ Tentativa de compra com cliente inexistente: client_id=%s",
                client_id,
            )
            raise HTTPException(
                status_code=status.HTTP_404_NOT_FOUND,
                detail=f"Cliente com ID {client_id} não encontrado.",
            )

        if client.archived_at is not None:
            logger.warning(
                "[PACK] ⚠️ Tentativa de compra com cliente arquivado: client_id=%s",
                client_id,
            )
            raise HTTPException(
                status_code=status.HTTP_400_BAD_REQUEST,
                detail="Cliente arquivado não pode comprar packs.",
            )

        pack_type = session.get(PackType, pack_type_id)
        if not pack_type:
            logger.warning(
                "[PACK] ⚠️ Tentativa de compra com pack_type inexistente: pack_type_id=%s",
                pack_type_id,
            )
            raise HTTPException(
                status_code=status.HTTP_404_NOT_FOUND,
                detail="Tipo de pack não encontrado.",
            )

        if not pack_type.is_active:
            logger.warning(
                "[PACK] ⚠️ Tentativa de compra com pack_type inativo: pack_type_id=%s",
                pack_type_id,
            )
            raise HTTPException(
                status_code=status.HTTP_400_BAD_REQUEST,
                detail="Tipo de pack inativo não pode ser comprado.",
            )

        if (
            pack_type.owner_trainer_id is not None
            and pack_type.owner_trainer_id != client.owner_trainer_id
        ):
            logger.warning(
                "[PACK] ⚠️ Tentativa de compra com pack_type de outro trainer: client_id=%s, pack_type_id=%s",
                client_id,
                pack_type_id,
            )
            raise HTTPException(
                status_code=status.HTTP_403_FORBIDDEN,
                detail="Tipo de pack não pertence ao trainer deste cliente.",
            )

        active_pack = PackService.get_active_pack(session=session, client_id=client_id)
        if active_pack:
            logger.warning(
                "[PACK] ⚠️ Tentativa de compra com pack ativo: client_id=%s, active_pack_id=%s",
                client_id,
                active_pack.id,
            )
            raise HTTPException(
                status_code=status.HTTP_400_BAD_REQUEST,
                detail="Cliente já possui um pack ativo. "
                "Não é permitido comprar um novo pack antes de finalizar o atual.",
            )

        purchased_at = purchase_at or utc_now()

        client_pack = ClientPack(
            client_id=client_id,
            pack_type_id=pack_type_id,
            client_name=client.full_name,
            purchase_at=purchased_at,
            sessions_total_snapshot=pack_type.sessions_total,
            sessions_used=0,
        )

        session.add(client_pack)
        commit_or_rollback(session)
        session.refresh(client_pack)

        logger.info(
            "[PACK] ✅ Pack comprado (via serviço): client_id=%s, pack_id=%s, pack_type_id=%s, sessões=%d",
            client_id,
            client_pack.id,
            pack_type_id,
            pack_type.sessions_total,
        )
        return client_pack

    @staticmethod
    def get_active_pack(session: Session, client_id: str) -> Optional[ClientPack]:
        """
        Retorna o pack ativo de um cliente.

        Pack ativo é aquele que não está cancelado e tem sessões restantes.
        """

        statement = (
            select(ClientPack)
            .where(ClientPack.client_id == client_id)
            .where(ClientPack.archived_at.is_(None))  # pylint: disable=no-member
            .where(ClientPack.cancelled_at.is_(None))  # pylint: disable=no-member
            .where(ClientPack.sessions_used < ClientPack.sessions_total_snapshot)
            .order_by(ClientPack.purchase_at.desc())  # pylint: disable=no-member
        ).limit(1)

        return session.exec(statement).first()

    @staticmethod
    def consume_session(session: Session, client_id: str) -> ClientPack:
        """
        Consome uma sessão do pack ativo do cliente.
        Marca pack como arquivado se todas as sessões foram consumidas.
        """
        active_pack = PackService.get_active_pack(session=session, client_id=client_id)
        if not active_pack:
            logger.warning(
                "[PACK] ⚠️ Tentativa de consumir sessão sem pack ativo: client_id=%s",
                client_id,
            )
            raise HTTPException(
                status_code=status.HTTP_404_NOT_FOUND,
                detail="Cliente não tem pack ativo.",
            )

        sessions_remaining = (
            active_pack.sessions_total_snapshot - active_pack.sessions_used
        )
        if sessions_remaining <= 0:
            logger.warning(
                "[PACK] ⚠️ Tentativa de consumir sessão sem saldo: client_id=%s, pack_id=%s",
                client_id,
                active_pack.id,
            )
            raise HTTPException(
                status_code=status.HTTP_400_BAD_REQUEST,
                detail="Cliente não tem sessões disponíveis.",
            )

        active_pack.sessions_used += 1
        sessions_remaining -= 1

        if sessions_remaining == 0:
            active_pack.archived_at = utc_now()

        session.add(active_pack)
        commit_or_rollback(session)
        session.refresh(active_pack)

        logger.info(
            "[PACK] ✅ Sessão consumida (via serviço): client_id=%s, pack_id=%s, restantes=%d, status=%s",
            client_id,
            active_pack.id,
            sessions_remaining,
            "finalizado" if sessions_remaining == 0 else "ativo",
        )
        return active_pack
