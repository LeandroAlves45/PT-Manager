using Application.Common.Abstractions;
using Domain.Entities.Jobs;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Tests.ArchitectureTests.Data;

public sealed class JobsModelMetadataTests : IDisposable
{
    private readonly PtManagerDbContext _context;

    public JobsModelMetadataTests()
    {
        var options = new DbContextOptionsBuilder<PtManagerDbContext>()
            .UseNpgsql("Host=localhost;Database=metadata_tests;" +
                    "Username=metadata_tests;Password=metadata_tests;")
            .Options;
        _context = new PtManagerDbContext(options, new MetadataTenantContext());
    }

    [Fact]
    public void OutboxMessage_CompletedAt_IsNullable()
    {
        // Arrange
        var model = _context.GetService<IDesignTimeModel>().Model;
        var entity = model.FindEntityType(typeof(OutboxMessage))
            ?? throw new InvalidOperationException("OutboxMessage is not mapped");
        var completedAt = entity.FindProperty(nameof(OutboxMessage.CompletedAt));

        // Assert
        Assert.NotNull(completedAt);
        Assert.True(completedAt.IsNullable);
    }

    public void Dispose() => _context.Dispose();

    private sealed class MetadataTenantContext : ITenantContext
    {
        public Guid? TrainerId => null;
        public Guid? UserId => null;
        public string? Role => "superuser";
        public TenantOrigin Origin => TenantOrigin.System;
        public bool IsAdministrative => true;
    }
}

