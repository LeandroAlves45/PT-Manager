namespace Infrastructure.Media.Imaging;

/// <summary>Lê um stream não confiável para a memória com um teto rígido de bytes.</summary>
internal static class BoundedStreamReader
{
    /// <summary>Lê no máximo <paramref name="maxBytes"/> bytes do stream.</summary>
    internal static async Task<byte[]?> ReadAsync(
        Stream source,
        long maxBytes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBytes);

        // Capacidade inicial modesta: o buffer cresce conforme o conteúdo real,
        // para que um Content-Length mentiroso não permita reservar memória.
        using var buffer = new MemoryStream();

        var rented = new byte[81920];
        long total = 0;

        while (true)
        {
            var read = await source.ReadAsync(rented, cancellationToken);
            if (read == 0)
                break;

            total += read;
            if (total > maxBytes)
                return null;

            await buffer.WriteAsync(rented.AsMemory(0, read), cancellationToken);
        }

        return buffer.ToArray();
    }
}
