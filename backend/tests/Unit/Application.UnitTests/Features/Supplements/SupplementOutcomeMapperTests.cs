using Application.Features.Supplements;
using Application.Features.Supplements.Abstractions;
using Domain.Entities.Supplements;

namespace Application.UnitTests.Features.Supplements;

public sealed class SupplementOutcomeMapperTests
{
    private static readonly DateTime Now = new(2026, 8, 18, 10, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(SupplementStoreResult.Status.NotFound, "supplement_not_found")]
    [InlineData(SupplementStoreResult.Status.GlobalReadOnly, "global_supplement_read_only")]
    [InlineData(SupplementStoreResult.Status.Inactive, "supplement_inactive")]
    public void PrivateCatalogFailure_MapsStableError(
        SupplementStoreResult.Status status, string expectedCode)
    {
        var result = SupplementStoreResult.For(status).ToDtoResult();

        Assert.Equal(expectedCode, result.Error!.Code);
    }

    [Theory]
    [InlineData(SupplementStoreResult.Status.Created)]
    [InlineData(SupplementStoreResult.Status.Updated)]
    public void PrivateCatalogSuccess_MapsDto(SupplementStoreResult.Status status)
    {
        var supplement = CreateSupplement(Guid.NewGuid());

        var result = SupplementStoreResult.WithSupplement(status, supplement).ToDtoResult();

        Assert.Equal(supplement.Id, result.Value.Id);
    }

    [Theory]
    [InlineData(SupplementStoreResult.Status.Changed)]
    [InlineData(SupplementStoreResult.Status.AlreadyInRequestedState)]
    public void PrivateTransitionSuccess_MapsSuccess(SupplementStoreResult.Status status)
    {
        var result = SupplementStoreResult.For(status).ToTransitionResult();

        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData(ClientSupplementAssignmentStoreResult.Status.ClientNotFound, "client_not_found")]
    [InlineData(ClientSupplementAssignmentStoreResult.Status.ClientInactive, "supplement_client_inactive")]
    [InlineData(ClientSupplementAssignmentStoreResult.Status.SupplementNotFound, "supplement_not_found")]
    [InlineData(ClientSupplementAssignmentStoreResult.Status.SupplementInactive, "supplement_inactive")]
    [InlineData(ClientSupplementAssignmentStoreResult.Status.AssignmentNotFound, "supplement_assignment_not_found")]
    [InlineData(ClientSupplementAssignmentStoreResult.Status.AssignmentAlreadyExists, "supplement_assignment_already_exists")]
    public void AssignmentFailure_MapsStableError(
        ClientSupplementAssignmentStoreResult.Status status, string expectedCode)
    {
        var result = ClientSupplementAssignmentStoreResult.For(status).ToDtoResult();

        Assert.Equal(expectedCode, result.Error!.Code);
    }

    [Theory]
    [InlineData(ClientSupplementAssignmentStoreResult.Status.Assigned)]
    [InlineData(ClientSupplementAssignmentStoreResult.Status.Updated)]
    [InlineData(ClientSupplementAssignmentStoreResult.Status.Changed)]
    [InlineData(ClientSupplementAssignmentStoreResult.Status.AlreadyInRequestedState)]
    public void AssignmentSuccess_MapsEntities(
        ClientSupplementAssignmentStoreResult.Status status)
    {
        var supplement = CreateSupplement(Guid.NewGuid());
        var assignment = new ClientSupplementAssignment(
            supplement.OwnerTrainerId!.Value, Guid.NewGuid(), supplement.Id,
            "5 g", "daily", "client note", Now);

        var result = ClientSupplementAssignmentStoreResult
            .WithEntities(status, assignment, supplement).ToDtoResult();

        Assert.Equal((assignment.Id, supplement.Id),
            (result.Value.Id, result.Value.SupplementId));
    }

    [Theory]
    [InlineData(GlobalSupplementStoreResult.Status.NotFound, "supplement_not_found")]
    [InlineData(GlobalSupplementStoreResult.Status.Inactive, "supplement_inactive")]
    [InlineData(GlobalSupplementStoreResult.Status.HasReferences, "global_supplement_has_references")]
    public void GlobalFailure_MapsStableError(
        GlobalSupplementStoreResult.Status status, string expectedCode)
    {
        var outcome = GlobalSupplementStoreResult.For(status);
        var result = status != GlobalSupplementStoreResult.Status.HasReferences
            ? outcome.ToDtoResult()
            : null;
        var transition = status == GlobalSupplementStoreResult.Status.HasReferences
            ? outcome.ToTransitionResult()
            : null;

        Assert.Equal(expectedCode, (result?.Error ?? transition?.Error)!.Code);
    }

    [Theory]
    [InlineData(GlobalSupplementStoreResult.Status.Created)]
    [InlineData(GlobalSupplementStoreResult.Status.Updated)]
    public void GlobalMutationSuccess_MapsDto(GlobalSupplementStoreResult.Status status)
    {
        var supplement = CreateSupplement(null);

        var result = GlobalSupplementStoreResult.WithSupplement(status, supplement)
            .ToDtoResult();

        Assert.Equal(supplement.Id, result.Value.Id);
    }

    [Theory]
    [InlineData(GlobalSupplementStoreResult.Status.Changed)]
    [InlineData(GlobalSupplementStoreResult.Status.Deleted)]
    [InlineData(GlobalSupplementStoreResult.Status.AlreadyInRequestedState)]
    public void GlobalTransitionSuccess_MapsSuccess(GlobalSupplementStoreResult.Status status)
    {
        var result = GlobalSupplementStoreResult.For(status).ToTransitionResult();

        Assert.True(result.IsSuccess);
    }

    private static Supplement CreateSupplement(Guid? ownerTrainerId) => new(
        ownerTrainerId, Guid.NewGuid(), "Creatine", null,
        "grams", "5 g", "daily", "internal", Now);
}
