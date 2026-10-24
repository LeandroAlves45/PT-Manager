using Domain.Exceptions;

namespace Domain.Entities.Supplements;

/// <summary>
/// Toma de hoje de um suplemento do plano alimentar escolhido, no dia local do personal
/// trainer. Refere a prescrição exata: o suplemento de uma refeição ou o do plano fora das
/// refeições. No máximo uma por prescrição e por dia; desmarcar apaga o registo. Se o personal
/// trainer tirar a prescrição do plano, a toma fica (o histórico não tem página).
/// </summary>
public sealed class ClientMealPlanSupplementIntake
{
    public Guid Id { get; private set; }
    public Guid OwnerTrainerId { get; private set; }
    public Guid ClientId { get; private set; }
    public Guid MealPlanId { get; private set; }
    public Guid? MealPlanMealSupplementId { get; private set; }
    public Guid? MealPlanSupplementId { get; private set; }

    /// <summary>Suplemento do catálogo, guardado para o histórico sobreviver à prescrição.</summary>
    public Guid SupplementId { get; private set; }
    public DateOnly LocalDate { get; private set; }
    public DateTime TakenAt { get; private set; }

    private ClientMealPlanSupplementIntake() { }

    /// <summary>Regista a toma de hoje de uma prescrição do plano.</summary>
    public ClientMealPlanSupplementIntake(
        Guid ownerTrainerId,
        Guid clientId,
        Guid mealPlanId,
        Guid? mealPlanMealSupplementId,
        Guid? mealPlanSupplementId,
        Guid supplementId,
        DateOnly localDate,
        DateTime now)
    {
        if (ownerTrainerId == Guid.Empty || clientId == Guid.Empty ||
            mealPlanId == Guid.Empty || supplementId == Guid.Empty)
            throw new DomainException("Owner trainer, client, meal plan and supplement are required.");
        if (mealPlanMealSupplementId.HasValue == mealPlanSupplementId.HasValue)
            throw new DomainException("An intake refers to exactly one plan supplement.");

        Id = Guid.NewGuid();
        OwnerTrainerId = ownerTrainerId;
        ClientId = clientId;
        MealPlanId = mealPlanId;
        MealPlanMealSupplementId = mealPlanMealSupplementId;
        MealPlanSupplementId = mealPlanSupplementId;
        SupplementId = supplementId;
        LocalDate = localDate;
        TakenAt = now;
    }
}
