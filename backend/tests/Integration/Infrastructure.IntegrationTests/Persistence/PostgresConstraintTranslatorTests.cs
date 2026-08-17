using Application.Errors;
using Infrastructure.Persistence.Errors;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Infrastructure.IntegrationTests.Persistence;

/// <summary>Verifica mapping exato por SQLSTATE, constraint e operação.</summary>
public sealed class PostgresConstraintTranslatorTests
{
    private readonly PostgresConstraintTranslator _translator = new();

    [Theory]
    [InlineData("uq_clients_tenant_contact_email_active", "client_email_already_exists")]
    [InlineData("uq_clients_tenant_phone_active", "client_phone_already_exists")]
    public void KnownClientUniqueConstraint_MapsExpectedCode(
        string constraintName,
        string expectedCode)
    {
        var exception = CreatePostgresException("23505", constraintName);

        var translated = _translator.TryTranslate(
            exception,
            PersistenceOperation.CreateClient,
            out var error);

        Assert.True(translated);
        Assert.Equal(expectedCode, error!.Code);
        Assert.Equal(ErrorCategory.Conflict, error.Category);
    }

    [Fact]
    public void KnownConstraint_WrongOperation_ReturnsFalse()
    {
        var exception = CreatePostgresException(
            "23505",
            "uq_clients_tenant_contact_email_active");

        var translated = _translator.TryTranslate(
            exception,
            PersistenceOperation.RemoveTrainingPlanStructure,
            out var error);

        Assert.False(translated);
        Assert.Null(error);
    }

    [Fact]
    public void UnknownConstraint_ReturnsFalseAndNullError()
    {
        var exception = CreatePostgresException("23505", "uq_unknown");

        var translated = _translator.TryTranslate(
            exception,
            PersistenceOperation.CreateClient,
            out var error);

        Assert.False(translated);
        Assert.Null(error);
    }

    [Fact]
    public void NestedPostgresException_IsFoundAcrossInnerExceptionChain()
    {
        var postgresException = CreatePostgresException(
            "23505",
            "uq_clients_tenant_phone_active");
        var wrapper = new InvalidOperationException("wrapper", postgresException);
        var exception = new DbUpdateException("save failed", wrapper);

        var translated = _translator.TryTranslate(
            exception,
            PersistenceOperation.UpdateClient,
            out var error);

        Assert.True(translated);
        Assert.Equal("client_phone_already_exists", error!.Code);
    }

    [Fact]
    public void GenericForeignKeyViolation_ReturnsFalse()
    {
        var exception = CreatePostgresException("23503", "fk_unknown");

        var translated = _translator.TryTranslate(
            exception,
            PersistenceOperation.CreateClient,
            out var error);

        Assert.False(translated);
        Assert.Null(error);
    }

    [Fact]
    public void PlannedTrainingHistoryConstraint_MapsOnlyRemovalContext()
    {
        var exception = CreatePostgresException(
            "23503",
            "fk_client_exercise_set_logs_training_plan_day_exercise");

        var removalTranslated = _translator.TryTranslate(
            exception,
            PersistenceOperation.RemoveTrainingPlanStructure,
            out var removalError);
        var createTranslated = _translator.TryTranslate(
            exception,
            PersistenceOperation.CreateClient,
            out var createError);

        Assert.True(removalTranslated);
        Assert.Equal("training_structure_has_history", removalError!.Code);
        Assert.False(createTranslated);
        Assert.Null(createError);
    }

    [Theory]
    [InlineData(nameof(PersistenceOperation.CreateSession))]
    [InlineData(nameof(PersistenceOperation.RescheduleSession))]
    [InlineData(nameof(PersistenceOperation.RestoreSession))]
    public void SessionScheduleConstraint_MapsExpectedCode(string operationName)
    {
        var exception = CreatePostgresException(
            "23505",
            "uq_sessions_tenant_scheduled_start");
        var operation = Enum.Parse<PersistenceOperation>(operationName);

        var translated = _translator.TryTranslate(
            exception,
            operation,
            out var error);

        Assert.True(translated);
        Assert.Equal("session_schedule_conflict", error!.Code);
        Assert.Equal(ErrorCategory.Conflict, error.Category);
    }

    [Fact]
    public void NullException_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => _translator.TryTranslate(
            null!,
            PersistenceOperation.CreateClient,
            out _));
    }

    private static PostgresException CreatePostgresException(
        string sqlState,
        string constraintName)
    {
        return new PostgresException(
            messageText: "constraint violation",
            severity: "ERROR",
            invariantSeverity: "ERROR",
            sqlState: sqlState,
            detail: null,
            hint: null,
            position: 0,
            internalPosition: 0,
            internalQuery: null,
            where: null,
            schemaName: null,
            tableName: null,
            columnName: null,
            dataTypeName: null,
            constraintName: constraintName,
            file: null,
            line: null,
            routine: null);
    }
}
