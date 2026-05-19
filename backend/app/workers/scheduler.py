"""
Scheduler de notificacoes — APScheduler em background.

Dois jobs:
  1. dispatch_job (cada 60s)  — processa notificacoes pendentes e envia emails
  2. token_cleanup_job (cada 60min) — remove tokens JWT expirados da active_tokens

O dispatch_job usa template_data JSONB para montar os emails HTML.
Mantem fallback para o formato pipe-delimitado antigo (TEMPLATE_HTML|...)
para notificacoes criadas antes da migration 009 nao se perderem.

Arquitectura de isolamento de erros:
    Cada notificacao e processada dentro de um try/except independente.
    Uma falha numa notificacao nao aborta as restantes — o job continua.
    A notificacao falhada e marcada como FAILED com a mensagem de erro registada.
"""

import logging
from datetime import datetime, timedelta, timezone

from apscheduler.schedulers.background import BackgroundScheduler
from apscheduler.triggers.interval import IntervalTrigger
from sqlmodel import select

from app.db.models.active_token import ActiveToken
from app.db.models.notification import NotificationChannel, NotificationStatus
from app.db.database import get_session
from app.services.email_service import EmailService
from app.services.notification_service import NotificationService
from app.utils.time import utc_now_datetime
from app.utils.error_tracker import ErrorTracker


# Número máximo de tentativas de envio antes de marcar como FAILED
# 3 retries = 4 tentativas no total (1 original + 3 com backoff)
MAX_RETRIES = 3

logger = logging.getLogger(__name__)

# Instância global do scheduler — iniciada uma vez no startup da aplicação
scheduler = BackgroundScheduler()


def dispatch_client_email(notification) -> None:
    """
    Envia email HTML para o cliente.

    Lê os dados de template_data (JSONB) em vez de parsear o
    formato pipe-delimitado fragil. Mantem fallback para notificacoes
    antigas que ainda usam o formato legado.

    O campo "type" dentro de template_data indica qual template usar.
    Actualmente so existe "client_session_reminder".
    """

    # template_data JSONB
    if notification.template_data:
        data = notification.template_data

        # Valida que os campos necessários estão presentes
        required = {
            "client_name",
            "session_date",
            "session_time",
            "duration_minutes",
            "location",
        }
        missing = required - set(data.keys())
        if missing:
            raise ValueError(
                f"Dados de template incompletos: faltam {', '.join(missing)}"
            )

        EmailService.send_session_email(
            to_email=notification.recipient,
            client_name=data["client_name"],
            session_date=data["session_date"],
            session_time=data["session_time"],
            duration_minutes=int(data["duration_minutes"]),
            location=data["location"],
            trainer_logo_url=data.get("trainer_logo_url", ""),
        )
        logger.info(
            "[EMAIL] ✅ Email HTML enviado para %s usando template_data",
            notification.recipient,
        )
        return

    # Formato pipe-delimitado
    # Processa notificações antigas que ainda estão na BD com o formato antigo
    if notification.message and notification.message.startswith("TEMPLATE_HTML|"):
        logger.warning(
            "[EMAIL] ⚠️ Notificação %s usando formato legado. "
            "Recomenda-se atualizar para template_data JSONB.",
            notification.id[:8],
        )
        raw = notification.message.replace("TEMPLATE_HTML|", "")
        parts = {}

        for item in raw.split(";"):
            if "=" in item:
                key, value = item.partition("=")[::2]
                parts[key.strip()] = value.strip()

        EmailService.send_session_email(
            to_email=notification.recipient,
            client_name=parts.get("client_name", ""),
            session_date=parts.get("session_date", ""),
            session_time=parts.get("session_time", ""),
            duration_minutes=int(parts.get("duration_minutes", "60")),
            location=parts.get("location", ""),
            trainer_logo_url=parts.get("trainer_logo_url", ""),
        )
        logger.info("[EMAIL] ✅ Email HTML enviado para %s.", notification.recipient)
        return

    raise ValueError("Notificação de email sem template_data ou formato válido")


def dispatch_job():
    """
    Job que processa e envia notificacoes de EMAIL pendentes.

    Executa a cada 60 segundos.

    Logica de decisao por notificacao:
    - Se recipient_type == CLIENT e template_data presente → email HTML (_dispatch_client_email)
    - Se recipient_type == CLIENT e message comeca com TEMPLATE_HTML| → email HTML legado
    - Caso contrario → email de texto simples (emails do Personal Trainer)
    """
    logger.info("[SCHEDULER] 🔄 Iniciando dispatch às %s", utc_now_datetime())

    with next(get_session()) as session:
        # Buscar notificações pendentes cuja hora de envio já chegou
        notifications = NotificationService.list_due_notifications(session, limit=100)
        logger.info(
            "[SCHEDULER] 📬 Encontradas %s notificação(ões) para enviar",
            len(notifications),
        )
        for notification in notifications:
            try:
                recipient = (notification.recipient or "").strip()

                # ================================================
                # PROCESSAR EMAIL
                # ================================================
                if notification.channel == NotificationChannel.EMAIL:
                    logger.info("[EMAIL] 📧 Processando para %s", recipient)

                    # Determina se é email HTML (cliente) ou email simples (Personal Trainer)
                    is_html_email = notification.template_data is not None or (
                        notification.message
                        and notification.message.startswith("TEMPLATE_HTML|")
                    )

                    if is_html_email:
                        # Email HTML para o cliente
                        dispatch_client_email(notification)

                    else:
                        # ========================================
                        # EMAIL SIMPLES PARA TREINADOR
                        # ========================================

                        EmailService.send_plain_email(
                            to_email=recipient,
                            subject="Lembrete de Sessão - PT Manager",
                            body=notification.message,
                        )
                        logger.info(
                            "[EMAIL] ✅ Email simples enviado para %s", recipient
                        )

                        # Marcar como enviada com timestamp UTC
                        notification.status = NotificationStatus.SENT
                        notification.sent_at = utc_now_datetime()
                        session.add(notification)

                        logger.info(
                            "[SUCCESS] ✅ Notificação %s processada com sucesso",
                            notification.id[:8],
                        )

                # ================================================
                # CANAL NÃO SUPORTADO
                # Cancela a notificação em vez de a deixar eternamente pending
                # ================================================
                else:
                    logger.warning(
                        "[SCHEDULER] ⚠️  Canal %s não suportado. "
                        "Cancelando notificação %s",
                        notification.channel,
                        notification.id[:8],
                    )
                    notification.status = NotificationStatus.CANCELLED
                    notification.error_message = (
                        f"Canal {notification.channel} não está ativo"
                    )
                    session.add(notification)

            except Exception as e:  # pylint: disable=broad-exception-caught
                error_log = ErrorTracker.log_error(
                    notification_id=notification.id[:8],
                    exception=e,
                    retry_count=notification.retry_count,
                    max_retries=MAX_RETRIES,
                )

                logger.error(
                    "[FAILED] ❌ %s",
                    error_log.model_dump_json(),
                )
                logger.exception("Stacktrace completo:")

                if notification.retry_count < MAX_RETRIES:
                    # Ainda tem tentativas disponíveis (agendar próxima tentativa com backoff)
                    notification.retry_count += 1

                    # Backoff exponencial: 5 * 2^(retry_count - 1) minutos
                    delay_minutes = 5 * (2 ** (notification.retry_count - 1))
                    scheduled_time = utc_now_datetime() + timedelta(
                        minutes=delay_minutes
                    )
                    notification.scheduled_for = scheduled_time

                    # Mantém PENDING para que list_due_notifications() a apanhe quando chegar a hora
                    notification.status = NotificationStatus.PENDING
                    error_str = str(e)[:400]
                    notification.error_message = (
                        f"[Tentativa {notification.retry_count}] {error_str}"
                    )

                    logger.warning(
                        "[RETRY] 🔄 Notificação %s reagendada para "
                        "+%s min (tentativa %s/%s)",
                        notification.id[:8],
                        delay_minutes,
                        notification.retry_count,
                        MAX_RETRIES,
                    )
                else:
                    # Esgotou todas as tentativas, marcar como FAILED
                    notification.status = NotificationStatus.FAILED
                    notification.error_message = f"[FINAL] {str(e)[:400]}"

                    logger.error(
                        "[FAILED] 💀 Notificação %s marcada FAILED definitivo "
                        "após %s tentativas. Email: %s",
                        notification.id[:8],
                        MAX_RETRIES + 1,
                        notification.recipient,
                    )

                session.add(
                    notification
                )  # Salvar o status FAILED e a mensagem de erro na BD

    session.commit()
    logger.info(
        "[SCHEDULER] ✅ Dispatch concluído. %s processada(s).", len(notifications)
    )


# ---------------------------------------------------------------
# Limpeza periódica de tokens expirados — para manter a tabela active_tokens enxuta
# ---------------------------------------------------------------


def token_cleanup_job():
    """
    Exclui as linhas expiradas de active_tokens.

    Esta é uma limpeza adicional — os tokens já são invalidados
    ao fazer logout, excluindo a linha. Esta tarefa captura tokens que expiraram
    naturalmente (o usuário nunca fez logout explicitamente) para manter a tabela enxuta.
    """

    logger.info("[SCHEDULER] Token cleanup started")

    with next(get_session()) as session:
        now = datetime.now(timezone.utc)
        try:
            # Apagar tokens expirados
            expired = session.exec(
                select(ActiveToken).where(ActiveToken.expires_at < now)
            ).all()

            count = len(expired)
            for token in expired:
                session.delete(token)

            session.commit()
            logger.info(
                "[SCHEDULER] Limpeza de tokens concluída — %s token(s) expirado(s) removido(s).",
                count,
            )
        except Exception as e:  # pylint: disable=broad-exception-caught
            session.rollback()
            logger.error("[SCHEDULER] Falha na limpeza de tokens: %s", str(e)[:400])


# ---------------------------------------------------------------
# Inicia o scheduler APScheduler em background.
# ---------------------------------------------------------------


def start_scheduler():
    """
    Inicia o scheduler APScheduler em background.

    Chamado no hook on_startup do FastAPI (main.py).
    max_instances=1 garante que o job não se sobrepõe a si mesmo
    se a execução anterior demorar mais de 60 segundos.
    """
    logger.info("[SCHEDULER] Iniciando scheduler de notificações")

    scheduler.add_job(
        dispatch_job,
        IntervalTrigger(seconds=60),
        id="notification_dispatch",
        replace_existing=True,
        max_instances=1,
    )

    scheduler.add_job(
        token_cleanup_job,
        IntervalTrigger(minutes=60),
        id="token_cleanup",
        replace_existing=True,
        max_instances=1,
    )

    scheduler.start()
    logger.info("[SCHEDULER] Scheduler iniciado com sucesso")


def shutdown_scheduler():
    """
    Desliga o scheduler graciosamente.

    Chamado no hook on_shutdown do FastAPI (main.py) para garantir
    que o job não fica a correr após o processo terminar.
    """

    logger.info("[SCHEDULER] Desligando scheduler de notificações")
    scheduler.shutdown(wait=False)
    logger.info("[SCHEDULER] Scheduler desligado")
