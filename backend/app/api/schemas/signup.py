"""Schemas de validação para signup (Personal Trainer + Client)."""

from typing import Optional
from pydantic import BaseModel, EmailStr, Field


class TrainerSignupIn(BaseModel):
    """Request body para signup de novo Personal Trainer.

    Fluxo:
    1. Trainer regista-se com email + password + full_name
    2. Sistema cria User(role="trainer") + TrainerSubscription(status="trialing", trial_end=+15d)
    3. Email de verificação é enviado
    4. Após verificar email, trainer ganha acesso à plataforma
    """

    email: EmailStr = Field(description="Email único do Personal Trainer")
    password: str = Field(
        min_length=8,
        max_length=128,
    )
    full_name: str = Field(
        min_length=2, max_length=200, description="Nome completo do trainer"
    )


class TrainerSignupOut(BaseModel):
    """Response após signup de Personal Trainer."""

    id: Optional[str] = Field(description="User ID (vazio se resposta genérica)")
    email: str
    full_name: str
    role: str = "trainer"
    message: str = Field(
        description="Mensagem de sucesso (ex: 'Verification email sent')"
    )



class EmailVerificationIn(BaseModel):
    """Request para verificar email do trainer (P0.1).

    Fluxo:
    1. Trainer recebe email com token de verificação
    2. Submete token para validação
    3. Sistema:
       - Calcula SHA256(token) e busca em bd
       - Valida token não expirou (15 min)
       - Marca User.email_verified = True
       - Retorna access_token + refresh_token (httpOnly)
    """

    token: str = Field(
        description="Email verification token (SHA-256 hash armazenado em BD)"
    )


class EmailVerificationOut(BaseModel):
    """Response após verificação bem-sucedida do email."""

    access_token: str
    refresh_token: Optional[str] = None
    email: str = Field(description="Email verificado")
    is_verified: bool = True
    message: str = Field(description="Confirmação de sucesso")


class ResendVerificationIn(BaseModel):
    """Pedido para reenviar email de verificação."""

    email: EmailStr = Field(description="Email do Personal Trainer")


class ResendVerificationOut(BaseModel):
    """Resposta genérica — igual para todos os pedidos (anti-enumeração)."""

    message: str = Field(description="Confirmação genérica")
