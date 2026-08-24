using Application.Errors;
using Npgsql;

namespace Infrastructure.Persistence.Errors;

/// <summary>Traduz violações PostgreSQL conhecidas em erros funcionais seguros.</summary>
internal sealed class PostgresConstraintTranslator
{
    public const string UniqueViolation = "23505";
    public const string ForeignKeyViolation = "23503";

    /// <summary>Tenta traduzir uma exceção no contexto da operação.</summary>
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

        // Unique Violations
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

        if (postgresException.SqlState == UniqueViolation &&
            operation is PersistenceOperation.ReactivateClient &&
            postgresException.ConstraintName == "uq_clients_user_active")
        {
            error = Error.Create(
                code: "client_user_already_has_active_relationship",
                category: ErrorCategory.Conflict,
                description: "The user already has an active client relationship."
            );
            return true;
        }

        if (postgresException.SqlState == UniqueViolation &&
            operation is PersistenceOperation.EnqueueNotification &&
            postgresException.ConstraintName == "unique_idempotency_key")
        {
            error = Error.Create(
                code: "notification_already_queued",
                category: ErrorCategory.Conflict,
                description: "The notification operation was already queued."
            );
            return true;
        }

        if (postgresException.SqlState == UniqueViolation &&
            (operation is PersistenceOperation.CreateSession or
                PersistenceOperation.RescheduleSession or
                PersistenceOperation.RestoreSession) &&
            postgresException.ConstraintName == "uq_sessions_tenant_scheduled_start")
        {
            error = Error.Create(
                code: "session_schedule_conflict",
                category: ErrorCategory.Conflict,
                description: "The personal trainer already has a session at this start time."
            );
            return true;
        }

        if (postgresException.SqlState == UniqueViolation &&
            operation is PersistenceOperation.CreateInitialAssessment &&
            postgresException.ConstraintName == "uq_initial_assessments_tenant_client_active")
        {
            error = Error.Create(
                code: "initial_assessment_already_exists",
                category: ErrorCategory.Conflict,
                description: "The client already has an initial assessment."
            );
            return true;
        }

        if (postgresException.SqlState == UniqueViolation &&
            (operation is PersistenceOperation.CreateCheckIn or
                PersistenceOperation.RescheduleCheckIn) &&
            postgresException.ConstraintName == "uq_checkins_tenant_client_date_active")
        {
            error = Error.Create(
                code: "check_in_date_conflict",
                category: ErrorCategory.Conflict,
                description: "The client already has a check-in on that date."
            );
            return true;
        }

        if (postgresException.SqlState == UniqueViolation &&
            (operation is PersistenceOperation.AssignSupplement or
                PersistenceOperation.ReactivateSupplementAssignment) &&
            postgresException.ConstraintName == "uq_client_supplement_active")
        {
            error = Error.Create(
                code: "supplement_assignment_already_exists",
                category: ErrorCategory.Conflict,
                description: "The client already has an active assignment for this supplement."
            );
            return true;
        }

        // Foreign Key Violations
        if (postgresException.SqlState == ForeignKeyViolation &&
            operation is PersistenceOperation.DeleteGlobalSupplement &&
            postgresException.ConstraintName is
                "fk_client_supplement_assignments_supplement" or
                "fk_meal_plan_meal_supplements_supplement")
        {
            error = Error.Create(
                code: "global_supplement_has_references",
                category: ErrorCategory.Conflict,
                description: "A referenced global supplement cannot be deleted."
            );
            return true;
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

        if (postgresException.SqlState == ForeignKeyViolation &&
            operation is PersistenceOperation.DeleteGlobalFood &&
            postgresException.ConstraintName == "fk_meal_plan_meal_items_food")
        {
            error = Error.Create(
                code: "global_food_has_references",
                category: ErrorCategory.Conflict,
                description: "A referenced global food cannot be deleted."
            );
            return true;
        }

        if (postgresException.SqlState == ForeignKeyViolation &&
            operation is PersistenceOperation.DeleteGlobalExercise &&
            postgresException.ConstraintName == "fk_training_plan_day_exercises_exercise")
        {
            error = Error.Create(
                code: "global_exercise_has_references",
                category: ErrorCategory.Conflict,
                description: "A referenced global exercise cannot be deleted."
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
