using Application.Common.Abstractions;

namespace Infrastructure.Media.Cloudinary;

/// <summary>Deriva a pasta de destino de um asset a partir de dados confiáveis.</summary>
internal sealed class CloudinaryFolderNaming
{
    internal static string FolderFor(string folderRoot, MediaAssetKind kind, Guid trainerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderRoot);
        if (trainerId == Guid.Empty)
            throw new ArgumentException("Trainer identifier is required.", nameof(trainerId));

        var leaf = kind switch
        {
            MediaAssetKind.TrainerLogo => "logos",
            MediaAssetKind.ClientAvatar => "avatars",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown media asset kind.")
        };

        return $"{folderRoot}/trainers/{trainerId:N}/{leaf}";
    }

    /// <summary>
    /// Indica se um identificador pertence ao espaço do tenant indicado. O
    /// adapter recusa eliminar qualquer asset fora dele: uma mensagem de outbox
    /// adulterada nunca consegue apagar conteúdo de outro personal trainer nem conteúdo
    /// da conta que não seja gerido por esta aplicação.
    /// </summary>
    internal static bool IsOwnedBy(string folderRoot, Guid trainerId, string publicId) =>
        trainerId != Guid.Empty &&
        publicId.StartsWith($"{folderRoot}/trainers/{trainerId:N}/", StringComparison.Ordinal) &&
        !publicId.Contains("..", StringComparison.Ordinal);
}
