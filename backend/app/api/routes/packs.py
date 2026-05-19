"""
Rotas para gestão de packs de sessões do cliente.

Responsabilidades:
  - Compra de packs de sessões para clientes
  - Listagem de histórico de packs comprados
  - Obtenção do pack ativo (em uso) do cliente
  - Snapshot de sessions_total no momento da compra para auditoria

Regras de negócio:
  - Um cliente só pode ter 1 pack ativo por vez
  - Clientes arquivados não podem comprar novos packs
  - O snapshot de sessões é imutável (para rastreabilidade)
"""

import logging
from fastapi import APIRouter, Depends, HTTPException, status
from sqlmodel import Session, select
from sqlalchemy.exc import SQLAlchemyError

from app.db.database import get_session
from app.core.security import require_active_subscription
from app.db.models.client import Client
from app.db.models.pack import ClientPack
from app.api.schemas.pack import ClientPackPurchase, ClientPackRead
from app.services.pack_service import PackService

logger = logging.getLogger(__name__)

router = APIRouter(prefix="/packs", tags=["Packs"])


def _get_owned_client(session: Session, client_id: str, current_user) -> Client:
    """
    Obtém um cliente garantindo isolamento por trainer.
    """
    client = session.get(Client, client_id)
    if not client:
        raise HTTPException(
            status_code=status.HTTP_404_NOT_FOUND,
            detail=f"Cliente com ID {client_id} não encontrado.",
        )

    if current_user.role == "trainer" and client.owner_trainer_id != current_user.id:
        raise HTTPException(
            status_code=status.HTTP_403_FORBIDDEN,
            detail="Acesso negado a cliente de outro trainer.",
        )

    return client


@router.post(
    "/clients/{client_id}/purchase",
    response_model=ClientPackRead,
    status_code=status.HTTP_201_CREATED,
)
async def purchase_pack_for_client(
    client_id: str,
    payload: ClientPackPurchase,
    session: Session = Depends(get_session),
    current_user=Depends(require_active_subscription),
) -> ClientPack:
    """
    Compra um pack para um cliente, com snapshot de sessions_total do pack_type.
    """

    try:
        _get_owned_client(session, client_id, current_user)
        new_pack = PackService.purchase_pack(
            session=session,
            client_id=client_id,
            pack_type_id=payload.pack_type_id,
            purchase_at=payload.purchase_at,
        )

        logger.info(
            "[PACK] ✅ Pack comprado: cliente=%s, pack_type_id=%s",
            client_id,
            payload.pack_type_id,
        )

        return new_pack

    except SQLAlchemyError as e:
        session.rollback()
        logger.error(
            "[PACK] ❌ SQLAlchemyError na compra de pack: cliente=%s, erro=%s",
            client_id,
            getattr(e, "orig", e),
            exc_info=True,
        )
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail="Erro ao comprar pack. Por favor, tente novamente.",
        ) from e


@router.get("/clients/{client_id}", response_model=list[ClientPackRead])
async def list_client_packs(
    client_id: str,
    session: Session = Depends(get_session),
    current_user=Depends(require_active_subscription),
) -> list[ClientPack]:
    """
    Lista packs comprados por um cliente.
    """
    try:
        _get_owned_client(session, client_id, current_user)
        stmt = (
            select(ClientPack)
            .where(ClientPack.client_id == client_id)
            .order_by(ClientPack.purchase_at.desc())  # pylint: disable=no-member
        )

        return list(session.exec(stmt).all())

    except SQLAlchemyError as e:
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail="Erro ao listar packs do cliente.",
        ) from e


@router.get("/clients/{client_id}/active", response_model=ClientPackRead | None)
async def get_active_pack(
    client_id: str,
    session: Session = Depends(get_session),
    current_user=Depends(require_active_subscription),
) -> ClientPack | None:
    """
    Retorna o pack ativo (se existir).
    """

    try:
        _get_owned_client(session, client_id, current_user)
        return PackService.get_active_pack(session=session, client_id=client_id)

    except SQLAlchemyError as e:
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail="Erro ao obter pack ativo.",
        ) from e


@router.put(
    "/clients/{client_id}/consume",
    response_model=ClientPackRead,
    status_code=status.HTTP_200_OK,
)
async def consume_session(
    client_id: str,
    session: Session = Depends(get_session),
    current_user=Depends(require_active_subscription),
) -> ClientPack:
    """
    Consome 1 sessão do pack ativo do cliente.

    Fluxo:
      1. Obter pack ativo
      2. Validar se há sessões disponíveis
      3. Incrementar sessions_used
      4. Se pack terminou: arquivar
      5. Commit e log de auditoria
    """

    try:
        _get_owned_client(session, client_id, current_user)
        active_pack = PackService.consume_session(session=session, client_id=client_id)
        sessions_remaining = (
            active_pack.sessions_total_snapshot - active_pack.sessions_used
        )

        logger.info(
            "[PACK] ✅ Sessão consumida: cliente=%s, pack_id=%s, restantes=%d, status=%s",
            client_id,
            active_pack.id,
            sessions_remaining,
            "finalizado" if sessions_remaining == 0 else "ativo",
        )

        return active_pack

    except SQLAlchemyError as e:
        session.rollback()
        logger.error(
            "[PACK] ❌ SQLAlchemyError na consumação de sessão: cliente=%s, erro=%s",
            client_id,
            getattr(e, "orig", e),
            exc_info=True,
        )
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail="Erro ao consumir sessão. Por favor, tente novamente.",
        ) from e
