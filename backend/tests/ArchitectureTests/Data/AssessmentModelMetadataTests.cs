using Application.Common.Abstractions;
using Domain.Entities.Assessments;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace ArchitectureTests.Data;

public sealed class AssessmentModelMetadataTests : IDisposable
{
    private readonly PtManagerDbContext _context;
    private readonly IModel _model;

    public AssessmentModelMetadataTests()
    {
        var options = new DbContextOptionsBuilder<PtManagerDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=metadata_tests;" +
                "Username=metadata_tests;Password=metadata_tests")
            .Options;
        _context = new PtManagerDbContext(options, new MetadataTenantContext());
        _model = _context.GetService<IDesignTimeModel>().Model;
    }

    [Fact]
    public void PendingReviewIndex_CoversTenantAndResponseOrder()
    {
        var index = _model.FindEntityType(typeof(CheckIn))!
            .GetIndexes()
            .Single(item => item.GetDatabaseName() == "idx_checkins_pending_review");

        Assert.Equal(
            new[] { "OwnerTrainerId", "RespondedAt", "Id" },
            index.Properties.Select(property => property.Name).ToArray());
        Assert.Equal(
            "responded_at IS NOT NULL AND cancelled_at IS NULL "
            + "AND reviewed_at IS NULL AND is_deleted = false",
            index.GetFilter());
        Assert.False(index.IsUnique);
    }

    public void Dispose() => _context.Dispose();

    private sealed class MetadataTenantContext : ITenantContext
    {
        public Guid? TrainerId => Guid.Empty;
        public Guid? UserId => Guid.Empty;
        public string? Role => "trainer";
        public TenantOrigin Origin => TenantOrigin.System;
        public bool IsAdministrative => false;
    }
}
