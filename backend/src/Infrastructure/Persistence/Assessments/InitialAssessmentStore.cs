using System.Data;
using Application.Features.Assessments.InitialAssessments.Abstractions;
using Domain.Entities.Assessments;
using Domain.Entities.Clients;
using Domain.ValueObjects;
using Infrastructure.Data;
using Infrastructure.Persistence.Errors;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Assessments;

/// <summary>Persiste avaliações iniciais de forma atómica e isolada por tenant.</summary>
internal sealed class InitialAssessmentStore : IInitialAssessmentStore
{
    private readonly PtManagerDbContext _dbContext;
    private readonly PostgresConstraintTranslator _constraintTranslator;

    public InitialAssessmentStore(
        PtManagerDbContext dbContext,
        PostgresConstraintTranslator constraintTranslator
    )
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _constraintTranslator = constraintTranslator ?? throw new ArgumentNullException(nameof(constraintTranslator));
    }

    public Task<InitialAssessmentStoreResult> CreateAsync(
        Guid trainerId,
        Guid clientId,
        decimal weightKg,
        int heightCm,
        decimal? bodyFatPercentage,
        string? medicalConditions,
        string fitnessLevel,
        ActivityLevel activityLevel,
        string goals,
        string? profession,
        BodyMeasurements bodyMeasurements,
        NutritionIntake nutritionIntake,
        DateTime now,
        CancellationToken cancellationToken) =>
        ExecuteTransactionAsync(
            () => CreateOnceAsync(
                trainerId,
                clientId,
                weightKg,
                heightCm,
                bodyFatPercentage,
                medicalConditions,
                fitnessLevel,
                activityLevel,
                goals,
                profession,
                bodyMeasurements,
                nutritionIntake,
                now,
                cancellationToken
            ),
            cancellationToken
        );

    public Task<InitialAssessmentStoreResult> UpdateAsync(
        Guid trainerId,
        Guid assessmentId,
        decimal weightKg,
        int heightCm,
        decimal? bodyFatPercentage,
        string? medicalConditions,
        string fitnessLevel,
        ActivityLevel activityLevel,
        string goals,
        string? profession,
        BodyMeasurements bodyMeasurements,
        NutritionIntake nutritionIntake,
        DateTime now,
        CancellationToken cancellationToken) =>
        ExecuteTransactionAsync(
            () => UpdateOnceAsync(
                trainerId,
                assessmentId,
                weightKg,
                heightCm,
                bodyFatPercentage,
                medicalConditions,
                fitnessLevel,
                activityLevel,
                goals,
                profession,
                bodyMeasurements,
                nutritionIntake,
                now,
                cancellationToken
            ),
            cancellationToken
        );

    private async Task<InitialAssessmentStoreResult> CreateOnceAsync(
        Guid trainerId,
        Guid clientId,
        decimal weightKg,
        int heightCm,
        decimal? bodyFatPercentage,
        string? medicalConditions,
        string fitnessLevel,
        ActivityLevel activityLevel,
        string goals,
        string? profession,
        BodyMeasurements bodyMeasurements,
        NutritionIntake nutritionIntake,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var client = await LockClientAsync(trainerId, clientId, cancellationToken);
        if (client is null)
            return InitialAssessmentStoreResult.For(
                InitialAssessmentStoreResult.Status.ClientNotFound
            );
        if (!client.IsActive)
            return InitialAssessmentStoreResult.For(
                InitialAssessmentStoreResult.Status.ClientInactive
            );

        var alreadyExists = await _dbContext.InitialAssessments
            .AnyAsync(
                assessment => assessment.OwnerTrainerId == trainerId &&
                    assessment.ClientId == clientId,
                cancellationToken);
        if (alreadyExists)
            return InitialAssessmentStoreResult.For(
                InitialAssessmentStoreResult.Status.AssessmentAlreadyExists
            );

        var assessment = new InitialAssessment(
            trainerId,
            clientId,
            weightKg,
            heightCm,
            bodyFatPercentage,
            medicalConditions,
            fitnessLevel,
            activityLevel,
            goals,
            profession,
            bodyMeasurements,
            nutritionIntake,
            now
        );
        _dbContext.InitialAssessments.Add(assessment);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (IsDuplicateAssessment(exception))
        {
            return InitialAssessmentStoreResult.For(
                InitialAssessmentStoreResult.Status.AssessmentAlreadyExists
            );
        }

        return InitialAssessmentStoreResult.For(
            InitialAssessmentStoreResult.Status.Created,
            assessment
        );
    }

    private async Task<InitialAssessmentStoreResult> UpdateOnceAsync(
        Guid trainerId,
        Guid assessmentId,
        decimal weightKg,
        int heightCm,
        decimal? bodyFatPercentage,
        string? medicalConditions,
        string fitnessLevel,
        ActivityLevel activityLevel,
        string goals,
        string? profession,
        BodyMeasurements bodyMeasurements,
        NutritionIntake nutritionIntake,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var clientId = await _dbContext.InitialAssessments
            .AsNoTracking()
            .Where(assessment => assessment.OwnerTrainerId == trainerId &&
                assessment.Id == assessmentId)
            .Select(assessment => (Guid?)assessment.ClientId)
            .SingleOrDefaultAsync(cancellationToken);
        if (!clientId.HasValue)
            return InitialAssessmentStoreResult.For(
                InitialAssessmentStoreResult.Status.AssessmentNotFound
            );

        // A ordem Client seguida de Assessment coincide com Create e evita ciclos de locks
        var client = await LockClientAsync(trainerId, clientId.Value, cancellationToken);
        if (client is null)
            return InitialAssessmentStoreResult.For(
                InitialAssessmentStoreResult.Status.AssessmentNotFound
            );

        var assessment = await LockAssessmentAsync(trainerId, assessmentId, cancellationToken);
        if (assessment is null || assessment.ClientId != clientId.Value)
            return InitialAssessmentStoreResult.For(
                InitialAssessmentStoreResult.Status.AssessmentNotFound
            );

        var previousUpdatedAt = assessment.UpdatedAt;
        assessment.Update(
            weightKg,
            heightCm,
            bodyFatPercentage,
            medicalConditions,
            fitnessLevel,
            activityLevel,
            goals,
            profession,
            bodyMeasurements,
            nutritionIntake,
            now
        );

        if (assessment.UpdatedAt == previousUpdatedAt)
            return InitialAssessmentStoreResult.For(
                InitialAssessmentStoreResult.Status.AlreadyInRequestedState,
                assessment
            );

        await _dbContext.SaveChangesAsync(cancellationToken);
        return InitialAssessmentStoreResult.For(
            InitialAssessmentStoreResult.Status.Updated,
            assessment
        );
    }

    private Task<Client?> LockClientAsync(
        Guid trainerId,
        Guid clientId,
        CancellationToken cancellationToken) =>
        _dbContext.Clients
            .FromSqlInterpolated($"""
                SELECT *
                FROM clients
                WHERE owner_trainer_id = {trainerId}
                    AND id = {clientId}
                    AND is_deleted = false
                FOR UPDATE
            """)
            .SingleOrDefaultAsync(cancellationToken);

    private Task<InitialAssessment?> LockAssessmentAsync(
        Guid trainerId,
        Guid assessmentId,
        CancellationToken cancellationToken) =>
        _dbContext.InitialAssessments
            .FromSqlInterpolated($"""
                SELECT *
                FROM initial_assessments
                WHERE owner_trainer_id = {trainerId}
                    AND id = {assessmentId}
                    AND is_deleted = false
                FOR UPDATE
            """)
            .SingleOrDefaultAsync(cancellationToken);

    private bool IsDuplicateAssessment(DbUpdateException exception) =>
        _constraintTranslator.TryTranslate(
            exception,
            PersistenceOperation.CreateInitialAssessment,
            out var error) &&
        error?.Code == "initial_assessment_already_exists";

    private async Task<InitialAssessmentStoreResult> ExecuteTransactionAsync(
        Func<Task<InitialAssessmentStoreResult>> operation,
        CancellationToken cancellationToken)
    {
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _dbContext.Database
                .BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

            var result = await operation();

            if (result.Kind is InitialAssessmentStoreResult.Status.Created or
                InitialAssessmentStoreResult.Status.Updated or
                InitialAssessmentStoreResult.Status.AlreadyInRequestedState)
                await transaction.CommitAsync(cancellationToken);
            else
                await transaction.RollbackAsync(cancellationToken);

            return result;
        });
    }
}
