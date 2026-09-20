using Application.Validation;
using FluentValidation;

namespace Application.Features.Administration.ContentModeration.ListModerationQueue;

/// <summary>Valida tipo de conteúdo, estado, pesquisa e paginação.</summary>
public sealed class ListModerationQueueQueryValidator : AbstractValidator<ListModerationQueueQuery>
{
    public ListModerationQueueQueryValidator()
    {
        RuleFor(query => query.Kind)
            .IsInEnum()
            .WithErrorCode("moderation_kind_invalid")
            .WithMessage("Kind must be Food or Exercise");

        RuleFor(query => query.Status)
            .IsInEnum()
            .WithErrorCode("moderation_status_invalid")
            .WithMessage("Status must be All, Allowed or Blocked");

        RuleFor(query => query.Search)
            .MaximumLength(255)
            .WithErrorCode("moderation_search_too_long")
            .WithMessage("Search cannot exceed 255 characters");

        this.ApplyPaginationRules(
            query => query.PageNumber,
            query => query.PageSize);
    }
}
