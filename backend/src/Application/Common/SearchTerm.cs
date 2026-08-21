namespace Application.Common;

/// <summary>Normaliza termos de pesquisa recebidos pelas queries de listagem.</summary>
public static class SearchTerm
{
    /// <summary>
    /// Devpolve null quando o termo é nulo, vazio ou whitespace; caso contrário
    /// devolve o termo aplicado.
    /// </summary>
    public static string? Normalize(string? search) =>
        string.IsNullOrWhiteSpace(search) ? null : search.Trim();
}
