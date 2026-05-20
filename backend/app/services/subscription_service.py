"""
Servico de subscricoes � logica de negocio de tiers e controlo de acesso.

Este servico contem a inteligencia do sistema de billing:
    - Calcular em que tier um trainer esta com base no n. de clientes
    - Decidir se um trainer pode adicionar mais clientes
    - Fazer upgrade/downgrade automatico no Stripe quando o n. de clientes muda
    - Verificar se um trainer tem acesso activo a plataforma
"""

from datetime import date
from typing import Optional

from sqlmodel import Session, select
from sqlalchemy import func

from app.db.models.client import Client
from app.db.models.trainer_subscription import (
    SubscriptionStatus,
    SubscriptionTier,
    TrainerSubscription,
)
from app.services.stripe_service import StripeService
from app.core.config import settings
from app.utils.db_errors import commit_or_rollback
from app.utils.time import utc_now_datetime

# --------------------------------------------
# Definicao dos tiers e limites
# --------------------------------------------

TIER_CONFIG = {
    SubscriptionTier.FREE: {
        "max_clients": 5,
        "price_id": settings.stripe_price_free,
        "monthly_eur": 0,
    },
    SubscriptionTier.STARTER: {
        "max_clients": 49,
        "price_id": settings.stripe_price_starter,
        "monthly_eur": 20,
    },
    SubscriptionTier.PRO: {
        "max_clients": None,
        "price_id": settings.stripe_price_pro,
        "monthly_eur": 40,
    },
}


class SubscriptionService:
    """Logica de subscricoes: tiers, limites, acesso e sincronizacao com Stripe."""

    @staticmethod
    def get_tier_for_count(client_count: int) -> str:
        """
        Determina o tier correcto com base no n. de clientes activos.

        Esta e a funcao central da logica de pricing.
        Qualquer alteracao nos limites de tier deve ser feita aqui.

        Exemplos:
            0  clientes -> FREE
            5  clientes -> FREE
            6  clientes -> STARTER
            49 clientes -> STARTER
            50 clientes -> PRO
        """
        if client_count <= 5:
            return SubscriptionTier.FREE
        if client_count <= 49:
            return SubscriptionTier.STARTER
        return SubscriptionTier.PRO

    @staticmethod
    def can_add_client(subscription: Optional[TrainerSubscription]) -> tuple[bool, str]:
        """
        Verifica se o trainer pode adicionar mais um cliente.

        Retorna uma tupla (pode_adicionar, mensagem_de_erro).
        A mensagem so e relevante quando pode_adicionar=False.

        Regras:
            - Trial: pode adicionar ate ao limite do tier FREE (5)
            - Active STARTER: pode ate 49
            - Active PRO: sem limite
            - Trial expirado / cancelado: nao pode adicionar
        """

        if not subscription:
            return False, (
                "Sem subscri��o ativa."
                "Por favor, cria uma subscri��o para poderes adicionar clientes."
            )

        if subscription.status == SubscriptionStatus.TRIAL_EXPIRED:
            return False, (
                "O teu per�odo de trial expirou. "
                "Adiciona um m�todo de pagamento para reativar a "
                "tua subscri��o e continuares a usar a plataforma."
            )

        if subscription.status == SubscriptionStatus.CANCELLED:
            return False, (
                "A tua subscri��o foi cancelada."
                "Adiciona um m�todo de pagamento para reativar a "
                "tua subscri��o e continuares a usar a plataforma."
            )

        if subscription.status not in SubscriptionStatus.ALLOWED:
            return False, (
                "Subscri��o inactiva. "
                "Por favor, adicione um m�todo de pagamento para ativar a sua subscri��o."
            )

        current_tier_config = TIER_CONFIG[subscription.tier]
        max_clients = current_tier_config["max_clients"]

        if max_clients is None:
            return True, ""

        if subscription.active_clients_count >= max_clients:
            next_tier_msg = _get_upgrade_message(subscription.tier)
            return False, (
                f"Limite de clientes atingido para o tier {subscription.tier}. "
                f"Podes adicionar mais clientes fazendo upgrade para o pr�ximo tier. "
                f"{next_tier_msg}"
            )

        if subscription.status == SubscriptionStatus.TRIALING:
            remaining = max_clients - subscription.active_clients_count
            return True, (
                f"Est�s em per�odo de trial."
                f"Podes adicionar mais {remaining} cliente/s durante o trial."
            )

        if subscription.status == SubscriptionStatus.PAST_DUE:
            return True, (
                "A tua subscri��o est� com pagamento em atraso."
                "Por favor, atualize o m�todo de pagamento para reativar a "
                "tua subscri��o e continuares a usar a plataforma."
            )

        return True, ""

    @staticmethod
    def sync_client_count(
        session: Session,
        trainer_user_id: str,
    ) -> Optional[TrainerSubscription]:
        """
        Reconta os clientes activos do trainer e sincroniza o tier no Stripe se necessario.

        Chamado sempre que:
            - Um cliente e criado (count +1)
            - Um cliente e arquivado (count -1)
            - Um cliente e reactivado (count +1)

        Processo:
            1. Faz COUNT(*) dos clientes activos deste trainer
            2. Calcula o tier correcto para esse count
            3. Se o tier mudou -> actualiza no Stripe
            4. Actualiza a linha trainer_subscriptions na BD

        Usar COUNT na BD em vez de incrementar/decrementar o cache
        para garantir consistencia mesmo se houver operacoes concorrentes.
        """
        count = session.exec(
            select(func.count())  # pylint: disable=not-callable
            .select_from(Client)
            .where(
                Client.owner_trainer_id == trainer_user_id,
                Client.archived_at.is_(None),  # pylint: disable=no-member
            )
        ).one()

        subscription = SubscriptionService.get_subscription(session, trainer_user_id)
        if not subscription:
            return None

        new_tier = SubscriptionService.get_tier_for_count(count)
        tier_changed = new_tier != subscription.tier

        if subscription.active_clients_count == count and not tier_changed:
            return subscription

        has_active_stripe_sub = (
            subscription.stripe_subscription_id is not None
            and subscription.status == SubscriptionStatus.ACTIVE
        )

        if tier_changed and has_active_stripe_sub:
            new_price_id = TIER_CONFIG[new_tier]["price_id"]
            try:
                StripeService.update_subscription_price(
                    subscription.stripe_subscription_id, new_price_id
                )
            except Exception:  # pylint: disable=broad-exception-caught
                # Nao bloqueia a operacao se o Stripe falhar � webhook sincroniza depois
                pass

        subscription.active_clients_count = count
        subscription.tier = new_tier
        subscription.updated_at = utc_now_datetime()
        session.add(subscription)
        commit_or_rollback(session)
        session.refresh(subscription)

        return subscription

    @staticmethod
    def has_active_access(subscription: Optional[TrainerSubscription]) -> bool:
        """
        Verifica se o trainer tem acesso activo a plataforma.

        Retorna False se:
            - Nao tem subscri��o
            - Trial expirado
            - Subscri��o cancelada
        """

        if not subscription:
            return False

        if subscription.status not in SubscriptionStatus.ALLOWED:
            return False

        if subscription.status == SubscriptionStatus.TRIALING:
            if subscription.trial_end and date.today() > subscription.trial_end.date():
                return False

        return True

    @staticmethod
    def get_subscription(
        session: Session, trainer_user_id: str
    ) -> Optional[TrainerSubscription]:
        """
        Busca a subscri��o do trainer na BD.

        Retorna None se nao existir.
        """
        return session.exec(
            select(TrainerSubscription).where(
                TrainerSubscription.trainer_user_id == trainer_user_id
            )
        ).first()

    @staticmethod
    def get_all_subscriptions(session: Session) -> list[TrainerSubscription]:
        """Obtem todas as subscricoes de todos os Personal Trainers na BD."""
        return session.exec(select(TrainerSubscription)).all()


# ----------------------------
# Funcao auxiliares privadas
# ----------------------------


def _get_upgrade_message(current_tier: str) -> str:
    """Retorna mensagem de sugestao de upgrade com base no tier actual."""
    if current_tier == SubscriptionTier.FREE:
        return "PRO (40 EUR/mes) para clientes ilimitados."
    if current_tier == SubscriptionTier.STARTER:
        return "PRO (40 EUR/mes) para clientes ilimitados."
    return ""
