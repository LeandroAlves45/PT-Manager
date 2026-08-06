using Application.Errors;
using FluentValidation.Results;

namespace Application.Validation;

/// <summary>
/// Converte falhas do FluentValidation para o contrato de validação da Application,
/// preservando todos os erros por campo e removendo detalhes internos.
/// </summary>
public static class ValidationResultExtension
{
    public static Error ToApplicationError(this ValidationResult validationResult)
    {
        ArgumentNullException.ThrowIfNull(validationResult, nameof(validationResult));
        if (validationResult.IsValid)
            throw new ArgumentException("ValidationResult must represent a failure.");

        var validationErrors = new List<ValidationError>();

        foreach (var failure in validationResult.Errors)
        {
            validationErrors.Add(new ValidationError(
                Field: failure.PropertyName,
                Code: failure.ErrorCode,
                Message: failure.ErrorMessage
            ));
        }

        return Error.Validation(validationErrors);
    }
}
