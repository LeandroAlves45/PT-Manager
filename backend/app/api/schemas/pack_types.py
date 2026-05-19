from typing import Optional
from datetime import date
from sqlmodel import SQLModel, Field

class PackTypeCreate(SQLModel):
    """Payload para criar um tipo de pack."""

    name: str = Field(min_length=1, max_length=100)
    sessions_total: int = Field(ge=1, le=500)


class PackTypeRead(SQLModel):
    """Resposta pública de um tipo de pack."""

    id: str
    owner_trainer_id: Optional[str] = None
    name: str
    sessions_total: int
    is_active: bool
    created_at: date
    updated_at: date


class PackTypeUpdate(SQLModel):
    """Payload para atualizar parcialmente um tipo de pack."""

    name: Optional[str] = Field(default=None, min_length=1, max_length=100)
    sessions_total: Optional[int] = Field(default=None, ge=1, le=500)
    is_active: Optional[bool] = Field(default=None)