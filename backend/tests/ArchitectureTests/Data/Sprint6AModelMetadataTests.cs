using Application.Common.Abstractions;
using Domain.Entities.Assessments;
using Domain.Entities.Nutrition;
using Domain.Entities.Supplements;
using Domain.Entities.Training;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace ArchitectureTests.Data;

/// <summary>Fixa o contrato de persistência da Sprint 6A sem precisar de base de dados.</summary>
public sealed class Sprint6AModelMetadataTests : IDisposable
{
    private readonly PtManagerDbContext _context;
    private readonly IModel _model;

    public Sprint6AModelMetadataTests()
    {
        var options = new DbContextOptionsBuilder<PtManagerDbContext>()
            .UseNpgsql("Host=localhost;Database=metadata_tests;Username=metadata_tests;Password=metadata_tests")
            .Options;

        _context = new PtManagerDbContext(options, new MetadataTenantContext());
        _model = _context.GetService<IDesignTimeModel>().Model;
    }

    [Theory]
    [InlineData(typeof(ExerciseSet), nameof(ExerciseSet.PlannedRpe), "planned_rpe", 3, 1)]
    [InlineData(typeof(ClientExerciseSetLog), nameof(ClientExerciseSetLog.Rpe), "rpe", 3, 1)]
    [InlineData(typeof(Food), nameof(Food.DefaultServingGrams), "default_serving_grams", 10, 2)]
    public void NewDecimalColumns_AreNullableWithCanonicalPrecision(
        Type entityType, string propertyName, string column, int precision, int scale)
    {
        var entity = _model.FindEntityType(entityType)!;
        var property = entity.FindProperty(propertyName)!;
        var table = StoreObjectIdentifier.Table(entity.GetTableName()!, null);

        Assert.True(property.IsNullable);
        Assert.Equal(column, property.GetColumnName(table));
        Assert.Equal((precision, scale), (property.GetPrecision(), property.GetScale()));
    }

    [Theory]
    [InlineData(typeof(ExerciseSet), "planned_rpe_check")]
    [InlineData(typeof(ClientExerciseSetLog), "rpe_check")]
    [InlineData(typeof(Food), "ck_foods_default_serving_grams")]
    [InlineData(typeof(CheckIn), "ck_checkins_review_requires_response")]
    public void NewCheckConstraints_AreDeclared(Type entityType, string constraintName) =>
        Assert.Contains(
            _model.FindEntityType(entityType)!.GetCheckConstraints(),
            constraint => constraint.Name == constraintName);

    [Fact]
    public void WorkoutCompletion_IsUniquePerClientDayAndDateWithRestrictedHistory()
    {
        var entity = RequireEntity<WorkoutCompletion>();
        var unique = entity.GetIndexes().Single(index =>
            index.GetDatabaseName() == "uq_workout_completions_client_day_date");

        Assert.True(unique.IsUnique);
        Assert.Equal(
            [nameof(WorkoutCompletion.ClientId), nameof(WorkoutCompletion.TrainingPlanDayId), nameof(WorkoutCompletion.LocalDate)],
            unique.Properties.Select(property => property.Name));
        Assert.Equal(DeleteBehavior.Restrict, ForeignKeyTo<WorkoutCompletion, TrainingPlanDay>().DeleteBehavior);
        Assert.Equal(DeleteBehavior.Restrict, ForeignKeyTo<WorkoutCompletion, TrainingPlan>().DeleteBehavior);
        Assert.NotEmpty(entity.GetDeclaredQueryFilters());
    }

    [Fact]
    public void ClientSupplementIntake_IsUniquePerAssignmentAndDateWithTenantForeignKey()
    {
        var entity = RequireEntity<ClientSupplementIntake>();
        var unique = entity.GetIndexes().Single(index =>
            index.GetDatabaseName() == "uq_client_supplement_intakes_assignment_date");
        var clientForeignKey = entity.GetForeignKeys().Single(key =>
            key.GetConstraintName() == "fk_client_supplement_intakes_client_tenant");

        Assert.True(unique.IsUnique);
        Assert.Equal(
            [nameof(ClientSupplementIntake.OwnerTrainerId), nameof(ClientSupplementIntake.ClientId)],
            clientForeignKey.Properties.Select(property => property.Name));
        Assert.Equal(DeleteBehavior.Restrict, ForeignKeyTo<ClientSupplementIntake, ClientSupplementAssignment>().DeleteBehavior);
        Assert.NotEmpty(entity.GetDeclaredQueryFilters());
    }

    public void Dispose() => _context.Dispose();

    private IReadOnlyEntityType RequireEntity<TEntity>()
        where TEntity : class =>
        _model.FindEntityType(typeof(TEntity))
        ?? throw new InvalidOperationException($"{typeof(TEntity).Name} is not mapped.");

    private IReadOnlyForeignKey ForeignKeyTo<TDependent, TPrincipal>()
        where TDependent : class =>
        RequireEntity<TDependent>().GetForeignKeys()
            .Single(key => key.PrincipalEntityType.ClrType == typeof(TPrincipal));

    private sealed class MetadataTenantContext : ITenantContext
    {
        public Guid? TrainerId => null;
        public Guid? UserId => null;
        public string? Role => "superuser";
        public TenantOrigin Origin => TenantOrigin.System;
        public bool IsAdministrative => true;
    }
}
