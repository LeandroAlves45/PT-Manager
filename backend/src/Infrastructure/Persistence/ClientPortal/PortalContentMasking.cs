namespace Infrastructure.Persistence.ClientPortal;

/// <summary>
/// Marcadores neutros com que o portal substitui conteúdo bloqueado pela moderação.
/// </summary>
internal static class PortalContentMasking
{
    /// <summary>Nome apresentado em vez do exercício bloqueado.</summary>
    internal const string UnavailableExerciseName = "Unavailable exercise";

    /// <summary>Nome apresentado em vez do alimento bloqueado.</summary>
    internal const string UnavailableFoodName = "Unavailable food";
}
