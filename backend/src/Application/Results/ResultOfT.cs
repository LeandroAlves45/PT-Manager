using Application.Errors;

namespace Application.Results;

/// <summary>
/// Representa o resultado de um caso de uso que devolve um valor.
/// </summary>
/// <typeparam name="TValue">Tipo do payload devolvido em caso de sucesso.</typeparam>
public sealed class Result<TValue> : Result
{
    private readonly TValue _value;
    private Result(bool isSuccess, TValue value, Error? error)
        : base(isSuccess, error)
    {
        _value = value;
    }
    /// <summary>Payload de sucesso. Só pode ser acedido quando IsSuccess é true.</summary>
    public TValue Value
    {
        get
        {
            if (IsFailure)
                throw new InvalidOperationException("Cannot access Value when the result is a failure.");
            return _value;
        }
    }

    /// <summary>Cria um resultado bem sucedido com payload.</summary>
    public static Result<TValue> Success(TValue value) => new Result<TValue>(true, value, null);

    /// <summary>Cria um resultado falhado sem payload acessível.</summary>
    public static new Result<TValue> Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error, nameof(error));

        return new Result<TValue>(false, default!, error);
    }
}
