"""
Router de administração - apenas superusers.

O superuser tem visibilidade global sobre toda a plataforma:
    - Ver todos os trainers e o estado das suas subscrições
    - Suspender/reactivar trainers
    - Ver métricas globais (total de trainers, clientes, receita estimada)
    - Gerir o catálogo global de exercícios e alimentos

Endpoints:
    GET  /admin/metrics              — métricas globais
    GET  /admin/trainers             — lista todos os trainers
    POST /admin/trainers/{id}/suspend  — suspende um trainer
    POST /admin/trainers/{id}/activate — reactiva um trainer
    POST /admin/trainers/{id}/grant-exemption    — concede isenção de billing
    POST /admin/trainers/{id}/revoke-exemption   — revoga isenção de billing
"""
from typing import Optional

from fastapi import APIRouter, Depends, HTTPException, status
from sqlmodel import Session

from app.db.database import get_session
from app.core.security import require_superuser
from app.api.schemas.admin import TrainerSummary, PlatformMetrics
from app.services.admin_service import AdminService

router = APIRouter(prefix="/admin", tags=["Admin"])

#-------------------------------
# Endpoints
#-------------------------------

@router.get("/metrics", response_model=PlatformMetrics)
async def get_metrics(
    session: Session = Depends(get_session),
    current_user = Depends(require_superuser) # pylint: disable=unused-argument
) -> PlatformMetrics:
    """Devolve métricas globais da plataforma para o superuser."""

    return AdminService.get_platform_metrics(session)

@router.get("/trainers", response_model=list[TrainerSummary])
async def list_trainers(
    status_filter: Optional[str] = None,
    session: Session = Depends(get_session),
    current_user = Depends(require_superuser) # pylint: disable=unused-argument
) -> list[TrainerSummary]:
    """
    Lista todos os Personal Trainers com o estado das suas subscrições.
    Permite filtrar por estado.
    """

    return AdminService.list_trainers(session, status_filter)

@router.post("/trainers/{trainer_id}/suspend", status_code=status.HTTP_200_OK)
async def suspend_trainer(
    trainer_id: str,
    session: Session = Depends(get_session),
    current_user = Depends(require_superuser) # pylint: disable=unused-argument
) -> dict:
    """Suspende um Personal Trainer, bloqueia acesso imediatamente."""

    try:
        return  {"detail": AdminService.suspend_trainer(trainer_id, session)}
    except ValueError as e:
        raise HTTPException(status_code=404, detail=str(e)) from e

@router.post("/trainers/{trainer_id}/activate", status_code=status.HTTP_200_OK)
async def activate_trainer(
    trainer_id: str,
    session: Session = Depends(get_session),
    current_user = Depends(require_superuser) # pylint: disable=unused-argument
) -> dict:
    """Reativa um Personal Trainer suspenso."""

    try:
        return  {"detail": AdminService.activate_trainer(trainer_id, session)}
    except ValueError as e:
        raise HTTPException(status_code=404, detail=str(e)) from e

@router.post("/trainers/{trainer_id}/grant-exemption", status_code=status.HTTP_200_OK)
async def grant_exemption(
    trainer_id: str,
    session: Session = Depends(get_session),
    current_user = Depends(require_superuser) # pylint: disable=unused-argument
) -> dict:
    """
    Concede isenção de billing a um Personal Trainer (FREE forever).
    Um trainer isento tem acesso equivalente á plataforma como o tier PRO, sem Stripe.
    Nunca pode ser auto-atribuída, apenas por um superuser.
    """

    try:
        return  {"detail": AdminService.grant_exemption(trainer_id, session)}
    except ValueError as e:
        raise HTTPException(status_code=404, detail=str(e)) from e

@router.post("/trainers/{trainer_id}/revoke-exemption", status_code=status.HTTP_200_OK)
async def revoke_exemption(
    trainer_id: str,
    session: Session = Depends(get_session),
    current_user = Depends(require_superuser) # pylint: disable=unused-argument
) -> dict:
    """
    Revoga isenção de billing de um Personal Trainer, 
    obrigando-o a subscrever para continuar a usar a plataforma.
    Senão será bloqueado com HTTP 402
    """

    try:
        return  {"detail": AdminService.revoke_exemption(trainer_id, session)}
    except ValueError as e:
        raise HTTPException(status_code=404, detail=str(e)) from e
