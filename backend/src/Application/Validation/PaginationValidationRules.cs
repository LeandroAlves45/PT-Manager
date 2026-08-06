using FluentValidation;
using System.Linq.Expressions;

namespace Application.Validation;

/// <summary>Regras de paginação reutilizadas pelos validadores de listagem.</summary>
internal static class PaginationValidationRules
{
    /// <summary>Aplica limites canónicos de página e tamanho a um validator existente.</summary>
    /// <typeparam name="T">Tipo da query validada.</typeparam>
    /// <param name="validator">Validator que recebe as regras.</param>
    /// <param name="pageNumberSelector">Seleciona PageNumber.</param>
    /// <param name="pageSizeSelector">Seleciona PageSize.</param>
    internal static void ApplyPaginationRules<T>(
        this AbstractValidator<T> validator,
        Expression<Func<T, int>> pageNumberSelector,
        Expression<Func<T, int>> pageSizeSelector)
    {
        if (validator is null || pageNumberSelector is null || pageSizeSelector is null)
            throw new ArgumentNullException(
                validator is null ? nameof(validator) :
                pageNumberSelector is null ? nameof(pageNumberSelector) :
                nameof(pageSizeSelector));

        validator.RuleFor(pageNumberSelector)
            .GreaterThanOrEqualTo(1)
            .WithErrorCode("page_number_invalid")
            .WithMessage("Page number must be greater than or equal to one.");

        validator.RuleFor(pageSizeSelector)
            .InclusiveBetween(1, 100)
            .WithErrorCode("page_size_invalid")
            .WithMessage("Page size must contain between 1 and 100 items.");
    }
}
