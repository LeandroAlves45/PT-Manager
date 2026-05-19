"""
logging.py — configuração centralizada de logging estruturado.

Formato: timestamp | nível | módulo | mensagem
Destinos: console (LOG_LEVEL+) e ficheiro rotativo (DEBUG+)

Convenções de logging no PT Manager:
  logger.info(...)    — operações normais (compra de pack, login, etc.)
  logger.warning(...) — situações inesperadas não fatais (validações falhadas, etc.)
  logger.error(...)   — erros recuperáveis (BD, API externa, validação)
  logger.exception(.) — erros não esperados — inclui stack trace automático

Uso em qualquer módulo:
  import logging
  logger = logging.getLogger(__name__)
  logger.error("[PACK] Erro ao comprar pack para cliente %s: %s", client_id, str(e))

Prefixos recomendados por domínio (facilita grep nos logs do Render):
  [AUTH]               — autenticação, tokens, sessões de utilizador
  [AUDIT]              — ações de gestão de users (criação, atualização, remoção)
  [PACK]               — packs de sessões (compra, consumo, arquivo)
  [DEVICE_HIJACKING]   — ataques de hijacking de dispositivos
  [PACK-TYPE]          — tipos de pack (catálogo)
  [EMAIL]              — envio de emails via Resend
  [SCHEDULER]          — tarefas agendadas (APScheduler)
  [SESSION UPDATE]     — atualização de sessões de treino
  [SESSION MISSED]     — sessões falhadas/ausência
  [SESSION CANCELLED]  — cancelamento de sessões
  [NOTIFICACAO]        — serviço de notificações
  [API DISPATCH]       — dispatch de notificações via API
  [DB]                 — erros de base de dados (IntegrityError, SQLAlchemyError)
  [SEED]               — seeds de dados (catálogo, superuser, demo)
  [UPLOAD]             — upload de ficheiros
  [SENTRY]             — inicialização do Sentry
  [BILLING]            — pagamentos e subscrições
  [NUTRITION]          — planos alimentares

Variáveis de ambiente:
  LOG_LEVEL — nível de logging no console (DEBUG, INFO, WARNING, ERROR). Padrão: INFO
"""

import logging
import sys
import os
from logging.handlers import RotatingFileHandler


def setup_logging():
    """
    Configura logging estruturado para a aplicação.

    Idempotente — seguro chamar múltiplas vezes.

    Console: LOG_LEVEL (padrão INFO) e acima — visível nos logs do Render
    Ficheiro: DEBUG e acima — rotativo 10MB, 5 backups
              (apenas em desenvolvimento — Render não tem disco persistente)

    Variável de ambiente:
      LOG_LEVEL — nível no console (DEBUG, INFO, WARNING, ERROR). Padrão: INFO
    """

    # Guarda de idempotência — evita duplicação de handlers em testes
    root_logger = logging.getLogger()
    if root_logger.handlers:
        return

    log_format = "%(asctime)s | %(levelname)-8s | %(name)s | %(message)s"
    date_format = "%Y-%m-%d %H:%M:%S"
    formatter = logging.Formatter(log_format, date_format)

    # Handler de console — sempre ativo (Render captura stdout)
    console_handler = logging.StreamHandler(sys.stdout)

    # LOG_LEVEL configurável via variável de ambiente
    log_level_name = os.environ.get("LOG_LEVEL", "INFO").upper()
    log_level = getattr(logging, log_level_name, logging.INFO)
    console_handler.setLevel(log_level)

    console_handler.setFormatter(formatter)

    root_logger.setLevel(logging.DEBUG)
    root_logger.addHandler(console_handler)

    # Handler de ficheiro — apenas se conseguir criar o diretório logs/
    # Em produção (Render) não há disco persistente, por isso é opcional.
    # Em desenvolvimento, cria o diretório automaticamente.
    logs_dir = "logs"
    try:
        os.makedirs(logs_dir, exist_ok=True)

        file_handler = RotatingFileHandler(
            os.path.join(logs_dir, "app.log"),
            maxBytes=10 * 1024 * 1024,  # 10MB por ficheiro
            backupCount=5,
            encoding="utf-8",
        )
        file_handler.setLevel(logging.DEBUG)
        file_handler.setFormatter(formatter)
        root_logger.addHandler(file_handler)

    except OSError:
        # Sem permissão para criar o diretório — apenas console
        pass

    # Silenciar bibliotecas externas — demasiado verbosas
    logging.getLogger("uvicorn.access").setLevel(logging.WARNING)
    logging.getLogger("sqlalchemy.engine").setLevel(logging.WARNING)
    logging.getLogger("apscheduler").setLevel(logging.INFO)
    logging.getLogger("httpx").setLevel(logging.WARNING)
    logging.getLogger("stripe").setLevel(logging.WARNING)
