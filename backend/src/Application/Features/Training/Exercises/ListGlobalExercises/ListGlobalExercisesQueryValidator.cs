using Application.Validation;
using FluentValidation;

namespace Application.Features.Training.Exercises.ListGlobalExercises;

/// <summary>Valida pesquisa e paginação administrativa.</summary>
public sealed class ListGlobalExercisesQueryValidator
    : AbstractValidator<ListGlobalExercisesQuery>
{
    public ListGlobalExercisesQueryValidator()
    {
        RuleFor(query => query.Search)
            .MaximumLength(255)
            .WithErrorCode("exercise_search_too_long");

        RuleFor(query => query.Activity)
            .IsInEnum()
            .WithErrorCode("exercise_activity_invalid");

        this.ApplyPaginationRules(
            query => query.PageNumber,
            query => query.PageSize
        );
    }
}
