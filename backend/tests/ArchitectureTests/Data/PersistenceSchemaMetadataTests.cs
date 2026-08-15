using Application.Common.Abstractions;
using Domain.Entities.Billing;
using Domain.Entities.Identity;
using Domain.Entities.Nutrition;
using Domain.Entities.Supplements;
using Domain.Entities.TrainerSettings;
using Domain.Entities.Training;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace ArchitectureTests.Data;

public sealed class PersistenceSchemaMetadataTests : IDisposable
{
    private readonly PtManagerDbContext _context;
    private readonly IModel _model;

    public PersistenceSchemaMetadataTests()
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
    public void RefreshToken_UserRelationship_UsesCascadeDelete()
    {
        // Arrange
        var entity = RequireEntity<RefreshToken>();
        var foreignKey = entity.GetForeignKeys()
            .Single(value =>
                value.PrincipalEntityType.ClrType == typeof(User) &&
                value.Properties.Single().Name == nameof(RefreshToken.UserId));

        // Assert
        Assert.Equal(DeleteBehavior.Cascade, foreignKey.DeleteBehavior);
    }

    [Fact]
    public void ScalarColumns_UseLowerSnakeCaseNames()
    {
        // Arrange
        var invalidColumns = _model.GetEntityTypes()
            .SelectMany(entity =>
            {
                var table = StoreObjectIdentifier.Create(entity, StoreObjectType.Table);
                return table.HasValue
                    ? entity.GetProperties()
                        .Select(property => property.GetColumnName(table.Value))
                    : [];
            })
            .Where(columnName =>
                columnName is not null &&
                (columnName.Any(char.IsUpper) || columnName.Contains(' ')))
            .Distinct()
            .Order()
            .ToArray();

        // Assert
        Assert.Empty(invalidColumns);
    }

    [Fact]
    public void CatalogLifecycle_MapsActiveColumnsWithTrueDefault()
    {
        // Arrange
        var mappings = new[]
        {
            GetPropertyMapping<Food>(nameof(Food.IsActive)),
            GetPropertyMapping<Exercise>(nameof(Exercise.IsActive)),
            GetPropertyMapping<Supplement>(nameof(Supplement.IsActive)),
            GetPropertyMapping<PackType>(nameof(PackType.IsActive)),
        };

        // Assert
        Assert.All(mappings, mapping =>
        {
            Assert.Equal("is_active", mapping.ColumnName);
            Assert.Equal(true, mapping.Property.GetDefaultValue());
            Assert.False(mapping.Property.IsNullable);
        });
    }

    [Fact]
    public void Supplement_RequiredServingFields_UseCanonicalColumns()
    {
        // Arrange
        var mappings = new[]
        {
            GetPropertyMapping<Supplement>(nameof(Supplement.UnitOfMeasure)),
            GetPropertyMapping<Supplement>(nameof(Supplement.ServingSize)),
            GetPropertyMapping<Supplement>(nameof(Supplement.Timing)),
            GetPropertyMapping<Supplement>(nameof(Supplement.TrainerNotes)),
        };

        // Assert
        Assert.Equal(
            new[] { "unit_of_measure", "serving_size", "timing", "trainer_notes" },
            mappings.Select(value => value.ColumnName));
        Assert.All(mappings.Take(3), mapping => Assert.False(mapping.Property.IsNullable));
    }

    [Fact]
    public void TrainerSettings_Timezone_UsesCanonicalColumnAndDefault()
    {
        // Arrange
        var mapping = GetPropertyMapping<TrainerSettings>(nameof(TrainerSettings.Timezone));

        // Assert
        Assert.Equal(
            ("time_zone_id", 100, "Europe/Lisbon"),
            (mapping.ColumnName,
                mapping.Property.GetMaxLength(),
                mapping.Property.GetDefaultValue()));
    }

    [Fact]
    public void ClientSessionPack_SnapshotAndPackTypeRelationship_MatchContract()
    {
        // Arrange
        var entity = RequireEntity<ClientSessionPack>();
        var snapshotColumns = new[]
        {
            GetPropertyMapping<ClientSessionPack>(nameof(ClientSessionPack.PackName)).ColumnName,
            GetPropertyMapping<ClientSessionPack>(nameof(ClientSessionPack.SessionsTotal)).ColumnName,
            GetPropertyMapping<ClientSessionPack>(nameof(ClientSessionPack.PriceCents)).ColumnName,
            GetPropertyMapping<ClientSessionPack>(nameof(ClientSessionPack.Currency)).ColumnName,
        };
        var packTypeForeignKey = entity.GetForeignKeys()
            .Single(value => value.PrincipalEntityType.ClrType == typeof(PackType));
        var constraintNames = entity.GetCheckConstraints()
            .Select(value => value.Name)
            .ToHashSet(StringComparer.Ordinal);
        var usableIndex = entity.GetIndexes()
            .Single(value => value.GetDatabaseName() ==
                "idx_client_session_packs_usable_order");

        // Assert
        Assert.Equal(
            new[] { "pack_name", "total_sessions", "price_cents", "currency" },
            snapshotColumns);
        Assert.Equal(DeleteBehavior.Restrict, packTypeForeignKey.DeleteBehavior);
        Assert.Contains("ck_client_session_packs_balance", constraintNames);
        Assert.Contains("ck_client_session_packs_price_non_negative", constraintNames);
        Assert.Contains("ck_client_session_packs_expected_end_order", constraintNames);
        Assert.Contains("ck_client_session_packs_completion_consistency", constraintNames);
        Assert.Equal(
            "sessions_remaining > 0 AND is_deleted = false",
            usableIndex.GetFilter());
    }

    public void Dispose() => _context.Dispose();

    private IReadOnlyEntityType RequireEntity<TEntity>()
        where TEntity : class =>
        _model.FindEntityType(typeof(TEntity))
        ?? throw new InvalidOperationException($"{typeof(TEntity).Name} is not mapped.");

    private PropertyMapping GetPropertyMapping<TEntity>(string propertyName)
        where TEntity : class
    {
        var entity = RequireEntity<TEntity>();
        var property = entity.FindProperty(propertyName)
            ?? throw new InvalidOperationException(
                $"{typeof(TEntity).Name}.{propertyName} is not mapped.");
        var table = StoreObjectIdentifier.Table(
            entity.GetTableName()
            ?? throw new InvalidOperationException($"{typeof(TEntity).Name} has no table."),
            entity.GetSchema());
        var columnName = property.GetColumnName(table)
            ?? throw new InvalidOperationException(
                $"{typeof(TEntity).Name}.{propertyName} has no column.");

        return new PropertyMapping(property, columnName);
    }

    private sealed record PropertyMapping(IReadOnlyProperty Property, string ColumnName);

    private sealed class MetadataTenantContext : ITenantContext
    {
        public Guid? TrainerId => null;
        public Guid? UserId => null;
        public string? Role => "superuser";
        public TenantOrigin Origin => TenantOrigin.System;
        public bool IsAdministrative => true;
    }
}
