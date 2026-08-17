using Application.Errors;
using Npgsql;

namespace Infrastructure.Persistence.Errors;

/// <summary>
/// Traduz violações PostgreSQL conhecidas para erros funcionais seguros.
/// Constraints desconhecidas permanecem falhas técnicas.
/// </summary>
internal sealed class PostgresConstraintTranslator
{
    public const string UniqueViolation = "23505";
    public const string ForeignKeyViolation = "23503";

    /// <summary>Tenta traduzir uma exceção no contexto da operação.</summary>
    /// <param name="exception">Exceção de persistência.</param>
    /// <param name="operation">Operação funcional em execução.</param>
    /// <param name="error">Erro seguro quando a tradução é conhecida.</param>
    /// <returns>True apenas quando SQLSTATE, constraint e operação são conhecidos.</returns>
    public bool TryTranslate(
        Exception exception,
        PersistenceOperation operation,
        out Error? error)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var postgresException = FindPostgresException(exception);
        if (postgresException is null)
        {
            error = null;
            return false;
        }

        if (postgresException.SqlState == UniqueViolation &&
            operation is PersistenceOperation.CreateClient or
                PersistenceOperation.UpdateClient)
        {
            if (postgresException.ConstraintName == "uq_clients_tenant_contact_email_active")
            {
                error = Error.Create(
                    code: "client_email_already_exists",
                    category: ErrorCategory.Conflict,
                    description: "A client with this email already exists."
                );
                return true;
            }

            if (postgresException.ConstraintName == "uq_clients_tenant_phone_active")
            {
                error = Error.Create(
                    code: "client_phone_already_exists",
                    category: ErrorCategory.Conflict,
                    description: "A client with this phone already exists."
                );
                return true;
            }
        }

        if (postgresException.SqlState == ForeignKeyViolation &&
            operation is PersistenceOperation.RemoveTrainingPlanStructure &&
            postgresException.ConstraintName == "fk_client_exercise_set_logs_training_plan_day_exercise")
        {
            error = Error.Create(
                code: "training_structure_has_history",
                category: ErrorCategory.Conflict,
                description: "Training structure with execution history cannot be removed."
            );
            return true;
        }

        if (postgresException.SqlState == UniqueViolation &&
            operation is PersistenceOperation.CreateSession or
                PersistenceOperation.RescheduleSession or
                PersistenceOperation.RestoreSession &&
            postgresException.ConstraintName == "uq_sessions_tenant_scheduled_start")
        {
            error = Error.Create(
                code: "session_schedule_conflict",
                category: ErrorCategory.Conflict,
                description: "The personal trainer already has a session at this start time."
            );
            return true;
        }

        error = null;
        return false;
    }

    private static PostgresException? FindPostgresException(Exception exception)
    {
        Exception? current = exception;

        while (current is not null)
        {
            if (current is PostgresException postgresException)
                return postgresException;

            current = current.InnerException;
        }

        return null;
    }
}
