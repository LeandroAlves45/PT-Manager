"""ConfiguraÃ§Ã£o central da aplicaÃ§Ã£o via variÃ¡veis de ambiente."""

from pydantic import Field
from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    """
    ConfiguraÃ§Ãµes da aplicaÃ§Ã£o

    database_url:
    - SQlite local: "sqlite:///./pt_manager.db"
    - PostgreSQL: "postgresql+psycopg2://user:pass@host:5432/dbname"

    Novas variÃ¡veis para Stripe:
        STRIPE_SECRET_KEY      : chave secreta da API Stripe (sk_test_... ou sk_live_...)
        STRIPE_WEBHOOK_SECRET  : chave para verificar assinaturas dos webhooks (whsec_...)
        STRIPE_PRICE_FREE      : ID do Price Stripe para o tier FREE (0â‚¬)
        STRIPE_PRICE_STARTER   : ID do Price Stripe para o tier STARTER (20â‚¬/mÃªs)
        STRIPE_PRICE_PRO       : ID do Price Stripe para o tier PRO (40â‚¬/mÃªs)
        STRIPE_SUCCESS_URL     : URL de redirecionamento apÃ³s checkout bem sucedido
        STRIPE_CANCEL_URL      : URL de redirecionamento apÃ³s cancelamento do checkout
    """

    # pydantic Settings v2
    model_config = SettingsConfigDict(
        env_file=".env", extra="ignore", case_sensitive=False
    )

    # Database
    database_url: str

    # API Key
    api_key: str

    # JWT
    secret_key: str

    access_token_expire_minutes: int = Field(
        default=60, description="Minutos de expiraÃ§Ã£o do JWT"
    )

    refresh_grace_hours: int = Field(
        default=4,
        description="Horas de grace period para refresh token apÃ³s expiraÃ§Ã£o",
    )

    # ============================================================
    # CONFIGURAÃ‡ÃƒO DE INVITE & SUBSCRIPTION
    # ============================================================

    invite_expiry_days: int = Field(
        default=7, description="Dias de validade do token de convite de clientes"
    )
    trial_days: int = Field(
        default=15, description="Dias de trial gratuito para novos trainers"
    )

    # ============================================================
    # CONFIGURAÃ‡ÃƒO DE STRIPE
    # ============================================================

    stripe_secret_key: str = Field(
        default="", description="Chave secreta da API Stripe"
    )
    stripe_webhook_secret: str = Field(
        default="", description="Chave para verificar webhooks do Stripe"
    )
    stripe_price_free: str = Field(
        default="", description="ID do Price Stripe para tier FREE"
    )
    stripe_price_starter: str = Field(
        default="", description="ID do Price Stripe para tier STARTER"
    )
    stripe_price_pro: str = Field(
        default="", description="ID do Price Stripe para tier PRO"
    )
    stripe_success_url: str = Field(
        default="http://localhost:5173/billing?success=true",
        description="URL apÃ³s checkout bem-sucedido",
    )
    stripe_cancel_url: str = Field(
        default="http://localhost:5173/billing?cancelled=true",
        description="URL apÃ³s cancelamento do checkout",
    )
    stripe_portal_url: str = Field(
        default="http://localhost:5173/billing",
        description="URL do billing portal do Stripe",
    )

    # ============================================================
    # CONFIGURAÃ‡ÃƒO DE NOTIFICAÃ‡Ã•ES
    # ============================================================

    timezone: str = Field(
        default="Europe/Lisbon", description="Fuso horÃ¡rio para notificaÃ§Ãµes"
    )
    reminder_hour_local: int = Field(
        default=9, description="Hora local para enviar notificaÃ§Ãµes"
    )
    trainer_email: str | None = Field(
        default=None, description="Email do trainer para notificaÃ§Ãµes"
    )
    notification_test_mode: bool = Field(
        default=False, description="Modo teste para notificaÃ§Ãµes"
    )
    notification_test_minutes: int = Field(
        default=2, description="Minutos para teste de notificaÃ§Ãµes"
    )

    # ============================================================
    # CONFIGURAÃ‡ÃƒO DE EMAIL SERVICE
    # ============================================================

    resend_api_key: str = Field(default="", description="Chave da API do Resend")
    email_from: str = Field(
        default="", description="Email remetente para emails via Resend"
    )

    # ============================================================
    # CONFIGURAÃ‡ÃƒO DE CLOUDINARY
    # ============================================================

    cloudinary_cloud_name: str = Field(
        default="", description="Cloud name do Cloudinary"
    )
    cloudinary_api_key: str = Field(default="", description="API key do Cloudinary")
    cloudinary_api_secret: str = Field(
        default="", description="API secret do Cloudinary"
    )

    # ============================================================
    # CONFIGURAÃ‡ÃƒO DE CORS
    # ============================================================

    cors_origins: str = Field(
        default="http://localhost:3000, http://localhost:5173",
        description="Origins permitidos para CORS",
    )

    # ============================================================
    # SUPERUSER SEED
    # ============================================================

    superuser_email: str = Field(default="", description="Email do superuser inicial")
    superuser_password: str = Field(
        default="", description="Password do superuser inicial"
    )
    superuser_name: str = Field(
        default="Admin", description="Nome do superuser inicial"
    )

    # ============================================================
    # MONITORAMENTO & LOGGING
    # ============================================================

    sentry_dsn: str = Field(default="", description="DSN do Sentry para monitoramento")
    environment: str = Field(default="development", description="Ambiente da aplicaÃ§Ã£o")

    # ============================================================
    # SEED DE DADOS DE DEMONSTRAÃ‡ÃƒO
    # ============================================================

    seed_demo_data: bool = Field(
        default=False, description="Seed dados de demo na inicializaÃ§Ã£o"
    )
    default_trainer_email: str = Field(
        default="", description="Email do trainer de demo"
    )
    default_trainer_password: str = Field(
        default="", description="Password do trainer de demo"
    )
    default_trainer_name: str = Field(default="", description="Nome do trainer de demo")
    default_client_email: str = Field(
        default="", description="Email do cliente de demo"
    )
    default_client_password: str = Field(
        default="", description="Password do cliente de demo"
    )
    default_client_name: str = Field(default="", description="Nome do cliente de demo")


settings = Settings()
