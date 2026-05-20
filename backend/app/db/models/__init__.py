"""
Definição SQLModel para PT Manager.

Exporta todos os models para o uso na aplicação.
"""

from app.db.models.active_token import ActiveToken
from app.db.models.checkin import CheckIn
from app.db.models.client import Client
from app.db.models.client_supplement import ClientSupplement
from app.db.models.initial_assessment import InitialAssessment
from app.db.models.notification import Notification
from app.db.models.nutrition import (
    Food,
    MealPlan,
    MealPlanItem,
    MealPlanMeal,
    MealPlanMealSupplement,
)
from app.db.models.pack import ClientPack, PackType
from app.db.models.session import PackConsumption, TrainingSession
from app.db.models.supplement import Supplement
from app.db.models.trainer_settings import TrainerSettings
from app.db.models.trainer_subscription import (
    TrainerSubscription,
    SubscriptionStatus,
    SubscriptionTier,
)
from app.db.models.processed_stripe_event import ProcessedStripeEvent
from app.db.models.training import (
    ClientActivePlan,
    ClientExerciseSetLog,
    Exercise,
    PlanDayExercise,
    PlanExerciseSetLoad,
    TrainingPlan,
    TrainingPlanDay,
)
from app.db.models.refresh_tokens import RefreshToken
from app.db.models.user import User


__all__ = [
    "ActiveToken",
    "CheckIn",
    "Client",
    "ClientSupplement",
    "InitialAssessment",
    "Notification",
    "Food",
    "MealPlan",
    "MealPlanItem",
    "MealPlanMeal",
    "MealPlanMealSupplement",
    "ClientPack",
    "PackType",
    "PackConsumption",
    "TrainingSession",
    "Supplement",
    "TrainerSettings",
    "TrainerSubscription",
    "ClientActivePlan",
    "ClientExerciseSetLog",
    "Exercise",
    "PlanDayExercise",
    "PlanExerciseSetLoad",
    "TrainingPlan",
    "TrainingPlanDay",
    "User",
    "ProcessedStripeEvent",
    "RefreshToken",
    "SubscriptionStatus",
    "SubscriptionTier",
]
