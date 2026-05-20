"""
Configuração centralizada de rate limiting.

Todos os routers importam daqui:
    from app.core.rate_limit import limiter, RateLimitConfig

Uso em decoradores:
    @router.post("/login")
    @limiter.limit(RateLimitConfig.LOGIN)
    async def login(...):
        ...
"""

from slowapi import Limiter
from slowapi.util import get_remote_address
from fastapi import Request


def build_email_ip_key(request: Request) -> str:
    """
    Combinação de IP + email para rate limiting.
    Protege contra brute-force em múltiplas contas via múltiplos IPs.

    O email é extraído pela middleware RateLimitEmailMiddleware.
    Fallback para IP se o email não estiver disponível.
    """
    client = request.client
    ip = getattr(client, "host", "unknown") if client else "unknown"
    email = request.scope.get("email", "")
    if email:
        return f"{ip}:{email}"
    return get_remote_address(request)


# ============================================================
# Limiter Global
# ============================================================

limiter = Limiter(key_func=build_email_ip_key)
"""
Instância única usada por todos os routers (app.state.limiter).

- Login: middleware define email no scope -> chave IP:email
- Demais rotas: fallback para IP (get_remote_address)
"""


# ============================================================
# Rate Limit Configuration
# ============================================================
# Estratégia: Risco × Frequência Legítima
#
# - Endpoints críticos (auth): limite apertado (5/min)
#   Razão: Brute-force de credentials é comum; usuários legítimos
#   fazem login 1-2× por dia, portanto restrição não afeta UX.
#
# - Endpoints operacionais (refresh, CRUD): limite médio (10-20/min)
#   Razão: Operações legítimas mais frequentes, mas ainda protegidas.
#
# - Endpoints de leitura (GET): limite generoso (30/min)
#   Razão: Sem risco de escrita; apenas protege contra crawlers/bots.
#
# - Endpoints públicos (signup): limite médio (5-10/min)
#   Razão: Previne spam de contas; usuário legítimo faz signup 1×.
#
# Em produção, estes valores devem ser ajustados com base em métricas
# reais de uso (ex: P95 de requisições por utilizador/minuto).


class RateLimitConfig:
    """Limites de rate limiting para todos os endpoints."""

    # ============================================================
    # Authentication (auth.py)
    # ============================================================

    LOGIN = "5/15minutes"
    """
    POST /auth/login — 5 tentativas por 15 minutos (por IP+email).
    Protege contra brute-force de email + password.
    Utilizador legítimo: 1-2 logins/dia (bem abaixo do limite).
    """

    CHANGE_PASSWORD = "5/minute"
    """
    POST /auth/users/me/change-password — 5 tentativas/minuto.
    Protege contra brute-force de password (old password).
    Utilizador legítimo: altera password ~1-2 vezes/ano (bem abaixo).
    """

    REFRESH = "10/minute"
    """
    POST /auth/refresh — 10 tentativas/minuto.
    Menos restritivo que login (refresh é transparente, não interactivo).
    Utilizador legítimo: refresh a cada 60-120 min (é normal 10-20/dia).
    """

    CREATE_USER = "20/minute"
    """
    POST /auth/users — 20 tentativas/minuto.
    Requer role=trainer; operação rara (criar utilizador 1-2×/semana).
    Limite médio protege contra spam mas não prejudica UX.
    """

    UPDATE_USER = "20/minute"
    """
    PATCH /auth/users/{id} — 20 tentativas/minuto.
    Operação normal (editar perfil); utilizador legítimo edita 1-2×/semana.
    Limite médio: protege contra spam, não prejudica UX.
    """

    LIST_USERS = "30/minute"
    """
    GET /auth/users — 30 tentativas/minuto.
    Sem risco de escrita; apenas protege contra crawlers.
    Utilizador legítimo: carrega lista 1-2×/sessão (bem abaixo).
    """

    LOGOUT = "30/minute"
    """
    POST /auth/logout — 30 tentativas/minuto.
    Operação muito rara (1 logout/dia); pode ser generoso.
    Limite alto: protege contra spam, não prejudica UX.
    """

    # ============================================================
    # Signup (signup.py)
    # ============================================================

    SIGNUP = "5/minute"
    """
    POST /signup/trainer — 5 tentativas/minuto.
    Endpoint público; protege contra spam de contas.
    Utilizador legítimo: signup 1× na vida (bem abaixo do limite).
    """


    VERIFY_EMAIL = "10/minute"
    """
    POST /auth/verify-email — 10 tentativas/minuto por IP.
    Protege contra brute-force de tokens de verificação.
    Utilizador legítimo: verifica 1× após signup.
    """

    RESEND_VERIFICATION = "5/hour"
    """
    POST /auth/resend-verification-email — 5 pedidos/hora por IP+email.
    Protege contra spam de emails e enumeração.
    Utilizador legítimo: reenvia 1-2× no máximo.
    """

    # ============================================================
    # Invite (invite.py)
    # ============================================================

    SEND_INVITE = "10/minute"
    """
    POST /invite/send — 10 tentativas/minuto.
    Operação comum mas com autenticação; protege contra spam.
    Utilizador legítimo: envia 1-5 convites/semana (bem abaixo).
    """

    ACCEPT_INVITE = "20/minute"
    """
    POST /invite/accept — 20 tentativas/minuto.
    Operação legítima, sem muita restrição.
    Utilizador legítimo: aceita 1-2 convites/mês (bem abaixo).
    """

    INVITE_GENERATE_LIMIT = "10/minute"
    """
    POST /clients/{client_id}/generate-invite — 10 tentativas/minuto.
    Operação legítima mas com autenticação; protege contra spam.
    Personal Trainer legítimo: gera 1-5 convites/semana (bem abaixo).
    """

    INVITE_VALIDATE_LIMIT = "10/minute"
    """
    GET /invite/validate/{token} — 10 tentativas/minuto.
    Operação pública, sem muita restrição.
    Cliente legítimo: valida 1-2 convites/mês (bem abaixo).
    """

    INVITE_SET_PASSWORD_LIMIT = "5/minute"
    """
    POST /invite/set-password/{token} — 5 tentativas/minuto.
    Operação legítima; protege contra brute-force de password.
    Cliente legítimo: define password 1-2×/semana (bem abaixo do limite).
    """

    # ============================================================
    # Billing (billing.py)
    # ============================================================

    UPDATE_SUBSCRIPTION = "10/minute"
    """
    PATCH /billing/subscription — 10 tentativas/minuto.
    Operação sensível (mudança de plano); protege contra abuso.
    Utilizador legítimo: muda plano 1-2×/ano (bem abaixo).
    """

    CREATE_CHECKOUT = "5/minute"
    """
    POST /billing/checkout-session — 5 tentativas/minuto.
    Operação sensível (pagamento); protege contra spam.
    Utilizador legítimo: cria checkout 1-2×/ano (bem abaixo).
    """

    GET_SUBSCRIPTION = "30/minute"
    """
    GET /billing/subscription — 30 tentativas/minuto.
    Leitura apenas; utilizador legítimo consulta subscrição 1-2×/sessão.
    """

    CREATE_BILLING_PORTAL = "10/minute"
    """
    POST /billing/portal — 10 tentativas/minuto.
    Operação legítima; gerir subscrição 1-2×/ano.
    """
