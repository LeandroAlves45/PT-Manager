"""Webhook do Stripe para processar eventos de subscrição e pagamento.

Handlers para eventos:
- customer.subscription.updated: Status change na subscrição
- customer.subscription.deleted: Subscrição cancelada
- invoice.payment_succeeded: Pagamento bem-sucedido
- invoice.payment_failed: Falha de pagamento
- customer.subscription.trial_will_end: Aviso 3 dias antes do fim do trial
"""

import logging
from datetime import datetime, timezone
from typing import Any, Optional

from fastapi import APIRouter, Request, Depends, HTTPException, Header
from sqlalchemy.exc import IntegrityError
from sqlmodel import Session, select
import stripe

from app.db.database import get_session
from app.core.config import settings
from app.db.models.trainer_subscription import TrainerSubscription, SubscriptionStatus
from app.db.models.user import User
from app.services.email_service import EmailService
from app.db.models.processed_stripe_event import ProcessedStripeEvent

logger = logging.getLogger(__name__)
router = APIRouter(prefix="/stripe-webhook", tags=["Stripe Webhooks"])


def _stripe_resource_id(value: Any) -> Optional[str]:
    """Extrai ID string de campos Stripe (string ou objeto expandido)."""
    if value is None:
        return None
    if isinstance(value, str):
        return value
    if isinstance(value, dict):
        rid = value.get("id")
        return str(rid) if rid is not None else None
    return str(value)


def _normalize_stripe_status(stripe_status: Optional[str]) -> Optional[str]:
    """Na app usamos sempre ``cancelled``; o Stripe envia por vezes ``canceled``."""
    if stripe_status == "canceled":
        return "cancelled"
    return stripe_status


@router.post("/webhook", status_code=200)
async def stripe_webhook(
    request: Request,
    stripe_signature: str = Header(..., alias="Stripe-Signature"),
    session: Session = Depends(get_session),
):
    """Processa eventos webhook do Stripe com validação de assinatura.

    Assinatura inválida → 400. Sucesso → 200. Falha no handler → rollback + 500
    (Stripe reenvia; claim revertido, evento não fica em processed_stripe_events).
    """
    payload = await request.body()

    try:
        event = stripe.Webhook.construct_event(
            payload=payload,
            sig_header=stripe_signature,
            secret=settings.stripe_webhook_secret,
        )
    except stripe.error.SignatureVerificationError as e:
        logger.warning("[BILLING] Assinatura do webhook é inválida: %s", str(e))
        raise HTTPException(
            status_code=400, detail="Sem autorização"
        ) from e
    except Exception as e:
        logger.error("[BILLING] Erro ao processar o webhook: %s", str(e))
        raise HTTPException(
            status_code=400, detail="Webhook inválido"
        ) from e

    handlers = {
        "customer.subscription.updated": _handle_subscription_updated,
        "customer.subscription.deleted": _handle_subscription_deleted,
        "invoice.payment_succeeded": _handle_payment_succeeded,
        "invoice.payment_failed": _handle_payment_failed,
        "customer.subscription.trial_will_end": _handle_trial_will_end,
    }

    handler = handlers.get(event["type"])
    if not handler:
        return {"received": True}

    event_id = event["id"]
    existing = session.exec(
        select(ProcessedStripeEvent).where(ProcessedStripeEvent.event_id == event_id)
    ).first()
    if existing:
        logger.info("[BILLING] Duplicate event %s, skipping", event_id)
        return {"received": True}

    # Claim: INSERT + flush com PK evita processar o mesmo event_id em paralelo.
    session.add(
        ProcessedStripeEvent(event_id=event_id, event_type=event["type"])
    )
    try:
        session.flush()
    except IntegrityError:
        session.rollback()
        logger.info("[BILLING] Concurrent duplicate event %s, skipping", event_id)
        return {"received": True}

    try:
        await handler(event["data"]["object"], session)
        session.commit()
        logger.info("[BILLING] Webhook event processed: %s", event["type"])
    except Exception as e:  # pylint: disable=broad-except
        session.rollback()
        logger.error("[BILLING] Handler %s failed: %s", event["type"], str(e))
        raise HTTPException(
            status_code=500, detail="Falha ao processar evento"
        ) from e

    return {"received": True}


async def _handle_subscription_updated(data: dict, session: Session):
    """Event: customer.subscription.updated"""
    stripe_subscription_id = data.get("id")
    stripe_status = _normalize_stripe_status(data.get("status"))
    current_period_end_ts = data.get("current_period_end")
    current_period_starts_at_ts = data.get("current_period_start")

    subscription = session.exec(
        select(TrainerSubscription).where(
            TrainerSubscription.stripe_subscription_id == stripe_subscription_id
        )
    ).first()

    if not subscription:
        logger.warning(
            "[BILLING] Subscription update: stripe_id %s not found",
            stripe_subscription_id,
        )
        return

    status_map = {
        "trialing": SubscriptionStatus.TRIALING,
        "active": SubscriptionStatus.ACTIVE,
        "past_due": SubscriptionStatus.PAST_DUE,
        "cancelled": SubscriptionStatus.CANCELLED,
        "unpaid": SubscriptionStatus.CANCELLED,
        "incomplete": SubscriptionStatus.TRIAL_EXPIRED,
        "incomplete_expired": SubscriptionStatus.TRIAL_EXPIRED,
    }

    new_status = status_map.get(stripe_status, SubscriptionStatus.CANCELLED)
    subscription.status = new_status

    if current_period_starts_at_ts:
        subscription.current_period_start = datetime.fromtimestamp(
            current_period_starts_at_ts, tz=timezone.utc
        )

    if current_period_end_ts:
        subscription.current_period_end = datetime.fromtimestamp(
            current_period_end_ts, tz=timezone.utc
        )

    subscription.updated_at = datetime.now(timezone.utc)
    session.add(subscription)


async def _handle_subscription_deleted(data: dict, session: Session):
    """Event: customer.subscription.deleted"""
    stripe_subscription_id = data.get("id")

    subscription = session.exec(
        select(TrainerSubscription).where(
            TrainerSubscription.stripe_subscription_id == stripe_subscription_id
        )
    ).first()

    if not subscription:
        logger.warning(
            "[BILLING] Subscription delete: stripe_id %s not found",
            stripe_subscription_id,
        )
        return

    subscription.status = SubscriptionStatus.CANCELLED
    subscription.updated_at = datetime.now(timezone.utc)
    session.add(subscription)


async def _handle_payment_succeeded(data: dict, session: Session):
    """Event: invoice.payment_succeeded"""
    stripe_subscription_id = _stripe_resource_id(data.get("subscription"))

    if not stripe_subscription_id:
        logger.warning("[BILLING] Payment succeeded: no subscription associated")
        return

    subscription = session.exec(
        select(TrainerSubscription).where(
            TrainerSubscription.stripe_subscription_id == stripe_subscription_id
        )
    ).first()

    if not subscription:
        logger.warning(
            "[BILLING] Payment succeeded: stripe_id %s not found",
            stripe_subscription_id,
        )
        return

    subscription.status = SubscriptionStatus.ACTIVE
    subscription.updated_at = datetime.now(timezone.utc)
    session.add(subscription)


async def _handle_payment_failed(data: dict, session: Session):
    """Event: invoice.payment_failed"""
    stripe_subscription_id = _stripe_resource_id(data.get("subscription"))

    if not stripe_subscription_id:
        logger.warning("[BILLING] Payment failed: no subscription associated")
        return

    subscription = session.exec(
        select(TrainerSubscription).where(
            TrainerSubscription.stripe_subscription_id == stripe_subscription_id
        )
    ).first()

    if not subscription:
        logger.warning(
            "[BILLING] Payment failed: stripe_id %s not found", stripe_subscription_id
        )
        return

    subscription.status = SubscriptionStatus.PAST_DUE
    subscription.updated_at = datetime.now(timezone.utc)
    session.add(subscription)


async def _handle_trial_will_end(data: dict, session: Session):
    """Event: customer.subscription.trial_will_end"""
    stripe_subscription_id = data.get("id")
    trial_end_ts = data.get("trial_end")

    subscription = session.exec(
        select(TrainerSubscription).where(
            TrainerSubscription.stripe_subscription_id == stripe_subscription_id
        )
    ).first()

    if not subscription:
        logger.warning(
            "[BILLING] Trial will end: stripe_id %s not found", stripe_subscription_id
        )
        return

    if subscription.trial_warning_sent:
        logger.info(
            "[BILLING] Trial warning already sent for subscription %s, skipping",
            stripe_subscription_id,
        )
        return

    trainer = session.get(User, subscription.trainer_user_id)
    if not trainer:
        logger.warning(
            "[BILLING] Trial will end: trainer not found for subscription %s",
            stripe_subscription_id,
        )
        return

    trial_end_str = (
        datetime.fromtimestamp(trial_end_ts, tz=timezone.utc).strftime("%d/%m/%Y")
        if trial_end_ts
        else "em breve"
    )

    try:
        EmailService.send_plain_email(
            to_email=trainer.email,
            subject="O teu período de trial termina em 3 dias - PT Manager",
            body=(
                f"Olá {trainer.full_name},\n\n"
                f"O teu período de trial termina a {trial_end_str}.\n\n"
                "Para não perderes o acesso à plataforma, "
                "adiciona um método de pagamento.\n\n"
                "— Equipa PT Manager"
            ),
        )
    except Exception as e:  # pylint: disable=broad-except
        logger.error("[BILLING] Email failed, will retry: %s", str(e))
        raise

    subscription.trial_warning_sent = True
    subscription.updated_at = datetime.now(timezone.utc)
    session.add(subscription)
