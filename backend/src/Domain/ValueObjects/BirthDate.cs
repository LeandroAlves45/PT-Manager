using Domain.Exceptions;

namespace Domain.ValueObjects;

/// <summary>
/// Data de nascimento canónica do cliente.
/// </summary>
public sealed record BirthDate
{
    public DateOnly Value { get; }

    private BirthDate(DateOnly value)
    {
        if (value == default)
            throw new DomainException("Birth date is required.");

        Value = value;
    }

    /// <summary>Cria uma data de nascimento válida para a data de referência.</summary>
    public static BirthDate Create(DateOnly value, DateOnly today)
    {
        if (value > today)
            throw new DomainException("Birth date cannot be in the future.");

        return new BirthDate(value);
    }

    /// <summary>
    /// Reconstrói o VO a partir de um valor já validado e persistido.
    /// A validação temporal continua a ocorrer nas escritas de agregado.
    /// </summary>
    public static BirthDate FromPersisted(DateOnly value) => new(value);

    /// <summary>Calcula anos completos na data indicada.</summary>
    public int CalculateAge(DateOnly onDate)
    {
        if (onDate < Value)
            throw new DomainException("Age reference date cannot be before birth date.");

        var age = onDate.Year - Value.Year;
        if (onDate < Value.AddYears(age))
            age--;

        return age;
    }
}
