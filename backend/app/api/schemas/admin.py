"""Schemas para a API de Administração."""

from datetime import datetime
from typing import Optional

from pydantic import BaseModel


class TrainerSummary(BaseModel):
    """Resumo de um Personal Trainer."""

    user_id: str
    full_name: str
    email: str
    is_active: bool
    is_exempt_from_billing: bool
    subscription_status: Optional[str]
    subscription_tier: Optional[str]
    active_clients_count: int
    monthly_eur: int
    trial_end: Optional[datetime]
    joined_at: datetime

    model_config = {
        "from_attributes": True,
    }


class PlatformMetrics(BaseModel):
    """Métricas globais da plataforma."""

    total_trainers: int
    active_trainers: int
    trialing_trainers: int
    total_clients: int
    estimated_monthly_revenue_eur: int
