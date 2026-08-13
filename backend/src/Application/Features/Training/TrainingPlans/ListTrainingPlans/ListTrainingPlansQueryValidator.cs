using Application.Validation;
using FluentValidation;

namespace Application.Features.Training.TrainingPlans.ListTrainingPlans;

/// <summary>Valida a listagem de planos de treino.</summary>
public sealed class ListTrainingPlansQueryValidator : AbstractValidator<ListTrainingPlansQuery>
{
    public ListTrainingPlansQueryValidator()
    {
        RuleFor(query => query.ClientId)
            .NotEqual(Guid.Empty)
            .When(query => query.ClientId.HasValue)
            .WithErrorCode("training_client_id_required");
        RuleFor(query => query.Search)
            .MaximumLength(200)
            .WithErrorCode("training_search_too_long");
        RuleFor(query => query.Activity)
            .IsInEnum()
            .WithErrorCode("training_activity_invalid");
        this.ApplyPaginationRules(
            query => query.PageNumber,
            query => query.PageSize
        );
    }
}
