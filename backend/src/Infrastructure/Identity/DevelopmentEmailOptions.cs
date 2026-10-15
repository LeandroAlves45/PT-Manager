namespace Infrastructure.Identity;

/// <summary>
/// Opções do email de desenvolvimento. Com <see cref="LogLinks"/> os links de autenticação
/// vão para o log em vez de saírem pelo fornecedor, o que permite percorrer os fluxos sem
/// caixa de correio.
/// </summary>
public sealed class DevelopmentEmailOptions
{
    /// <summary>Nome da secção de configuração.</summary>
    public const string SectionName = "DevelopmentEmail";

    /// <summary>Escreve os links no log em vez de os enviar; só permitido em Development.</summary>
    public bool LogLinks { get; set; }
}
