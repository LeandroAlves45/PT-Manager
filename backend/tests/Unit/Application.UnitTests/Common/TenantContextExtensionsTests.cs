using Application.Common.Abstractions;
using Application.Errors;
using Xunit;

namespace Application.UnitTests.Common;

/// <summary>Verifica que tenant ausente ou vazio falha de forma fechada.</summary>
public sealed class TenantContextExtensionsTests
{
    [Fact]
    public void MissingTrainer_ReturnsTenantRequired()
    {
        var context = new StubTenantContext(trainerId: null);

        var result = context.GetRequiredTrainerId();

        Assert.True(result.IsFailure);
        Assert.Equal(CommonErrors.TenantRequired.Code, result.Error!.Code);
    }

    [Fact]
    public void EmptyTrainer_ReturnsTenantRequired()
    {
        var context = new StubTenantContext(trainerId: Guid.Empty);

        var result = context.GetRequiredTrainerId();

        Assert.True(result.IsFailure);
        Assert.Equal(CommonErrors.TenantRequired.Code, result.Error!.Code);
    }

    [Fact]
    public void ValidTrainer_ReturnsIdentifier()
    {
        var trainerId = Guid.NewGuid();
        var context = new StubTenantContext(trainerId);

        var result = context.GetRequiredTrainerId();

        Assert.True(result.IsSuccess);
        Assert.Equal(trainerId, result.Value);
    }

    [Fact]
    public void NullContext_Throws()
    {
        ITenantContext? context = null;

        Assert.Throws<ArgumentNullException>(() => TenantContextExtensions.GetRequiredTrainerId(context!));
    }

    private sealed class StubTenantContext : ITenantContext
    {
        public Guid? TrainerId { get; }
        public Guid? UserId { get; } = null;
        public string? Role { get; } = null;
        public TenantOrigin Origin { get; } = TenantOrigin.System;
        public bool IsAdministrative { get; } = false;

        public StubTenantContext(Guid? trainerId)
        {
            TrainerId = trainerId;
        }
    }
}
