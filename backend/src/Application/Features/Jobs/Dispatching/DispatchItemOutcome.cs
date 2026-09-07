namespace Application.Features.Jobs.Dispatching;

/// <summary>Classifica o resultado de um handler executado pelo dispatcher.</summary>
public enum DispatchItemOutcomeKind
{
    Succeeded,

    /// <summary>A operação pode ser repetida com a mesma chave de idempotência.</summary>
    TransientFailure,

    /// <summary>A repetição pode não produzir sucesso sem alterar dados ou código.</summary>
    PermanentFailure,

    LeaseLost
}

/// <summary>Resultado seguro e independente do transporte de uma execução.</summary>
public sealed record DispatchItemOutcome
{
    private DispatchItemOutcome(DispatchItemOutcomeKind kind, string? failureCode)
    {
        if (kind is DispatchItemOutcomeKind.TransientFailure or
            DispatchItemOutcomeKind.PermanentFailure)
        {
            if (string.IsNullOrWhiteSpace(failureCode))
                throw new ArgumentException("A failure code is required.", nameof(failureCode));

            if (failureCode.Length > 100)
                throw new ArgumentException(
                    "A failure code cannot exceed 100 characters.", nameof(failureCode));
        }
        else if (failureCode is not null)
            throw new ArgumentException(
                "A non-failure outcome cannot contain a failure code.", nameof(failureCode));

        Kind = kind;
        FailureCode = failureCode;
    }

    public DispatchItemOutcomeKind Kind { get; }
    public string? FailureCode { get; }

    public static DispatchItemOutcome Succeeded() => new(DispatchItemOutcomeKind.Succeeded, null);

    public static DispatchItemOutcome TransientFailure(string failureCode) =>
        new(DispatchItemOutcomeKind.TransientFailure, failureCode);

    public static DispatchItemOutcome PermanentFailure(string failureCode) =>
        new(DispatchItemOutcomeKind.PermanentFailure, failureCode);

    public static DispatchItemOutcome LeaseLost() => new(DispatchItemOutcomeKind.LeaseLost, null);
}
