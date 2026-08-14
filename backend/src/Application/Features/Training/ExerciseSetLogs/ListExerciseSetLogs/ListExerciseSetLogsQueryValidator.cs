using Application.Validation;
using FluentValidation;

namespace Application.Features.Training.ExerciseSetLogs.ListExerciseSetLogs;

/// <summary>Valida o intervalo e a paginação dos logs.</summary>
public sealed class ListExerciseSetLogsQueryValidator : AbstractValidator<ListExerciseSetLogsQuery>
{
    public ListExerciseSetLogsQueryValidator()
    {
        RuleFor(query => query.ClientId)
            .NotEmpty().WithErrorCode("training_client_id_required");

        RuleFor(query => query.TrainingPlanId)
            .NotEqual(Guid.Empty).When(query => query.TrainingPlanId.HasValue)
            .WithErrorCode("training_plan_id_invalid");

        RuleFor(query => query)
            .Must(query => !query.PerformedFrom.HasValue ||
                !query.PerformedTo.HasValue ||
                query.PerformedFrom <= query.PerformedTo)
            .WithName("PerformedTo")
            .WithErrorCode("training_log_date_range_invalid");

        this.ApplyPaginationRules(
            query => query.PageNumber,
            query => query.PageSize
        );
    }
}
