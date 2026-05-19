"""
Testes unitários para o SubscriptionService.

Estes testes cobrem a lógica pura de negócio:
    - Cálculo de tiers
    - Verificação de limites de clientes
    - Verificação de acesso activo

São testes unitários puros -> não tocam na BD nem no Stripe.
"""

from datetime import datetime, timedelta, timezone
from unittest.mock import MagicMock

from app.db.models.trainer_subscription import SubscriptionStatus, SubscriptionTier
from app.services.subscription_service import SubscriptionService


def _make_subscription(
    status=SubscriptionStatus.ACTIVE,
    tier=SubscriptionTier.STARTER,
    active_clients_count=0,
    trial_end=None,
) -> MagicMock:
    """
    Factory que cria um objecto mock de TrainerSubscription.
    Usar MagicMock em vez de instanciar o modelo SQLModel evita precisar de BD.
    """

    sub = MagicMock()
    sub.status = status
    sub.tier = tier
    sub.active_clients_count = active_clients_count
    sub.trial_end = trial_end

    return sub


class TestGetTierForCount:
    """Testa o cálculo de tier com base no número de clientes activos."""

    def test_zero_clients_is_free(self):
        """Zero clientes deve manter o trainer no tier FREE."""
        assert SubscriptionService.get_tier_for_count(0) == SubscriptionTier.FREE

    def test_five_clients_is_free(self):
        """Cinco clientes ainda deve manter o trainer no tier FREE."""
        assert SubscriptionService.get_tier_for_count(5) == SubscriptionTier.FREE

    def test_six_clients_is_starter(self):
        """Seis clientes deve subir o trainer para o tier STARTER."""
        assert SubscriptionService.get_tier_for_count(6) == SubscriptionTier.STARTER

    def test_forty_nine_clients_is_starter(self):
        """Quarenta e nove clientes ainda deve pertencer ao tier STARTER."""
        assert SubscriptionService.get_tier_for_count(49) == SubscriptionTier.STARTER

    def test_fifty_clients_is_pro(self):
        """Cinquenta clientes deve subir o trainer para o tier PRO."""
        assert SubscriptionService.get_tier_for_count(50) == SubscriptionTier.PRO

    def test_hundred_clients_is_pro(self):
        """Cem clientes deve continuar no tier PRO."""
        assert SubscriptionService.get_tier_for_count(100) == SubscriptionTier.PRO

    def test_boundary_between_free_and_starter(self):
        """A fronteira entre FREE e STARTER deve ser entre 5 e 6 clientes."""
        assert SubscriptionService.get_tier_for_count(5) == SubscriptionTier.FREE
        assert SubscriptionService.get_tier_for_count(6) == SubscriptionTier.STARTER

    def test_boundary_between_starter_and_pro(self):
        """A fronteira entre STARTER e PRO deve ser entre 49 e 50 clientes."""
        assert SubscriptionService.get_tier_for_count(49) == SubscriptionTier.STARTER
        assert SubscriptionService.get_tier_for_count(50) == SubscriptionTier.PRO


class TestCanAddClient:
    """Testa as regras para adicionar clientes conforme subscrição e limites."""

    def test_free_tier_below_limit_can_add(self):
        """FREE com 3 clientes activos pode adicionar mais clientes."""
        sub = _make_subscription(
            status=SubscriptionStatus.ACTIVE,
            tier=SubscriptionTier.FREE,
            active_clients_count=3,
        )

        can_add, msg = SubscriptionService.can_add_client(sub)
        assert can_add is True
        assert msg == ""

    def test_free_tier_at_limit_cannot_add(self):
        """FREE com 5 clientes activos não pode adicionar mais clientes."""
        sub = _make_subscription(
            status=SubscriptionStatus.ACTIVE,
            tier=SubscriptionTier.FREE,
            active_clients_count=5,
        )
        can_add, msg = SubscriptionService.can_add_client(sub)
        assert can_add is False
        assert len(msg) > 0
        assert "FREE" in msg

    def test_starter_tier_at_limit_cannot_add(self):
        """STARTER com 49 clientes não pode adicionar mais clientes."""
        sub = _make_subscription(
            status=SubscriptionStatus.ACTIVE,
            tier=SubscriptionTier.STARTER,
            active_clients_count=49,
        )
        can_add, msg = SubscriptionService.can_add_client(sub)
        assert can_add is False
        assert "pro" in msg.lower()

    def test_pro_tier_no_limit(self):
        """PRO deve permitir adicionar clientes sem limite prático."""
        sub = _make_subscription(
            status=SubscriptionStatus.ACTIVE,
            tier=SubscriptionTier.PRO,
            active_clients_count=999,
        )
        can_add, _msg = SubscriptionService.can_add_client(sub)
        assert can_add is True

    def test_cancelled_subscription_cannot_add(self):
        """Subscrição cancelada não pode adicionar clientes."""
        sub = _make_subscription(
            status=SubscriptionStatus.CANCELLED,
            tier=SubscriptionTier.STARTER,
            active_clients_count=0,
        )
        can_add, msg = SubscriptionService.can_add_client(sub)
        assert can_add is False
        assert len(msg) > 0

    def test_trial_expired_cannot_add(self):
        """Subscrição com trial expirado não pode adicionar clientes."""
        sub = _make_subscription(
            status=SubscriptionStatus.TRIAL_EXPIRED,
            tier=SubscriptionTier.FREE,
            active_clients_count=0,
        )

        can_add, msg = SubscriptionService.can_add_client(sub)
        assert can_add is False
        assert "trial" in msg.lower()

    def test_trialling_within_limits_can_add(self):
        """Subscrição em trial dentro dos limites pode adicionar clientes."""
        sub = _make_subscription(
            status=SubscriptionStatus.TRIALING,
            tier=SubscriptionTier.FREE,
            active_clients_count=2,
        )

        can_add, msg = SubscriptionService.can_add_client(sub)
        assert can_add is True
        assert "trial" in msg.lower()

    def test_past_due_within_limit_can_add(self):
        """PAST_DUE dentro dos limites ainda permite adicionar clientes."""
        sub = _make_subscription(
            status=SubscriptionStatus.PAST_DUE,
            tier=SubscriptionTier.STARTER,
            active_clients_count=10,
        )

        can_add, msg = SubscriptionService.can_add_client(sub)
        assert can_add is True
        assert "pagamento" in msg.lower()


class TestHasActiveAccess:
    """Testa a verificação de acesso activo à plataforma."""

    def test_none_subscription_has_no_access(self):
        """Sem subscrição, o trainer não tem acesso."""
        assert SubscriptionService.has_active_access(None) is False

    def test_active_subscription_has_access(self):
        """Subscrição activa concede acesso."""
        sub = _make_subscription(status=SubscriptionStatus.ACTIVE)
        assert SubscriptionService.has_active_access(sub) is True

    def test_trialing_subscription_has_access(self):
        """Subscrição em trial válido concede acesso."""
        future = datetime.now(timezone.utc) + timedelta(days=10)
        sub = _make_subscription(status=SubscriptionStatus.TRIALING, trial_end=future)
        assert SubscriptionService.has_active_access(sub) is True

    def test_trialing_past_end_has_no_access(self):
        """Subscrição em trial expirado não concede acesso."""
        past = datetime.now(timezone.utc) - timedelta(days=1)
        sub = _make_subscription(status=SubscriptionStatus.TRIALING, trial_end=past)
        assert SubscriptionService.has_active_access(sub) is False

    def test_cancelled_subscription_has_no_access(self):
        """Subscrição cancelada não concede acesso."""
        sub = _make_subscription(status=SubscriptionStatus.CANCELLED)
        assert SubscriptionService.has_active_access(sub) is False

    def test_past_due_has_access(self):
        """Subscrição PAST_DUE ainda concede acesso durante o grace period."""
        sub = _make_subscription(status=SubscriptionStatus.PAST_DUE)
        assert SubscriptionService.has_active_access(sub) is True
