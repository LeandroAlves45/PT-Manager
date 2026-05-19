"""Modelo para Refresh Tokens -> segurança da sessão."""

import uuid
from datetime import datetime, timezone
from typing import Optional
from sqlmodel import SQLModel, Field
from app.utils.time import utc_now_datetime


class RefreshToken(SQLModel, table=True):
    """Tokens de refresh — permitem renovar access tokens sem pedir credenciais.

    Segurança:
    - Cada refresh_token é uma string aleatória opaca guardada como hash SHA-256
    - Suporta rotation: cada refresh emite um novo token e revoga o anterior
    - Revogação: logout revoga todos os refresh tokens do user
    """

    __tablename__ = "refresh_tokens"

    id: str = Field(default_factory=lambda: str(uuid.uuid4()), primary_key=True)

    user_id: str = Field(foreign_key="users.id", index=True)

    token_hash: str = Field(unique=True, index=True)

    expires_at: datetime = Field(index=True)

    created_at: datetime = Field(default_factory=lambda: datetime.now(timezone.utc))

    revoked_at: Optional[datetime] = None

    device_hint: Optional[str] = None

    @property
    def is_active(self) -> bool:
        """Retorna True se não foi revogado e ainda não expirou."""

        now = utc_now_datetime()
        return self.revoked_at is None and self.expires_at > now
