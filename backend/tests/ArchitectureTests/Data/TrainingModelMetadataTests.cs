using Application.Common.Abstractions;
using Domain.Entities.Training;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace ArchitectureTests.Data;

public sealed class TrainingModelMetadataTests : IDisposable
{
    private readonly PtManagerDbContext _context;
    private readonly IModel _model;

    public TrainingModelMetadataTests()
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
    public void TrainingStructure_CollectionsUseCanonicalForeignKeys()
    {
        var dayNavigation = RequireEntity<TrainingPlan>()
            .FindNavigation(nameof(TrainingPlan.Days));
        var exerciseNavigation = RequireEntity<TrainingPlanDay>()
            .FindNavigation(nameof(TrainingPlanDay.Exercises));
        var setNavigation = RequireEntity<TrainingPlanDayExercise>()
            .FindNavigation(nameof(TrainingPlanDayExercise.Sets));

        Assert.NotNull(dayNavigation);
        Assert.Equal(
            nameof(TrainingPlanDay.TrainingPlanId),
            Assert.Single(dayNavigation.ForeignKey.Properties).Name);
        Assert.NotNull(exerciseNavigation);
        Assert.Equal(
            nameof(TrainingPlanDayExercise.TrainingPlanDayId),
            Assert.Single(exerciseNavigation.ForeignKey.Properties).Name);
        Assert.NotNull(setNavigation);
        Assert.Equal(
            nameof(ExerciseSet.TrainingPlanDayExerciseId),
            Assert.Single(setNavigation.ForeignKey.Properties).Name);
    }

    [Fact]
    public void TrainingStructure_UniqueIndexesMatchCanonicalContract()
    {
        var activePlan = RequireIndex<TrainingPlan>(
            "uq_training_plan_active_per_client");
        var daySlot = RequireIndex<TrainingPlanDay>(
            "uq_training_plan_day_weekday");
        var setNumber = RequireIndex<ExerciseSet>(
            "uq_exercise_set_number");

        Assert.True(activePlan.IsUnique);
        Assert.Equal(
            "is_active = true AND is_deleted = false",
            activePlan.GetFilter());
        Assert.True(daySlot.IsUnique);
        Assert.True(setNumber.IsUnique);
    }

    [Fact]
    public void ExerciseSetLog_UsesChronologicalNonUniqueIndexAndRestrictDelete()
    {
        var entity = RequireEntity<ClientExerciseSetLog>();
        var performedAt = entity.FindProperty(nameof(ClientExerciseSetLog.PerformedAt))
            ?? throw new InvalidOperationException("PerformedAt is not mapped.");
        var table = StoreObjectIdentifier.Table(
            entity.GetTableName() ?? throw new InvalidOperationException("Log table is missing."),
            entity.GetSchema());
        var chronological = RequireIndex<ClientExerciseSetLog>(
            "idx_logs_client_performed_at");
        var exerciseForeignKey = entity.GetForeignKeys().Single(value =>
            value.PrincipalEntityType.ClrType == typeof(TrainingPlanDayExercise));

        Assert.Equal("performed_at", performedAt.GetColumnName(table));
        Assert.Equal("now()", performedAt.GetDefaultValueSql());
        Assert.False(chronological.IsUnique);
        Assert.Equal(
            new[]
            {
                nameof(ClientExerciseSetLog.ClientId),
                nameof(ClientExerciseSetLog.PerformedAt),
                nameof(ClientExerciseSetLog.Id)
            },
            chronological.Properties.Select(property => property.Name));
        Assert.Equal(new[] { false, true, false }, chronological.IsDescending);
        Assert.Equal(DeleteBehavior.Restrict, exerciseForeignKey.DeleteBehavior);
        Assert.DoesNotContain(
            entity.GetIndexes(),
            index => index.GetDatabaseName() == "unique_set_log");
    }

    public void Dispose() => _context.Dispose();

    private IReadOnlyEntityType RequireEntity<TEntity>()
        where TEntity : class =>
        _model.FindEntityType(typeof(TEntity))
        ?? throw new InvalidOperationException($"{typeof(TEntity).Name} is not mapped.");

    private IReadOnlyIndex RequireIndex<TEntity>(string databaseName)
        where TEntity : class =>
        RequireEntity<TEntity>().GetIndexes().Single(index =>
            index.GetDatabaseName() == databaseName);

    private sealed class MetadataTenantContext : ITenantContext
    {
        public Guid? TrainerId => null;
        public Guid? UserId => null;
        public string? Role => "superuser";
        public TenantOrigin Origin => TenantOrigin.System;
        public bool IsAdministrative => true;
    }
}
