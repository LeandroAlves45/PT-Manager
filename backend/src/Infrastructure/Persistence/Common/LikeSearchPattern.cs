namespace Infrastructure.Persistence.Common;

/// <summary>
/// Constrói o padrão ILIKE escapado partilhado por todas as queries
/// com pesquisa de texto livre.
/// </summary>
internal static class LikeSearchPattern
{
    public const string LikeEscapeCharacter = "\\";

    public static string Build(string search) => $"%{Escape(search.Trim())}%";

    private static string Escape(string value) => value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("%", "\\%", StringComparison.Ordinal)
        .Replace("_", "\\_", StringComparison.Ordinal);
}
