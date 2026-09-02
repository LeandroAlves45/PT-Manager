namespace Api.FunctionalTests.Support;

/// <summary>Forma mínima de Problem Details para asserções nos testes funcionais.</summary>
public sealed record ProblemDetailsPayload(
    string? Title,
    int? Status,
    string? Detail,
    string? Instance,
    IReadOnlyList<ValidationErrorPayload>? Errors)
{
    /// <summary>Erros de validação vazios quando a resposta não é de validação.</summary>
    public IReadOnlyList<ValidationErrorPayload> Errors { get; init; } =
        Errors ?? Array.Empty<ValidationErrorPayload>();
}

/// <summary>Erro de validação por campo, tal como serializado pelo mapper.</summary>
public sealed record ValidationErrorPayload(string Field, string Code, string Message);

/// <summary>Forma do envelope de paginação, para asserções nos testes.</summary>
public sealed record PagedPayload<TItem>(
    IReadOnlyList<TItem> Items,
    int TotalCount,
    int PageNumber,
    int PageSize);
