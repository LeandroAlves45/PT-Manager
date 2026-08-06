using System.Collections.Immutable;

namespace Application.Errors;

/// <summary>
/// Descreve uma falha esperada de um caso de uso através de um código estável,
/// uma categoria e uma descrição segura e metadados mínimos.
/// </summary>
public sealed class Error
{
    /// <summary>Código estável consumível por clientes e testes.</summary>
    public string Code { get; }

    /// <summary>Categoria independente do protocolo de transporte.</summary>
    public ErrorCategory Category { get; }

    /// <summary>Descrição segura para o consumidor.</summary>
    public string Description { get; }

    /// <summary>Erros por campo. Só é preenchido para Validation.</summary>
    public IReadOnlyList<ValidationError> ValidationErrors { get; }

    /// <summary>Metadados não sensíveis estritamente necessários para o consumidor.</summary>
    public IReadOnlyDictionary<string, object?> Metadata { get; }

    private Error(
        string code,
        ErrorCategory category,
        string description,
        IReadOnlyList<ValidationError> validationErrors,
        IReadOnlyDictionary<string, object?> metadata)
    {
        Code = code;
        Category = category;
        Description = description;
        ValidationErrors = validationErrors;
        Metadata = metadata;
    }

    /// <summary>Cria um erro não associado a campos.</summary>
    public static Error Create(
        string code,
        ErrorCategory category,
        string description,
        IReadOnlyDictionary<string, object?>? metadata = null)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Code cannot be null or whitespace.", nameof(code));
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Description cannot be null or whitespace.", nameof(description));

        if (category == ErrorCategory.Validation)
            throw new ArgumentException("Use Validation method to create validation errors.");

        IReadOnlyDictionary<string, object?> immutableMetadata = metadata is null
            ? ImmutableDictionary<string, object?>.Empty
            : metadata.ToImmutableDictionary();

        return new Error(
            code: code,
            category: category,
            description: description,
            validationErrors: ImmutableList<ValidationError>.Empty,
            metadata: immutableMetadata
        );
    }

    /// <summary>Cria um erro de validação com uma ou mais falhas por campo.</summary>
    public static Error Validation(IReadOnlyList<ValidationError> errors)
    {
        if (errors is null || errors.Count == 0)
            throw new ArgumentException("Validation errors cannot be null or empty.");

        foreach (var error in errors)
        {
            if (error is null)
                throw new ArgumentException("Validation error cannot be null.");
            if (string.IsNullOrWhiteSpace(error.Field) ||
                string.IsNullOrWhiteSpace(error.Code) ||
                string.IsNullOrWhiteSpace(error.Message))
                throw new ArgumentException("Validation error fields cannot be null or whitespace.");
        }

        var immutableErrors = errors.ToImmutableList();
        return new Error(
            code: "validation_failed",
            category: ErrorCategory.Validation,
            description: "One or more validation errors occurred.",
            validationErrors: immutableErrors,
            metadata: ImmutableDictionary<string, object?>.Empty
        );
    }
}
