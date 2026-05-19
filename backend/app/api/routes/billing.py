"""
Router de billing — dashboard de subscrição do trainer.

Endpoints:
    GET  /billing/subscription      — estado actual da subscrição
    POST /billing/checkout          — gera URL do Stripe Checkout (adicionar cartão)
    POST /billing/portal            — gera URL do Billing Portal (gerir subscrição)
"""

import logging
from datetime import datetime, timezone

from fastapi import APIRouter, Depends, HTTPException, Request, status
from sqlmodel import Session

from app.api.schemas.subscription import SubscriptionRead
from app.core.config import settings
from app.core.rate_limit import RateLimitConfig, limiter
from app.core.security import require_trainer
from app.db.database import get_session
from app.db.models.trainer_subscription import SubscriptionTier, TrainerSubscription
from app.services.stripe_service import StripeService
from app.services.subscription_service import TIER_CONFIG, SubscriptionService
from app.utils.db_errors import commit_or_rollback

logger = logging.getLogger(__name__)

router = APIRouter(prefix="/billing", tags=["Billing"])

TIER_LABELS = {
    SubscriptionTier.FREE: "Free",
    SubscriptionTier.STARTER: "Starter",
    SubscriptionTier.PRO: "Pro",
}


def _stripe_is_configured() -> bool:
    return bool(
        settings.stripe_secret_key
        and settings.stripe_price_free
        and settings.stripe_price_starter
        and settings.stripe_price_pro
    )


def _trial_days_for_subscription(subscription: TrainerSubscription) -> int:
    """
    Dias de trial no Stripe; mínimo 1 se ainda houver trial na BD.

    Note: .days trunca horas — se faltam 23 horas, max(1, 0)=1 dia.
    Trial é inclusivo e arredonda para cima.
    """
    if not subscription.trial_end:
        return 1

    trial_end = subscription.trial_end
    if trial_end.tzinfo is None:
        trial_end = trial_end.replace(tzinfo=timezone.utc)

    remaining = (trial_end - datetime.now(timezone.utc)).days
    if remaining <= 0:
        raise HTTPException(status_code=400, detail="O trial já expirou.")
    return max(1, remaining)


def _persist_stripe_ids(session: Session, subscription: TrainerSubscription) -> None:
    try:
        session.add(subscription)
        commit_or_rollback(session)
    except Exception as e:
        logger.error("[BILLING] Erro ao atualizar subscrição com IDs Stripe: %s", e)
        raise HTTPException(
            status_code=502,
            detail="Erro ao atualizar subscrição com IDs Stripe.",
        ) from e


def _ensure_stripe_customer_and_subscription(
    session: Session,
    subscription: TrainerSubscription,
    current_user,
) -> None:
    """Garante customer + subscription no Stripe e persiste IDs na BD."""
    if subscription.stripe_customer_id and subscription.stripe_subscription_id:
        return

    if not current_user.email:
        raise HTTPException(status_code=400, detail="Perfil do trainer incompleto.")

    try:
        if not subscription.stripe_customer_id:
            logger.info(
                "[BILLING] Trainer ID %s sem Customer Stripe -> a criar...",
                current_user.id,
            )
            subscription.stripe_customer_id = StripeService.create_customer(
                email=current_user.email,
                full_name=current_user.full_name,
                trainer_user_id=current_user.id,
            )

        if not subscription.stripe_subscription_id:
            logger.info(
                "[BILLING] Trainer ID %s sem Subscription Stripe -> a criar trial...",
                current_user.id,
            )
            trial_days = _trial_days_for_subscription(subscription)
            subscription.stripe_subscription_id = (
                StripeService.create_trial_subscription(
                    stripe_customer_id=subscription.stripe_customer_id,
                    trial_days=trial_days,
                )
            )

        _persist_stripe_ids(session, subscription)
        logger.info(
            "[BILLING] Trainer ID %s sincronizado com Stripe.",
            current_user.id,
        )
    except HTTPException:
        raise
    except Exception as e:
        logger.error(
            "[BILLING] Erro ao sincronizar trainer ID %s com Stripe: %s",
            current_user.id,
            e,
        )
        raise HTTPException(
            status_code=502,
            detail=(
                "Erro ao criar conta no Stripe. "
                "Verifica as credenciais Stripe nas variáveis de ambiente."
            ),
        ) from e


@router.get("/subscription", response_model=SubscriptionRead)
@limiter.limit(RateLimitConfig.GET_SUBSCRIPTION)
async def get_subscription(
    request: Request,  # pylint: disable=unused-argument
    session: Session = Depends(get_session),
    current_user=Depends(require_trainer),
) -> SubscriptionRead:
    """Estado actual da subscrição do trainer."""
    if not current_user.email or not current_user.id:
        raise HTTPException(status_code=400, detail="Perfil do trainer incompleto.")

    subscription = SubscriptionService.get_subscription(session, current_user.id)
    if not subscription:
        raise HTTPException(status_code=404, detail="Subscrição não encontrada.")

    if subscription.tier not in TIER_CONFIG:
        logger.error(
            "Invalid tier %s for trainer %s", subscription.tier, current_user.id
        )
        raise HTTPException(status_code=500, detail="Configuração de tier inválida.")

    tier_config = TIER_CONFIG[subscription.tier]
    can_add, upgrade_msg = SubscriptionService.can_add_client(subscription)

    return SubscriptionRead(
        status=subscription.status,
        tier=subscription.tier,
        tier_label=TIER_LABELS.get(subscription.tier, subscription.tier),
        monthly_eur=tier_config["monthly_eur"],
        max_clients=tier_config["max_clients"],
        active_clients_count=subscription.active_clients_count,
        trial_end=subscription.trial_end,
        current_period_end=subscription.current_period_end,
        can_add_client=can_add,
        upgrade_message=upgrade_msg,
    )


@router.post("/checkout", status_code=status.HTTP_200_OK)
@limiter.limit(RateLimitConfig.CREATE_CHECKOUT)
async def create_checkout_session(
    request: Request,  # pylint: disable=unused-argument
    session: Session = Depends(get_session),
    current_user=Depends(require_trainer),
) -> dict:
    """Gera URL do Stripe Checkout para configurar método de pagamento."""
    subscription = SubscriptionService.get_subscription(session, current_user.id)
    if not subscription:
        raise HTTPException(status_code=404, detail="Subscrição não encontrada.")

    if not _stripe_is_configured():
        raise HTTPException(
            status_code=400,
            detail=(
                "Stripe não está configurado neste ambiente. "
                "Configura STRIPE_SECRET_KEY, STRIPE_PRICE_FREE, "
                "STRIPE_PRICE_STARTER e STRIPE_PRICE_PRO."
            ),
        )

    _ensure_stripe_customer_and_subscription(session, subscription, current_user)

    if not subscription.stripe_customer_id or not subscription.stripe_subscription_id:
        raise HTTPException(
            status_code=400,
            detail="Conta Stripe incompleta. Tenta novamente ou contacta suporte.",
        )

    try:
        checkout_url = StripeService.create_checkout_session(
            stripe_customer_id=subscription.stripe_customer_id,
            stripe_subscription_id=subscription.stripe_subscription_id,
            success_url=settings.stripe_success_url,
            cancel_url=settings.stripe_cancel_url,
        )
    except Exception as e:
        raise HTTPException(
            status_code=status.HTTP_502_BAD_GATEWAY,
            detail="Erro ao criar sessão de checkout.",
        ) from e

    return {"checkout_url": checkout_url}


@router.post("/portal", status_code=status.HTTP_200_OK)
@limiter.limit(RateLimitConfig.CREATE_BILLING_PORTAL)
async def create_billing_portal(
    request: Request,  # pylint: disable=unused-argument
    session: Session = Depends(get_session),
    current_user=Depends(require_trainer),
) -> dict:
    """Gera URL do Stripe Billing Portal."""
    subscription = SubscriptionService.get_subscription(session, current_user.id)

    if not subscription:
        raise HTTPException(
            status_code=status.HTTP_404_NOT_FOUND,
            detail="Subscrição não encontrada.",
        )

    if not _stripe_is_configured():
        raise HTTPException(
            status_code=400,
            detail=("Stripe não está configurado neste ambiente. " "Contacta suporte."),
        )

    _ensure_stripe_customer_and_subscription(session, subscription, current_user)

    if not subscription.stripe_customer_id:
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail=(
                "Não foi possível criar conta Stripe. "
                "Tenta novamente ou contacta suporte."
            ),
        )

    try:
        portal_url = StripeService.create_billing_portal_session(
            stripe_customer_id=subscription.stripe_customer_id,
            return_url=settings.stripe_portal_url,
        )
    except Exception as e:
        raise HTTPException(
            status_code=status.HTTP_502_BAD_GATEWAY,
            detail="Erro ao criar sessão do portal de faturação.",
        ) from e

    return {"portal_url": portal_url}
