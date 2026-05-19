"""
Modelo de eventos processados do Stripe.
"""

from datetime import datetime, timezone
from sqlmodel import Field, SQLModel


class ProcessedStripeEvent(SQLModel, table=True):
    """
    Registo de eventos processados do Stripe.
    """

    __tablename__ = "processed_stripe_events"

    event_id: str = Field(primary_key=True)
    event_type: str = Field(index=True)
    processed_at: datetime = Field(default_factory=lambda: datetime.now(timezone.utc))
