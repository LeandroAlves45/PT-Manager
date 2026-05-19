"""
Rotas para gestão de tipos de packs (catálogo de packs disponíveis).

Responsabilidades:
  - Criar novos tipos de pack com validação de nomes únicos
  - Listar todos os tipos de pack disponíveis
  - Atualizar pack types (nome, sessões, status ativo)
  - Eliminar tipos de pack

Regras de negócio:
  - Nomes de pack types devem ser únicos
  - Sessions_total deve estar entre 1 e 500
  - Pack types podem ser marcados como inativos (is_active=False)
"""

import logging
from fastapi import APIRouter, Depends, HTTPException, status
from sqlalchemy import or_
from sqlmodel import Session, select
from sqlalchemy.exc import SQLAlchemyError

from app.db.database import get_session
from app.core.security import require_active_subscription
from app.db.models.pack import PackType
from app.api.schemas.pack_types import PackTypeCreate, PackTypeUpdate, PackTypeRead
from app.utils.db_errors import commit_or_rollback

logger = logging.getLogger(__name__)

router = APIRouter(prefix="/pack-types", tags=["Pack-Types"])


def _pack_type_visibility_filter(current_user):
    """
    Devolve o filtro de visibilidade dos tipos de pack para o utilizador atual.
    """
    if current_user.role == "superuser":
        return True

    return or_(
        PackType.owner_trainer_id.is_(None),  # pylint: disable=no-member
        PackType.owner_trainer_id == current_user.id,
    )


def _get_owned_pack_type(
    session: Session, pack_type_id: str, current_user
) -> PackType:
    """
    Obtém um pack type garantindo que o trainer pode gerir esse registo.
    """
    pack_type = session.get(PackType, pack_type_id)
    if not pack_type:
        logger.warning(
            "[PACK-TYPE] ⚠️ Pack type inexistente: id=%s",
            pack_type_id,
        )
        raise HTTPException(
            status_code=status.HTTP_404_NOT_FOUND,
            detail="Pack Type não encontrado.",
        )

    if current_user.role == "trainer" and pack_type.owner_trainer_id != current_user.id:
        raise HTTPException(
            status_code=status.HTTP_403_FORBIDDEN,
            detail="Acesso negado a Pack Type de outro trainer.",
        )

    return pack_type


@router.get("", response_model=list[PackTypeRead])
async def list_pack_types(
    session: Session = Depends(get_session),
    current_user=Depends(require_active_subscription),
) -> list[PackTypeRead]:
    """
    Lista todos os tipos de pack ordenados por nome.
    """

    try:
        stmt = (
            select(PackType)
            .where(_pack_type_visibility_filter(current_user))
            .order_by(PackType.name)
        )
        pack_types = session.exec(stmt).all()

        logger.info(
            "[PACK-TYPE] ✅ Listagem: %d pack types retornados", len(pack_types)
        )
        return pack_types

    except SQLAlchemyError as e:
        logger.error(
            "[PACK-TYPE] ❌ SQLAlchemyError na listagem: erro=%s",
            getattr(e, "orig", e),
            exc_info=True,
        )
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail="Erro ao listar Pack Types.",
        ) from e


@router.post("", response_model=PackTypeRead, status_code=status.HTTP_201_CREATED)
async def create_pack_type(
    payload: PackTypeCreate,
    session: Session = Depends(get_session),
    current_user=Depends(require_active_subscription),
) -> PackType:
    """
    Cria um novo tipo de pack com validação de nome único.
    """
    try:
        existing = session.exec(
            select(PackType)
            .where(PackType.name == payload.name)
            .where(_pack_type_visibility_filter(current_user))
        ).first()

        if existing:
            logger.warning(
                "[PACK-TYPE] ⚠️ Tentativa de criar pack type duplicado: nome=%s",
                payload.name,
            )
            raise HTTPException(
                status_code=status.HTTP_400_BAD_REQUEST,
                detail=f"Pack Type com este nome já existe: '{payload.name}'",
            )

        pack_type = PackType(
            name=payload.name,
            sessions_total=payload.sessions_total,
            owner_trainer_id=None if current_user.role == "superuser" else current_user.id,
        )
        session.add(pack_type)
        commit_or_rollback(session)
        session.refresh(pack_type)

        logger.info(
            "[PACK-TYPE] ✅ Pack type criado: id=%s, nome=%s, sessões=%d",
            pack_type.id,
            pack_type.name,
            pack_type.sessions_total,
        )
        return pack_type

    except SQLAlchemyError as e:
        session.rollback()
        logger.error(
            "[PACK-TYPE] ❌ SQLAlchemyError ao criar pack type: nome=%s, erro=%s",
            payload.name,
            getattr(e, "orig", e),
            exc_info=True,
        )
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail="Erro ao criar Pack Type.",
        ) from e


@router.put("/{pack_type_id}", response_model=PackTypeRead)
async def update_pack_type(
    pack_type_id: str,
    payload: PackTypeUpdate,
    session: Session = Depends(get_session),
    current_user=Depends(require_active_subscription),
) -> PackType:
    """
    Atualiza um pack type (nome, sessões, status ativo) com validações.
    """

    try:
        pack_type = _get_owned_pack_type(session, pack_type_id, current_user)

        # Validar nome único se for alterado
        if payload.name is not None and payload.name != pack_type.name:
            existing = session.exec(
                select(PackType)
                .where(PackType.name == payload.name)
                .where(_pack_type_visibility_filter(current_user))
                .where(PackType.id != pack_type_id)
            ).first()

            if existing:
                logger.warning(
                    "[PACK-TYPE] ⚠️ Tentativa de atualizar para"
                    "nome duplicado: id=%s, novo_nome=%s",
                    pack_type_id,
                    payload.name,
                )
                raise HTTPException(
                    status_code=status.HTTP_400_BAD_REQUEST,
                    detail=f"Já existe Pack Type com este nome: '{payload.name}'",
                )

            pack_type.name = payload.name

        # Atualizar sessions_total se fornecido
        if payload.sessions_total is not None:
            pack_type.sessions_total = payload.sessions_total

        # Atualizar is_active se fornecido
        if payload.is_active is not None:
            pack_type.is_active = payload.is_active

        session.add(pack_type)
        commit_or_rollback(session)
        session.refresh(pack_type)

        logger.info(
            "[PACK-TYPE] ✅ Pack type atualizado: id=%s, nome=%s, sessões=%d, ativo=%s",
            pack_type.id,
            pack_type.name,
            pack_type.sessions_total,
            pack_type.is_active,
        )
        return pack_type

    except SQLAlchemyError as e:
        session.rollback()
        logger.error(
            "[PACK-TYPE] ❌ SQLAlchemyError ao atualizar pack type: id=%s, erro=%s",
            pack_type_id,
            getattr(e, "orig", e),
            exc_info=True,
        )
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail="Erro ao atualizar Pack Type.",
        ) from e


@router.delete("/{pack_type_id}", status_code=status.HTTP_204_NO_CONTENT)
async def delete_pack_type(
    pack_type_id: str,
    session: Session = Depends(get_session),
    current_user=Depends(require_active_subscription),
) -> None:
    """
    Elimina um tipo de pack pelo ID.
    """
    try:
        pack_type = _get_owned_pack_type(session, pack_type_id, current_user)

        session.delete(pack_type)
        commit_or_rollback(session)

        logger.info(
            "[PACK-TYPE] ✅ Pack type eliminado: id=%s, nome=%s",
            pack_type_id,
            pack_type.name,
        )
        return None

    except SQLAlchemyError as e:
        session.rollback()
        logger.error(
            "[PACK-TYPE] ❌ SQLAlchemyError ao eliminar pack type: id=%s, erro=%s",
            pack_type_id,
            getattr(e, "orig", e),
            exc_info=True,
        )
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail="Erro ao apagar Pack Type.",
        ) from e
