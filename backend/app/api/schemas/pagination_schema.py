"""Schemas Pydantic para paginação."""

from pydantic import BaseModel, Field
from app.api.schemas.auth import UserRead

class PaginationParams(BaseModel):
    """Parâmetros de paginação."""

    skip: int = Field(0, ge=0)
    limit: int = Field(10, ge=1, le=100)

class PaginatedUserRead(BaseModel):
    """Dados paginados de utilizadores."""

    users: list[UserRead]
    total: int
    skip: int
    limit: int
