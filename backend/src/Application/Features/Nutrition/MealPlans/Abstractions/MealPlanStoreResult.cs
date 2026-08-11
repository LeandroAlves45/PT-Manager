namespace Application.Features.Nutrition.MealPlans.Abstractions;

/// <summary>Classifica resultados funcionais de escrita de um plano alimentar.</summary>
public sealed class MealPlanStoreResult
{
    public enum Status
    {
        Created,
        Updated,
        Changed,
        AlreadyInRequestedState,
        NotFound,
        ClientNotFound,
        StructureReferenceNotFound,
        CatalogReferenceNotFound,
        CatalogReferenceInactive
    }

    public Status Kind { get; }
    public Guid? MealPlanId { get; }

    private MealPlanStoreResult(Status kind, Guid? mealPlanId)
    {
        Kind = kind;
        MealPlanId = mealPlanId;
    }

    public static MealPlanStoreResult ForCreated(Guid id) =>
        WithRequiredId(Status.Created, id);
    public static MealPlanStoreResult ForUpdated(Guid id) =>
        WithRequiredId(Status.Updated, id);
    public static MealPlanStoreResult ForChanged() => new(Status.Changed, null);
    public static MealPlanStoreResult ForAlreadyRequested() =>
        new(Status.AlreadyInRequestedState, null);
    public static MealPlanStoreResult ForNotFound() => new(Status.NotFound, null);
    public static MealPlanStoreResult ForClientNotFound() =>
        new(Status.ClientNotFound, null);
    public static MealPlanStoreResult ForStructureReferenceNotFound() =>
        new(Status.StructureReferenceNotFound, null);
    public static MealPlanStoreResult ForCatalogReferenceNotFound() =>
        new(Status.CatalogReferenceNotFound, null);
    public static MealPlanStoreResult ForCatalogReferenceInactive() =>
        new(Status.CatalogReferenceInactive, null);

    private static MealPlanStoreResult WithRequiredId(Status status, Guid id)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Meal plan ID is required.", nameof(id));

        return new MealPlanStoreResult(status, id);
    }
}
