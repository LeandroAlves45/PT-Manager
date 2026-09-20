namespace Application.Common.Time;

/// <summary>Intervalo UTC meio-aberto [startUtc, endUtc).</summary>
public readonly record struct UtcRange(DateTimeOffset StartUtc, DateTimeOffset EndUtc);

/// <summary>
/// Conversões entre instantes UTC e dias locais de um fuso IANA. Centraliza o cálculo que
/// antes vivia privado no <c>SessionStore</c> para que sessões, dashboard, resumo e portal
/// usem a mesma regra de "dia local" (incluindo dias de 23 h/25 h na mudança de hora).
/// </summary>
public static class LocalDates
{
    /// <summary>Dia local correspondente a um instante UTC.</summary>
    public static DateOnly ToLocalDate(DateTime utcInstant, TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(timeZone);

        // O Npgsql devolve timestamptz com Kind=Utc; um Kind diferente só aparece em testes
        // e é tratado como UTC para nunca aplicar o fuso da máquina.
        var utc = utcInstant.Kind == DateTimeKind.Utc
            ? utcInstant
            : DateTime.SpecifyKind(utcInstant, DateTimeKind.Utc);

        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utc, timeZone));
    }

    /// <summary>Dia local correspondente a um instante com offset.</summary>
    public static DateOnly ToLocalDate(DateTimeOffset instant, TimeZoneInfo timeZone) =>
        ToLocalDate(instant.UtcDateTime, timeZone);

    /// <summary>Dia local de hoje para o relógio do servidor.</summary>
    public static DateOnly Today(DateTime utcNow, TimeZoneInfo timeZone) =>
        ToLocalDate(utcNow, timeZone);

    /// <summary>
    /// Converte os dias locais <c>[fromDate, toDateExclusive)</c> num intervalo UTC
    /// meio-aberto, pronto para filtrar colunas <c>timestamptz</c>.
    /// </summary>
    public static UtcRange ToUtcRange(
        DateOnly fromDate,
        DateOnly toDateExclusive,
        TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(timeZone);

        var localStart = fromDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var localEnd = toDateExclusive.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);

        return new UtcRange(
            new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localStart, timeZone), TimeSpan.Zero),
            new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localEnd, timeZone), TimeSpan.Zero)
        );
    }
}
