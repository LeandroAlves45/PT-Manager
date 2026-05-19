"""
Ponto de entrada da aplicação FastAPI — app factory e configuração global.

Responsabilidades deste ficheiro:
    - Criar a instância FastAPI com metadados
    - Configurar CORS (origens permitidas vindas do .env)
    - Registar todos os routers com os seus prefixos e dependências
    - Hooks de startup: init_db, migrations, scheduler, seed data
    - Hook de shutdown: desligar o scheduler graciosamente

Convenção de routers:
    - Sem JWT e sem API Key: health, stripe_webhook (monitorização / Stripe HMAC)
    - Sem JWT, com API Key: auth, signup, invite
    - Com JWT e API Key: restantes routers protegidos
    A granularidade fina de permissões (role, subscrição) é gerida
    dentro de cada router individualmente via Depends.
"""

import logging
import os
from contextlib import asynccontextmanager

from fastapi import Depends, FastAPI, HTTPException, Request
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import JSONResponse
import sentry_sdk
from sentry_sdk.integrations.fastapi import FastApiIntegration
from sentry_sdk.integrations.sqlalchemy import SqlalchemyIntegration
from slowapi.errors import RateLimitExceeded
from sqlmodel import Session

from app.api.routes.admin import router as admin_router
from app.api.routes.assessments import router as assessments_router
from app.api.routes.auth import router as auth_router
from app.api.routes.billing import router as billing_router
from app.api.routes.checkins import router as checkins_router
from app.api.routes.client_portal import router as client_portal_router
from app.api.routes.client_supplements import router as client_supplements_router
from app.api.routes.clients import router as clients_router
from app.api.routes.exercises import router as exercises_router
from app.api.routes.health import router as health_router
from app.api.routes.invite import router as invite_router
from app.api.routes.notifications import router as notifications_router
from app.api.routes.nutrition import router as nutrition_router
from app.api.routes.pack_types import router as pack_types_router
from app.api.routes.packs import router as packs_router
from app.api.routes.sessions import router as sessions_router
from app.api.routes.signup import router as signup_router
from app.api.routes.stripe_webhook import router as webhooks_router
from app.api.routes.supplements import router as supplements_router
from app.api.routes.trainer_profile import router as trainer_profile_router
from app.api.routes.training_plans import router as training_plans_router
from app.core.config import settings
from app.middleware.rate_limit_email import RateLimitEmailMiddleware
from app.core.rate_limit import limiter
from app.core.security import get_current_user, require_api_key
from app.db.database import engine
from app.db.init_db import init_db
from app.db.seeds.catalogue import seed_catalogue
from app.db.seeds.demo_data import seed_demo_data
from app.db.seeds.pack_types import seed_pack_types
from app.db.seeds.superuser import seed_superuser
from app.utils.logging import setup_logging
from app.workers.scheduler import shutdown_scheduler, start_scheduler

# Sentry — monitorização de erros em produção.
# Inicializado antes de tudo para capturar erros no startup também.
# Se SENTRY_DSN não estiver configurado (.env local), o SDK fica inactivo
# sem lançar erros -> comportamento seguro para desenvolvimento.
# Sentry inicializado antes do import da app para capturar erros no startup.
# Lê SENTRY_DSN do settings (pydantic-settings) -> NUNCA de os.getenv().
# Se SENTRY_DSN estiver vazio, o bloco é ignorado silenciosamente.

try:
    if settings.sentry_dsn:
        sentry_sdk.init(
            dsn=settings.sentry_dsn,
            traces_sample_rate=0.1,
            profiles_sample_rate=0.1,
            integrations=[FastApiIntegration(), SqlalchemyIntegration()],
            environment=settings.environment,
        )
        logging.getLogger(__name__).info("[SENTRY] inicializado com sucesso.")
    else:
        logging.getLogger(__name__).info(
            "[SENTRY] DSN não configurado, Sentry inactivo."
        )
except ImportError:
    logging.getLogger(__name__).warning(
        "[SENTRY] SDK não encontrado, monitorização de erros desactivada."
    )

logger = logging.getLogger(__name__)

# Garante que a pasta de logs existe antes de configurar o logging
os.makedirs("logs", exist_ok=True)
setup_logging()

# ----------------------------------------------
# Rate Limit Exception Handler
# Definido antes da criação da app para evitar used-before-assignment
# ----------------------------------------------


def _rate_limit_exceeded_handler(_: Request, _exc: RateLimitExceeded) -> JSONResponse:
    """Handler para o Rate Limit Exceeded"""
    return JSONResponse(
        status_code=429,
        content={"detail": "Demasiadas tentativas. Tenta novamente mais tarde."},
    )


# ----------------------------------------------
# Lifespan: startup e shutdown hooks
# ----------------------------------------------


@asynccontextmanager
async def lifespan(_app: FastAPI):
    """
    Startup hooks:
    - Inicializa a base de dados
    - Executa as migrations
    - Executa as seeds
    - Inicia o scheduler
    """
    init_db()
    with Session(engine) as session:
        seed_pack_types(session)
        seed_superuser(session)
        seed_demo_data(session)
        seed_catalogue(session)
    start_scheduler()

    yield

    shutdown_scheduler()


# ----------------------------------------------
# Criação da instância FastAPI
# ----------------------------------------------

app = FastAPI(
    title="PT Manager API",
    version="0.1.0",
    description="API multi-tenant para gestão de clientes de Personal Trainers.",
    lifespan=lifespan,
)

# ----------------------------------------------
# Configuração do Limiter
# ----------------------------------------------

app.state.limiter = limiter
app.add_exception_handler(RateLimitExceeded, _rate_limit_exceeded_handler)

# -------------------------------------------------------
# Handler global de excepções
#
# Sem este handler, erros inesperados devolvem stack traces completos
# ao cliente — expõe detalhes da implementação em produção.
#
# Comportamento:
#   - HTTPException: deixa passar normalmente (tem status_code próprio)
#   - Qualquer outra Exception: devolve 500 genérico e regista o erro no log
# -------------------------------------------------------


@app.exception_handler(Exception)
async def global_exception_handler(request: Request, exc: Exception) -> JSONResponse:
    """
    Captura qualquer excepção não tratada e devolve uma resposta 500 genérica.
    O stack trace real é registado no log para diagnóstico, mas nunca exposto ao cliente.
    """
    if isinstance(exc, HTTPException):
        # Deixa passar HTTPExceptions para que o FastAPI as trate normalmente
        raise exc

    # Para outras exceções, regista o erro e devolve uma resposta genérica
    logger.error(
        "Erro não tratado em %s %s: %s",
        request.method,
        request.url,
        exc,
        exc_info=True,
    )

    return JSONResponse(
        status_code=500,
        content={"detail": "Ocorreu um erro inesperado. Por favor, tente novamente."},
    )


# ----------------------------------------------
# Configuração de CORS
# ----------------------------------------------

origins = [
    origin.strip() for origin in settings.cors_origins.split(",")  # pylint: disable=no-member
]


# Rate limit middleware - extrai email do body para limiter_email_ip
app.add_middleware(RateLimitEmailMiddleware)
app.state.limiter = limiter
app.add_exception_handler(RateLimitExceeded, _rate_limit_exceeded_handler)
app.add_middleware(
    CORSMiddleware,
    allow_origins=origins,
    allow_credentials=True,
    allow_methods=["GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS"],
    allow_headers=["Content-Type", "Authorization", "X-API-Key"],
)



# --------------------------------------
# API Key — aplicada aos routers listados abaixo (health e webhook excluídos)
# --------------------------------------

common_dependencies = [Depends(require_api_key)]

# --------------------------------------
# Router Públicos (requerem apenas API Key, sem JWT)
# --------------------------------------

# auth e signup: login e registo não precisam de estar autenticados
app.include_router(auth_router, prefix="/api/v1", dependencies=common_dependencies)
app.include_router(signup_router, prefix="/api/v1", dependencies=common_dependencies)

# Stripe webhooks: autenticados via HMAC signature, não via JWT — sem API Key
app.include_router(webhooks_router, prefix="/api/v1")

# Health check: público para monitorização externa (sem API Key)
app.include_router(health_router, prefix="/api/v1")

# Invite: os endpoints de convite
app.include_router(invite_router, prefix="/api/v1", dependencies=common_dependencies)


# ---------------------------------------------------------------------------
# Rotas PROTEGIDAS por JWT
#
# A permissão fina (role, subscrição activa) é verificada dentro de
# cada router via Depends(require_trainer), Depends(require_active_subscription), etc.
# ---------------------------------------------------------------------------

jwt_dependency = [Depends(require_api_key), Depends(get_current_user)]

app.include_router(clients_router, prefix="/api/v1", dependencies=jwt_dependency)
app.include_router(supplements_router, prefix="/api/v1", dependencies=jwt_dependency)
app.include_router(billing_router, prefix="/api/v1", dependencies=jwt_dependency)
app.include_router(admin_router, prefix="/api/v1", dependencies=jwt_dependency)
app.include_router(packs_router, prefix="/api/v1", dependencies=jwt_dependency)
app.include_router(pack_types_router, prefix="/api/v1", dependencies=jwt_dependency)
app.include_router(sessions_router, prefix="/api/v1", dependencies=jwt_dependency)
app.include_router(
    trainer_profile_router, prefix="/api/v1", dependencies=jwt_dependency
)
app.include_router(training_plans_router, prefix="/api/v1", dependencies=jwt_dependency)
app.include_router(exercises_router, prefix="/api/v1", dependencies=jwt_dependency)
app.include_router(notifications_router, prefix="/api/v1", dependencies=jwt_dependency)
app.include_router(assessments_router, prefix="/api/v1", dependencies=jwt_dependency)
app.include_router(nutrition_router, prefix="/api/v1", dependencies=jwt_dependency)
app.include_router(checkins_router, prefix="/api/v1", dependencies=jwt_dependency)
app.include_router(client_portal_router, prefix="/api/v1", dependencies=jwt_dependency)
app.include_router(
    client_supplements_router, prefix="/api/v1", dependencies=jwt_dependency
)
