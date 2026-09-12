using Application.Common.Abstractions;
using SkiaSharp;

namespace Infrastructure.Media.Imaging;

/// <summary>Implementa IImageProcessor sobre SkiaSharp.</summary>
/// <remarks>
/// <para>
/// A descodificação é a única prova de que o conteúdo é uma imagem: o
/// Content-Type e o tamanho declarados na fronteira HTTP são afirmações do
/// cliente. Um SVG com XXE, um ZIP renomeado ou um PDF falham aqui.
/// </para>
/// </remarks>
internal sealed class SkiaImageProcessor : IImageProcessor
{
    public async Task<ImageProcessingResult> NormalizeAsync(
        MediaUpload upload,
        ImageProfile profile,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(upload);
        ArgumentNullException.ThrowIfNull(profile);

        var bytes = await BoundedStreamReader.ReadAsync(
            upload.Content,
            profile.MaxBytes,
            cancellationToken);

        if (bytes is null)
            return ImageProcessingResult.Invalid(ImageValidationFailure.TooLarge);
        if (bytes.Length == 0)
            return ImageProcessingResult.Invalid(ImageValidationFailure.Empty);

        cancellationToken.ThrowIfCancellationRequested();

        return Normalize(bytes, upload.ContentType, profile);
    }

    private static ImageProcessingResult Normalize(
        byte[] bytes,
        string declaredContentType,
        ImageProfile profile)
    {
        using var data = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(data);
        if (codec is null)
            return ImageProcessingResult.Invalid(ImageValidationFailure.NotDecodable);

        var actualContentType = ContentTypeFor(codec.EncodedFormat);
        if (actualContentType is null)
            return ImageProcessingResult.Invalid(ImageValidationFailure.UnsupportedFormat);

        if (!string.Equals(actualContentType, declaredContentType, StringComparison.OrdinalIgnoreCase))
            return ImageProcessingResult.Invalid(ImageValidationFailure.ContentTypeMismatch);

        var info = codec.Info;
        if (info.Width <= 0 || info.Height <= 0)
            return ImageProcessingResult.Invalid(ImageValidationFailure.NotDecodable);

        if (info.Width < profile.MinDimension || info.Height < profile.MinDimension)
            return ImageProcessingResult.Invalid(ImageValidationFailure.DimensionsTooSmall);

        if (info.Width > profile.MaxDimension || info.Height > profile.MaxDimension)
            return ImageProcessingResult.Invalid(ImageValidationFailure.DimensionsTooLarge);

        // Multiplicação em long: 6000 * 6000 cabe em int, mas o produto de dois
        // valores próximos do máximo não caberia se o perfil subisse.
        if ((long)info.Width * info.Height > profile.MaxPixels)
            return ImageProcessingResult.Invalid(ImageValidationFailure.PixelBudgetExceeded);

        // SKBitmap.Decode(codec) não serve aqui: perante um ficheiro truncado
        // devolve um bitmap parcialmente preenchido e engole o erro, pelo que uma
        // imagem corrompida seria publicada como imagem valida. GetPixels expoe o
        // SKCodecResult, e tudo o que nao seja Success e recusado.
        var decodeInfo = new SKImageInfo(
            info.Width,
            info.Height,
            SKColorType.Rgba8888,
            SKAlphaType.Premul);

        using var bitmap = new SKBitmap(decodeInfo);
        var decodeResult = codec.GetPixels(decodeInfo, bitmap.GetPixels());
        if (decodeResult != SKCodecResult.Success)
            return ImageProcessingResult.Invalid(ImageValidationFailure.NotDecodable);

        var (targetWidth, targetHeight) = FitInside(
            bitmap.Width,
            bitmap.Height,
            profile.OutputMaxDimension);

        var encoded = Encode(bitmap, targetWidth, targetHeight, profile);
        if (encoded is null)
            return ImageProcessingResult.Invalid(ImageValidationFailure.EncodingFailed);

        return ImageProcessingResult.Ok(
            new ProcessedImage(
                encoded,
                profile.OutputContentType,
                targetWidth,
                targetHeight
            )
        );
    }

    private static byte[]? Encode(
        SKBitmap bitmap,
        int targetWidth,
        int targetHeight,
        ImageProfile profile)
    {
        var imageInfo = new SKImageInfo(
            targetWidth,
            targetHeight,
            SKColorType.Rgba8888,
            SKAlphaType.Premul);

        using var surface = SKSurface.Create(imageInfo);
        if (surface is null)
            return null;

        // Transparente e não branco: um logo com canal alfa não pode ganhar um
        // fundo sólido só por atravessar o pipeline.
        surface.Canvas.Clear(SKColors.Transparent);
        surface.Canvas.DrawBitmap(
            bitmap,
            new SKRect(0, 0, targetWidth, targetHeight),
            new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
        surface.Canvas.Flush();

        using var image = surface.Snapshot();
        using var encoded = image.Encode(FormatFor(profile.OutputContentType), profile.OutputQuality);

        return encoded?.ToArray();
    }

    /// <summary>
    /// Calcula a caixa de destino preservando o rácio. Nunca faz upscale: uma
    /// imagem de 100x100 sai 100x100, porque ampliar não acrescenta informação e
    /// só aumenta o custo de armazenamento e de transferência.
    /// </summary>
    private static (int Width, int Height) FitInside(
        int width,
        int height,
        int maxDimension)
    {
        if (width <= maxDimension && height <= maxDimension)
            return (width, height);

        var scale = Math.Min(
            (double)maxDimension / width,
            (double)maxDimension / height);

        return (
            Math.Max(1, (int)Math.Round(width * scale)),
            Math.Max(1, (int)Math.Round(height * scale)));
    }

    private static string? ContentTypeFor(SKEncodedImageFormat format) => format switch
    {
        SKEncodedImageFormat.Jpeg => "image/jpeg",
        SKEncodedImageFormat.Png => "image/png",
        SKEncodedImageFormat.Webp => "image/webp",
        _ => null,
    };

    private static SKEncodedImageFormat FormatFor(string contentType) => contentType switch
    {
        "image/png" => SKEncodedImageFormat.Png,
        "image/jpeg" => SKEncodedImageFormat.Jpeg,
        _ => SKEncodedImageFormat.Webp,
    };
}
