"""
Serviço de notificações via EMAIL.
 
Sistema simplificado usando apenas email com templates HTML profissionais.
 
Fluxo de uma notificação:
  1. sessions.py chama create_reminder_for_session() ao criar/editar uma sessão
  2. Este serviço cria registos Notification na BD (status=PENDING)
  3. O scheduler (scheduler.py) executa dispatch_job a cada 60s
  4. O dispatch_job chama list_due_notifications() e processa os pendentes
"""

from __future__ import annotations
from datetime import time, timedelta, datetime, timezone
import logging
from typing import Optional
from sqlmodel import Session, select
import pytz

from app.core.config import settings
from app.db.models.notification import Notification , NotificationChannel, RecipientType, NotificationStatus
from app.db.models.session import TrainingSession
from app.db.models.client import Client
from app.db.models.user import User
from app.db.models.trainer_settings import TrainerSettings
from app.utils.time import utc_now_datetime

logger = logging.getLogger(__name__)

class NotificationService:
    """
    Serviço de notificações via EMAIL.
    Todos os métodos são estáticos - não há estado por instância.
    """

    # ------------------------------
    # Cálculo do momento do envio
    # ------------------------------

    @staticmethod
    def _compute_reminder_datetime_utc(training_session: TrainingSession) -> datetime:
        """
        Calcula o datetime UTC para enviar o lembrete.
 
        MODO PRODUÇÃO: 1 dia antes às 20h (horário local de Lisboa)
        MODO TESTE:    X minutos após o agendamento (configurável via .env)
 
        Args:
            training_session: A sessão de treino para a qual criar o lembrete
 
        Returns:
            datetime: Momento em UTC quando o lembrete deve ser enviado
        """
        
        # Verificar se está em modo de teste (via variável de ambiente)
        test_mode = getattr(settings, 'notification_test_mode', False)
        test_minutes = getattr(settings, 'notification_test_minutes', 2)
        
        if test_mode:
            # ====================================
            # MODO TESTE: Enviar em X minutos
            # ====================================
            scheduled_for_utc = datetime.now(timezone.utc) + timedelta(minutes=test_minutes)
            
            logger.info(
                f"[NOTIFICAÇÃO TEST MODE] Sessão {training_session.id[:8]} - "
                f"Notificação agendada para {scheduled_for_utc.strftime('%H:%M:%S')} UTC "
                f"(daqui a {test_minutes} minutos)"
            )
            
            return scheduled_for_utc
        
        else:
            # ====================================
            # MODO PRODUÇÃO: 1 dia antes às 20h
            # ====================================
            
            # Extrair apenas a data do starts_at (que agora é datetime)
            session_date = training_session.starts_at.date()
            
            # Calcular a data do lembrete (1 dia antes da sessão)
            reminder_date = session_date - timedelta(days=1)
            
            # Criar datetime às 20h no horário local (Europe/Lisbon)
            local_tz = pytz.timezone(settings.timezone)
            remind_local_dt = local_tz.localize(
                datetime.combine(reminder_date, time(20, 0))
            )
            
            # Converter para UTC para armazenamento na base de dados
            scheduled_for_utc = remind_local_dt.astimezone(pytz.UTC)
            
            logger.info(
                f"[NOTIFICAÇÃO PRODUÇÃO] Sessão {training_session.id[:8]} - "
                f"Notificação agendada para {scheduled_for_utc.strftime('%Y-%m-%d %H:%M:%S')} UTC "
                f"(1 dia antes às 20h local)"
            )
            
            return scheduled_for_utc
        
    # ------------------------------
    # Criação de lembretes para uma sessão
    # ------------------------------
    
    @staticmethod
    def create_reminder_for_session(
        db: Session, training_session: TrainingSession
    ) -> list[Notification]:
        """
        Cria notificações de lembrete para uma sessão agendada.
 
        Cria até dois registos na tabela notifications:
          - Um email HTML para o cliente (se tiver email registado)
          - Um email de texto para o Personal Trainer (lido de trainer.email na BD)
 
        Nota: se scheduled_for já passou, não cria nenhum (evita spam imediato).
 
        Returns:
            Lista das notificações criadas (pode ser vazia).
        """

        now_utc = utc_now_datetime()
        scheduled_for = NotificationService._compute_reminder_datetime_utc(training_session)

        # Verificar se já passou do horário
        if scheduled_for <= now_utc:
            logger.warning(
                f"[NOTIFICAÇÃO] Sessão {training_session.id[:8]} agendada tarde demais. "
                f"Notificação NÃO criada para evitar spam."
            )
            return []

        # Carregar dados do cliente e do PT
        client = db.get(Client, training_session.client_id)
        if not client:
            raise ValueError("Cliente não encontrado para a sessão.")
        
        trainer = db.get(User, training_session.owner_trainer_id) if training_session.owner_trainer_id else None

        # Carrega TrainerSettings para obter o logo_url (se existir) e app_name
        trainer_settings: Optional[TrainerSettings] = None
        if trainer:
            trainer_settings = db.exec(
                select(TrainerSettings).where(
                    TrainerSettings.trainer_user_id == trainer.id
                )
            ).first()

        # Construir valores white-label com fallbacks robustos para PT
        trainer_name = trainer.full_name if trainer else "O teu Personal Trainer"
        app_name = (
            (trainer_settings.app_name or "PT Manager")
            if trainer_settings
            else "PT Manager"
        )

        trainer_logo_url = (trainer.logo_url or "") if trainer else ""

        notifications: list[Notification] = []
        session_date = training_session.starts_at.strftime("%d/%m/%Y")
        session_time = training_session.starts_at.strftime("%H:%M")

        # ------------------------------
        # Email HTML para o cliente
        # ------------------------------

        if client.email:

            client_template_data = {
                "type": "client_session_reminder",
                "client_name": client.full_name,
                "session_date": session_date,
                "session_time": session_time,
                "duration_minutes": training_session.duration_minutes,
                "location": training_session.location or "A definir",
                "trainer_logo_url": trainer_logo_url,
                "trainer_name": trainer_name,
                "app_name": app_name,
            }

            notifications.append(
                Notification(
                    session_id=training_session.id,
                    channel=NotificationChannel.EMAIL,
                    recipient_type=RecipientType.CLIENT,
                    recipient=client.email,
                    message=f"📅 Lembrete: Treino Amanhã - {session_date} às {session_time}",
                    template_data=client_template_data,
                    scheduled_for=scheduled_for,
                    status=NotificationStatus.PENDING
                )
            )

            logger.info(f"[NOTIFICAÇÃO] ✅ Email HTML criado para cliente {client.email}")
        else:
            logger.warning(f"[NOTIFICAÇÃO] ⚠️  Cliente {client.id[:8]} não tem email registado.")
        
        # -------------------------------
        # Email de texto para o Personal Trainer
        # -------------------------------

        if trainer and trainer.email:
            session_datetime_str = training_session.starts_at.strftime("%d/%m/%Y às %H:%M")

            msg_trainer = (
                f"🏋️ Lembrete: Treino Amanhã\n\n"
                f"Cliente: {client.full_name}\n"
                f"Data/Hora: {session_datetime_str}\n"
                f"Duração: {training_session.duration_minutes} minutos\n"
                f"Local: {training_session.location}\n"
            )

            if training_session.notes:
                msg_trainer += f"Notas: {training_session.notes}\n"

            notifications.append(
                Notification(
                    session_id=training_session.id,
                    channel=NotificationChannel.EMAIL,
                    recipient_type=RecipientType.TRAINER,
                    recipient=trainer.email,
                    message=msg_trainer,
                    template_data=None,  # O email do trainer é simples, sem template
                    scheduled_for=scheduled_for,
                    status=NotificationStatus.PENDING
                )
            )

            logger.info(f"[NOTIFICAÇÃO] ✅ Email criado para PT {trainer.email}")
        else:
            if not trainer:
                logger.warning(
                    f"[NOTIFICAÇÃO] ⚠️  Sessão {training_session.id[:8]} sem Personal Trainer associado "
                    f"(owner_trainer_id=None). Email de lembrete ao PT não criado."
                )
            else:
                logger.warning(
                    f"[NOTIFICAÇÃO] ⚠️  Personal Trainer {trainer.id[:8]} não tem email registado na BD. "
                    f"Email de lembrete ao PT não criado."
                )

        #persistir notificações
        for notification in notifications:
            db.add(notification)
        
        db.flush()

        logger.info(
            f"[NOTIFICAÇÃO] 📧 {len(notifications)} email(s) criado(s). "
            f"Envio agendado para: {scheduled_for.strftime('%Y-%m-%d %H:%M:%S')} UTC"
        )
        return notifications
    
    # ------------------------------
    # Cancelamento de lembretes pendentes
    # ------------------------------

    @staticmethod
    def cancel_pending_reminders_for_session(db: Session, session_id: str) -> int:
        """
        Cancela todas as notificações PENDING de uma sessão.
 
        Chamado quando uma sessão é cancelada ou remarcada — evita
        que o cliente receba um lembrete de uma sessão que já não existe.
 
        Returns:
            Número de notificações canceladas.
        """

        statement = db.exec(
            select(Notification)
            .where(Notification.session_id == session_id)
            .where(Notification.status == NotificationStatus.PENDING)
        ).all()
        
        for notification in statement:
            notification.status = NotificationStatus.CANCELLED
            db.add(notification)
        
        db.flush()

        logger.info(
            f"[NOTIFICAÇÃO] ❌ {len(statement)} notificação(ões) cancelada(s) "
            f"para sessão {session_id[:8]}"
        )
        return len(statement)
    
    # ------------------------------
    # Listagem de notificações pendentes para envio
    # ------------------------------

    @staticmethod
    def list_due_notifications(db: Session, *, limit: int = 100) -> list[Notification]:
        """
        Devolve notificações PENDING cujo scheduled_for já passou.
        Chamado pelo scheduler a cada 60 segundos.
        """
        
        now_utc = utc_now_datetime()

        notifications = db.exec(
            select(Notification)
            .where(Notification.status == NotificationStatus.PENDING)
            .where(Notification.scheduled_for <= now_utc)
            .order_by(Notification.scheduled_for.asc())
            .limit(limit)
        ).all()

        if notifications:
            logger.info(
                f"[NOTIFICAÇÃO] 📬 {len(notifications)} email(s) pronto(s) para envio"
            )

        return notifications
