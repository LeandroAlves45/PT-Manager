"""Schemas de validação para signup (Personal Trainer + Client)."""

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

    id: str = Field(description="User ID do trainer")
    email: str
    full_name: str
    role: str = "trainer"
    message: str = Field(
        description="Mensagem de sucesso (ex: 'Verification email sent')"
    )


class ClientSignupIn(BaseModel):
    """Request body para signup de novo Client (via convite de trainer).

    Fluxo:
    1. Trainer convida client (gera invite_token com 7 dias de validade)
    2. Client regista-se com email + password + full_name + phone + invite_token
    3. Sistema valida:
       - invite_token é válido e não expirou
       - trainer que convidou tem subscrição ATIVA
    4. Cria User(role="client") + Client(perfil de aluno)
    5. Client ganha acesso imediatamente (sem verificação de email)

    Segurança:
    - invite_token deve ser enviado por email, não em URL pública
    - Valida trainer.subscription.status in [trialing, active, past_due]
    - Rate limiting: 10/hora por IP + email
    """

    email: EmailStr = Field(description="Email do client")
    password: str = Field(
        min_length=8,
        max_length=128,
    )
    full_name: str = Field(
        min_length=2,
        max_length=200,
    )
    phone: str = Field(
        min_length=7,
        max_length=15,
    )
    invite_token: str = Field(
        description="Token de convite enviado pelo trainer (válido 7 dias)"
    )


class ClientSignupOut(BaseModel):
    """Response após signup de Client."""

    access_token: str
    refresh_token: str
    id: str = Field(description="User ID do client")
    email: str
    full_name: str
    role: str = "client"
    client_id: str = Field(description="Client profile ID")
    message: str = Field(description="Mensagem de sucesso")


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
    refresh_token: str
    email: str = Field(description="Email verificado")
    is_verified: bool = True
    message: str = Field(description="Confirmação de sucesso")
