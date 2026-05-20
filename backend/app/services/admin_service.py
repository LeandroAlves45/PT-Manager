"""Serviço de administração da plataforma."""

from typing import Optional
from sqlmodel import Session, select
from sqlalchemy import func

from app.db.models.client import Client
from app.db.models.trainer_subscription import SubscriptionStatus
from app.api.schemas.admin import TrainerSummary, PlatformMetrics
from app.repositories.user_repository import UserRepository
from app.services.subscription_service import SubscriptionService, TIER_CONFIG
from app.utils.db_errors import commit_or_rollback
from app.utils.time import utc_now_datetime


class AdminService:
    """Serviço de administração da plataforma."""

    @staticmethod
    def get_platform_metrics(session: Session) -> PlatformMetrics:
        """Obtém as métricas globais da plataforma."""

        all_trainers = UserRepository.get_all_trainers(session)
        all_subs = SubscriptionService.get_all_subscriptions(session)
        allowed_statuses = {SubscriptionStatus.ACTIVE, SubscriptionStatus.TRIALING}
        total_clients = session.exec(
            select(func.count()).select_from(Client)  # pylint: disable=not-callable
        ).one()

        return PlatformMetrics(
            total_trainers=len(all_trainers),
            active_trainers=sum(
                1 for sub in all_subs if sub.status in allowed_statuses
            ),
            trialing_trainers=sum(
                1 for sub in all_subs if sub.status == SubscriptionStatus.TRIALING
            ),
            total_clients=total_clients,
            estimated_monthly_revenue_eur=sum(
                TIER_CONFIG[sub.tier]["monthly_eur"]
                for sub in all_subs
                if sub.status == SubscriptionStatus.ACTIVE
            ),
        )

    @staticmethod
    def list_trainers(
        session: Session, status_filter: Optional[str] = None
    ) -> list[TrainerSummary]:
        """Lista todos os Personal Trainers com o estado das suas subscrições."""

        trainers = UserRepository.get_all_trainers(session)
        results = []
        for trainer in trainers:
            sub = SubscriptionService.get_subscription(session, trainer.id)

            if status_filter and (not sub or sub.status != status_filter):
                continue

            results.append(
                TrainerSummary(
                    user_id=trainer.id,
                    full_name=trainer.full_name,
                    email=trainer.email,
                    is_active=trainer.is_active,
                    is_exempt_from_billing=getattr(
                        trainer, "is_exempt_from_billing", False
                    ),
                    subscription_status=sub.status if sub else None,
                    subscription_tier=sub.tier if sub else None,
                    active_clients_count=sub.active_clients_count if sub else 0,
                    monthly_eur=TIER_CONFIG[sub.tier]["monthly_eur"] if sub else 0,
                    trial_end=sub.trial_end if sub else None,
                    joined_at=trainer.created_at,
                )
            )
        return results

    @staticmethod
    def suspend_trainer(trainer_id: str, session: Session) -> str:
        """Suspende um Personal Trainer (bloqueia acesso imediatamente)."""

        trainer = UserRepository.get_trainer_or_raise(trainer_id, session)
        trainer.is_active = False
        trainer.updated_at = utc_now_datetime()
        session.add(trainer)
        commit_or_rollback(session)
        return f"Personal Trainer {trainer.full_name} suspenso."

    @staticmethod
    def activate_trainer(trainer_id: str, session: Session) -> str:
        """Reativa um Personal Trainer (desbloqueia acesso imediatamente)."""

        trainer = UserRepository.get_trainer_or_raise(trainer_id, session)
        trainer.is_active = True
        trainer.updated_at = utc_now_datetime()
        session.add(trainer)
        commit_or_rollback(session)
        return f"Personal Trainer {trainer.full_name} reativado."

    @staticmethod
    def grant_exemption(trainer_id: str, session: Session) -> str:
        """Concede isenção permanente de billing a um Personal Trainer (free-forever)."""

        trainer = UserRepository.get_trainer_or_raise(trainer_id, session)
        trainer.is_exempt_from_billing = True
        trainer.updated_at = utc_now_datetime()
        session.add(trainer)
        commit_or_rollback(session)
        return f"Personal Trainer {trainer.full_name} isento de billing."

    @staticmethod
    def revoke_exemption(trainer_id: str, session: Session) -> str:
        """Revoga a isenção de billing de um Personal Trainer (subscrição normal)."""

        trainer = UserRepository.get_trainer_or_raise(trainer_id, session)
        trainer.is_exempt_from_billing = False
        trainer.updated_at = utc_now_datetime()
        session.add(trainer)
        commit_or_rollback(session)
        return f"Personal Trainer {trainer.full_name} não isento de billing."
