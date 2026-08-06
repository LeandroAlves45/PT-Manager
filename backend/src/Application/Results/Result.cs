using Application.Errors;

namespace Application.Results;

/// <summary>
/// Representa o resultado sem payload de um caso de uso.
/// Falhas inesperadas são valores; exceções ficam reservadas a defeitos e falhas técnicas inesperadas.
/// </summary>
public class Result
{
    /// <summary>Indica que a operação terminou com sucesso.</summary>
    public bool IsSuccess { get; }

    /// <summary>Indica que a operação terminou com falha esperada.</summary>
    public bool IsFailure { get; }

    /// <summary>Erro da operação. É nulo num resultado bem sucedido.</summary>
    public Error? Error { get; }

    protected Result(bool isSuccess, Error? error)
    {
        if (isSuccess && error is not null)
            throw new ArgumentException("Successful result cannot have an error.");
        if (!isSuccess && error is null)
            throw new ArgumentException("Failed result must have an error.");

        IsSuccess = isSuccess;
        IsFailure = !isSuccess;
        Error = error;
    }

    /// <summary>Cria um resultado bem sucedido sem payload.</summary>
    public static Result Success() => new Result(true, null);

    /// <summary>Cria um resultado de falha esperada.</summary>
    public static Result Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error, nameof(error));

        return new Result(false, error);
    }
}


