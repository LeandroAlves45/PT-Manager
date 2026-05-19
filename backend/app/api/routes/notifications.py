"""
Rotas para gestão de notificações — envio, listagem e monitorização.

Responsabilidades:
  - POST /dispatch — processa manualmente notificações pendentes
  - GET /pending — lista notificações ainda por enviar
  - GET /stats — estatísticas de notificações
  - DELETE /{id} — cancela notificação pendente
"""

from __future__ import annotations

import logging
from datetime import datetime, timezone
from typing import List, Optional
from fastapi import APIRouter, Depends, HTTPException, Query, status
from pydantic import BaseModel
from sqlmodel import Session, select
from sqlalchemy.exc import SQLAlchemyError

from app.db.database import get_session
from app.db.models.notification import (
    Notification,
    NotificationChannel,
    NotificationStatus,
)
from app.services.email_service import EmailService
from app.utils.db_errors import commit_or_rollback


logger = logging.getLogger(__name__)

router = APIRouter(prefix="/notifications", tags=["Notifications"])


# Schema para notificação pendente
class PendingNotificationRead(BaseModel):
    """
    Schema para leitura de notificação pendente com informações de agendamento
    """

    id: str
    channel: str
    recipient: str
    recipient_type: str
    scheduled_for: str  # ISO format
    is_due: bool
    minutes_until_send: float
    created_at: Optional[str] = None  # ISO format


def _parse_legacy_template_message(message: str) -> dict[str, str]:
    """
    Extrai dados de notificações antigas em formato TEMPLATE_HTML.
    """
    raw = message.replace("TEMPLATE_HTML|", "")
    separator = ";" if ";" in raw else "|"
    data = {}

    for item in raw.split(separator):
        if "=" in item:
            key, value = item.split("=", 1)
            data[key.strip()] = value.strip()

    return data


def _send_email_notification(notification: Notification) -> None:
    """
    Envia uma notificação por email usando o formato atual ou legado.
    """
    recipient = (notification.recipient or "").strip()

    if not recipient:
        raise ValueError("Notificação sem destinatário.")

    if notification.template_data:
        data = notification.template_data
        EmailService.send_session_email(
            to_email=recipient,
            client_name=data["client_name"],
            session_date=data["session_date"],
            session_time=data["session_time"],
            duration_minutes=int(data["duration_minutes"]),
            location=data["location"],
            trainer_logo_url=data.get("trainer_logo_url", ""),
            trainer_name=data.get("trainer_name", "O teu Personal Trainer"),
            app_name=data.get("app_name", "PT Manager"),
        )
        return

    if notification.message and notification.message.startswith("TEMPLATE_HTML|"):
        data = _parse_legacy_template_message(notification.message)
        EmailService.send_session_email(
            to_email=recipient,
            client_name=data.get("client_name", ""),
            session_date=data.get("session_date", ""),
            session_time=data.get("session_time", ""),
            duration_minutes=int(data.get("duration_minutes", "60")),
            location=data.get("location", ""),
            trainer_logo_url=data.get("trainer_logo_url", ""),
            trainer_name=data.get("trainer_name", "O teu Personal Trainer"),
            app_name=data.get("app_name", "PT Manager"),
        )
        return

    EmailService.send_plain_email(
        to_email=recipient,
        subject="Lembrete de Sessão - PT Manager",
        body=notification.message,
    )


@router.post("/dispatch")
async def dispatch_due_notifications(session: Session = Depends(get_session)) -> dict:
    """
    Processa manualmente notificações pendentes.

    O scheduler é o mecanismo principal de envio; este endpoint serve para
    operação/admin quando é necessário disparar o processamento manualmente.
    """

    logger.info("[API DISPATCH] Iniciando processamento manual de notificações")

    now = datetime.now(timezone.utc)

    # Buscar notificações pendentes (scheduled_for <= now)
    stmt = select(Notification).where(
        Notification.status == NotificationStatus.PENDING,
        Notification.scheduled_for <= now,
    )
    notifications_to_send = session.exec(stmt).all()

    logger.info(
        "[API DISPATCH] ✅ Encontradas %d notificações pendentes",
        len(notifications_to_send),
    )

    sent = 0
    failed = 0

    for notification in notifications_to_send:

        try:
            if notification.channel == NotificationChannel.EMAIL:
                logger.info(
                    "[API DISPATCH] ✅ Processando email para %s",
                    notification.recipient,
                )

                _send_email_notification(notification)

                notification.status = NotificationStatus.SENT
                notification.sent_at = now
                notification.error_message = None
                sent += 1

            # ================================================
            # CANAL NÃO SUPORTADO
            # ================================================
            else:
                logger.warning(
                    "[API DISPATCH] ⚠️ Canal %s não suportado. Cancelando notificação %s",
                    notification.channel,
                    notification.id[:8],
                )
                notification.status = NotificationStatus.CANCELLED
                notification.error_message = (
                    f"Canal {notification.channel} não está ativo"
                )
                failed += 1

        except Exception as e:  # pylint: disable=broad-exception-caught
            logger.error(
                "[API DISPATCH] ❌ Erro ao processar notificação %s: %s",
                notification.id[:8],
                str(e),
            )

            notification.status = NotificationStatus.FAILED
            notification.error_message = str(e)[:500]  # Limitar tamanho
            failed += 1

    commit_or_rollback(session)

    result = {
        "due_found": len(notifications_to_send),
        "sent": sent,
        "failed": failed,
        "timestamp": now.isoformat(),
    }

    logger.info(
        "[API DISPATCH] ✅ Concluído - "
        "Encontradas: %d, Enviadas: %d, Falhadas: %d",
        result["due_found"],
        result["sent"],
        result["failed"],
    )

    return result


@router.get("/pending", response_model=List[PendingNotificationRead])
async def list_pending_notifications(
    limit: int = Query(
        default=100, ge=1, le=1000, description="Número máximo de notificações"
    ),
    session: Session = Depends(get_session),
) -> List[PendingNotificationRead]:
    """
    Lista notificações pendentes ordenadas por data de envio.
    Útil para debug e monitorização do sistema de notificações.

    Args:
        limit: Número máximo de notificações a retornar (1-1000)

    Returns:
        Lista de notificações pendentes com tempo restante até envio
    """

    # Buscar notificações pendentes
    stmt = (
        select(Notification)
        .where(Notification.status == NotificationStatus.PENDING)
        .order_by(Notification.scheduled_for.asc())  # pylint: disable=no-member
        .limit(limit)
    )

    try:
        notifications = session.exec(stmt).all()
    except SQLAlchemyError as e:
        raise HTTPException(
            status_code=500, detail="Erro ao buscar notificações pendentes."
        ) from e

    # Calcular informações de tempo
    now = datetime.now(timezone.utc)

    result = []
    for n in notifications:
        # Verificar se notificação já está pronta para envio
        is_due = n.scheduled_for <= now

        # Calcular tempo restante em minutos
        time_diff_seconds = (n.scheduled_for - now).total_seconds()
        time_diff_minutes = time_diff_seconds / 60

        result.append(
            PendingNotificationRead(
                id=n.id,
                channel=n.channel,
                recipient=n.recipient,
                recipient_type=n.recipient_type,
                scheduled_for=n.scheduled_for.isoformat(),
                is_due=is_due,
                minutes_until_send=(
                    round(time_diff_minutes, 1) if time_diff_minutes > 0 else 0.0
                ),
                created_at=n.created_at.isoformat() if n.created_at else None,
            )
        )

    return result


@router.get("/stats")
async def get_notification_stats(session: Session = Depends(get_session)) -> dict:
    """
    Retorna estatísticas de notificações (total, pendentes, enviadas, falhadas, por canal).
    """

    # Contar por status
    all_notifications = session.exec(select(Notification)).all()

    stats = {
        "total": len(all_notifications),
        "pending": 0,
        "sent": 0,
        "failed": 0,
        "cancelled": 0,
        "by_channel": {
            "email": 0,
        },
    }

    for n in all_notifications:
        # Contar por status
        if n.status == NotificationStatus.PENDING:
            stats["pending"] += 1
        elif n.status == NotificationStatus.SENT:
            stats["sent"] += 1
        elif n.status == NotificationStatus.FAILED:
            stats["failed"] += 1
        elif n.status == NotificationStatus.CANCELLED:
            stats["cancelled"] += 1

        # Contar por canal
        if n.channel == NotificationChannel.EMAIL:
            stats["by_channel"]["email"] += 1

    return stats


@router.delete("/{notification_id}", status_code=status.HTTP_200_OK)
async def delete_notification(
    notification_id: str,
    session: Session = Depends(get_session),
) -> dict:
    """
    Cancela uma notificação pendente (muda status para CANCELLED).
    """

    notification = session.get(Notification, notification_id)

    if not notification:
        raise HTTPException(
            status_code=status.HTTP_404_NOT_FOUND,
            detail="Notificação não encontrada",
        )

    if notification.status != NotificationStatus.PENDING:
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail=f"Notificação já está com status '{notification.status}'",
        )

    notification.status = NotificationStatus.CANCELLED
    session.add(notification)
    commit_or_rollback(session)

    logger.info(
        "[API DISPATCH] ✅ Notificação %s cancelada manualmente",
        notification_id[:8],
    )

    return {
        "message": "Notificação cancelada com sucesso",
        "notification_id": notification_id,
    }
