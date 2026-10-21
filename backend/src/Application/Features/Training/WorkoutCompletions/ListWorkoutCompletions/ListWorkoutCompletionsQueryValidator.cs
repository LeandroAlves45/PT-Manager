using Application.Validation;
using FluentValidation;

namespace Application.Features.Training.WorkoutCompletions.ListWorkoutCompletions;

/// <summary>Valida o cliente, o plano opcional e a paginação.</summary>
public sealed class ListWorkoutCompletionsQueryValidator : AbstractValidator<ListWorkoutCompletionsQuery>
{
    public ListWorkoutCompletionsQueryValidator()
    {
        RuleFor(query => query.ClientId)
            .NotEmpty().WithErrorCode("training_client_id_required");

        RuleFor(query => query.TrainingPlanId)
            .NotEqual(Guid.Empty).When(query => query.TrainingPlanId.HasValue)
            .WithErrorCode("training_plan_id_invalid");

        this.ApplyPaginationRules(
            query => query.PageNumber,
            query => query.PageSize
        );
    }
}
