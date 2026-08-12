using Application.Validation;
using FluentValidation;

namespace Application.Features.Training.Exercises.ListExercises;

/// <summary>Valida paginação, pesquisa e atividade.</summary>
public sealed class ListExercisesQueryValidator : AbstractValidator<ListExercisesQuery>
{
    public ListExercisesQueryValidator()
    {
        RuleFor(query => query.Search)
            .MaximumLength(200)
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
