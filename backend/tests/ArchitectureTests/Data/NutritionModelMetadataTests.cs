using Application.Common.Abstractions;
using Domain.Entities.Assessments;
using Domain.Entities.Clients;
using Domain.Entities.Nutrition;
using Domain.Entities.Supplements;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace ArchitectureTests.Data;

public sealed class NutritionModelMetadataTests : IDisposable
{
    private readonly PtManagerDbContext _context;

    public NutritionModelMetadataTests()
    {
        var options = new DbContextOptionsBuilder<PtManagerDbContext>()
            .UseNpgsql("Host=localhost;Database=metadata_tests;Username=metadata_tests;Password=metadata_tests")
            .Options;

        _context = new PtManagerDbContext(options, new MetadataTenantContext());
    }

    [Fact]
    public void Client_RequiredProfileValueObjects_UseExpectedColumns()
    {
        // Arrange
        var entity = RequireEntity<Client>();
        var table = StoreObjectIdentifier.Table("clients", null);
        var birthDate = entity.FindProperty(nameof(Client.BirthDate));
        var sex = entity.FindProperty(nameof(Client.Sex));

        // Assert
        Assert.NotNull(birthDate);
        Assert.NotNull(sex);
        Assert.False(birthDate.IsNullable);
        Assert.False(sex.IsNullable);
        Assert.Equal("date_of_birth", birthDate.GetColumnName(table));
        Assert.Equal("sex", sex.GetColumnName(table));
        Assert.Equal(typeof(DateOnly), birthDate.GetValueConverter()?.ProviderClrType);
        Assert.Equal(typeof(string), sex.GetValueConverter()?.ProviderClrType);
    }

    [Fact]
    public void InitialAssessment_RemovesDuplicateFieldsAndMapsActivityLevel()
    {
        // Arrange
        var entity = RequireEntity<InitialAssessment>();
        var table = StoreObjectIdentifier.Table("initial_assessments", null);
        var activityLevel = entity.FindProperty(nameof(InitialAssessment.ActivityLevel));

        // Assert
        Assert.Null(entity.FindProperty("Age"));
        Assert.Null(entity.FindProperty("Gender"));
        Assert.NotNull(activityLevel);
        Assert.Equal("activity_level", activityLevel.GetColumnName(table));
        Assert.Equal(typeof(string), activityLevel.GetValueConverter()?.ProviderClrType);
    }

    [Fact]
    public void Food_HasPerOneHundredGramConstraint()
    {
        // Arrange
        var entity = RequireEntity<Food>();
        var constraint = entity.GetCheckConstraints()
            .Single(value => value.Name == "ck_foods_nutrients_per_100g");

        // Assert
        Assert.Contains("protein + carbs + fats <= 100", constraint.Sql);
        Assert.Contains("protein BETWEEN 0 AND 100", constraint.Sql);
    }

    [Fact]
    public void MealPlan_MapsTargetsAndRequiredJsonSnapshot()
    {
        // Arrange
        var entity = RequireEntity<MealPlan>();
        var table = StoreObjectIdentifier.Table("meal_plans", null);
        var targets = entity.FindComplexProperty(nameof(MealPlan.Targets));
        var snapshot = entity.FindComplexProperty(nameof(MealPlan.CalculationSnapshot));

        // Assert
        Assert.NotNull(targets);
        Assert.NotNull(snapshot);
        Assert.False(snapshot.IsNullable);
        Assert.Equal(
            "fats_target_g",
            targets.ComplexType.FindProperty("FatsGrams")?.GetColumnName(table)
        );
        Assert.Equal(
            "calculation_snapshot",
            snapshot.ComplexType.GetContainerColumnName()
        );
    }

    [Fact]
    public void MealPlan_HasEnergyToleranceConstraint()
    {
        // Arrange
        var entity = RequireEntity<MealPlan>();
        var constraint = entity.GetCheckConstraints()
            .Single(value => value.Name == "ck_meal_plans_targets");

        // Assert
        Assert.Contains("kcal_target > 0", constraint.Sql);
        Assert.Contains("<= 100", constraint.Sql);
    }

    [Fact]
    public void ClientSupplementAssignment_IsMappedAsTenantOwnedEntity()
    {
        // Arrange
        var entity = RequireEntity<ClientSupplementAssignment>();
        var table = StoreObjectIdentifier.Table("client_supplement_assignments", null);

        var ownerTrainerId = entity.FindProperty(nameof(ClientSupplementAssignment.OwnerTrainerId));
        var servingSize = entity.FindProperty(nameof(ClientSupplementAssignment.ServingSize));
        var timing = entity.FindProperty(nameof(ClientSupplementAssignment.Timing));

        // Assert
        Assert.NotNull(ownerTrainerId);
        Assert.NotNull(servingSize);
        Assert.NotNull(timing);
        Assert.False(ownerTrainerId.IsNullable);
        Assert.False(servingSize.IsNullable);
        Assert.False(timing.IsNullable);
        Assert.Equal("owner_trainer_id", ownerTrainerId.GetColumnName(table));
        Assert.Equal("serving_size", servingSize.GetColumnName(table));
        Assert.Equal("timing", timing.GetColumnName(table));
        Assert.NotNull(entity.GetDeclaredQueryFilters());
    }

    public void Dispose() => _context.Dispose();

    private IReadOnlyEntityType RequireEntity<TEntity>()
        where TEntity : class =>
        _context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(TEntity))
            ?? throw new InvalidOperationException($"{typeof(TEntity).Name} is not mapped.");

    private sealed class MetadataTenantContext : ITenantContext
    {
        public Guid? TrainerId { get; } = Guid.NewGuid();
        public Guid? UserId => null;
        public string? Role => "trainer";
        public TenantOrigin Origin => TenantOrigin.System;
        public bool IsAdministrative => false;
    }
}
