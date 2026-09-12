namespace Infrastructure.Media.Cloudinary;

/// <summary>Configuração validada do storage de imagens Cloudinary.</summary>
public sealed class CloudinaryOptions
{
    public const string SectionName = "Cloudinary";

    public bool Enabled { get; init; }
    public string? CloudName { get; init; }
    public string? ApiKey { get; init; }
    public string? ApiSecret { get; init; }

    /// <summary>
    /// Pasta raiz de todos os assets geridos, por exemplo <c>pt-manager/dev</c>.
    /// Separa ambientes que partilhem a mesma conta e delimita o que o adapter
    /// aceita eliminar.
    /// </summary>
    public string FolderRoot { get; init; } = "pt-manager";

    public Uri BaseAddress { get; init; } = new("https://api.cloudinary.com/");
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(20);
}
