using Application.Common.Abstractions;
using Infrastructure.Media.Imaging;
using SkiaSharp;

namespace Infrastructure.IntegrationTests.Media;

/// <summary>
/// Prova o ponto 6 do Gate 5C: EXIF e geolocalização não permanecem no ficheiro
/// publicado.
/// </summary>
/// <remarks>
/// A garantia não vem de remover campos, mas de reencodar a imagem a partir dos
/// píxeis descodificados: os metadados do contentor original nunca chegam ao
/// contentor de saída. Este teste existe para falhar se alguém trocar a
/// reencodificação por uma cópia de bytes ou por uma transformação delegada ao
/// fornecedor.
/// </remarks>
public sealed class SkiaImageProcessorExifTests
{
    private static readonly byte[] ExifSignature = "Exif\0\0"u8.ToArray();

    [Fact]
    public async Task NormalizeAsync_StripsExifAndGeolocation()
    {
        var jpegWithGps = CreateJpegWithGpsExif(1024, 768);

        // O ficheiro de entrada tem mesmo o que dizemos ter: sem esta asserção o
        // teste passaria por vacuidade.
        Assert.True(ContainsApp1Marker(jpegWithGps));
        Assert.True(ContainsSequence(jpegWithGps, ExifSignature));
        Assert.True(ContainsSequence(jpegWithGps, "PTMANAGER_GPS_CANARY"u8.ToArray()));

        using var stream = new MemoryStream(jpegWithGps);
        var result = await new SkiaImageProcessor().NormalizeAsync(
            new MediaUpload(stream, "image/jpeg", jpegWithGps.Length),
            ImageProfiles.ClientAvatar,
            TestContext.Current.CancellationToken);

        var image = Assert.IsType<ProcessedImage>(result.Image);
        var published = image.Content.ToArray();

        Assert.False(ContainsApp1Marker(published));
        Assert.False(ContainsSequence(published, ExifSignature));
        Assert.False(ContainsSequence(published, "PTMANAGER_GPS_CANARY"u8.ToArray()));
    }

    /// <summary>
    /// Constrói um JPEG real e injecta-lhe um segmento APP1 com a assinatura EXIF
    /// e uma marca reconhecível a fazer de coordenada. Um descodificador salta
    /// APP1, por isso a imagem continua válida.
    /// </summary>
    private static byte[] CreateJpegWithGpsExif(int width, int height)
    {
        using var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.SeaGreen);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 92);
        var jpeg = data.ToArray();

        var payload = new List<byte>();
        payload.AddRange(ExifSignature);
        payload.AddRange("MM\0*"u8.ToArray());
        payload.AddRange("PTMANAGER_GPS_CANARY 38.7223N 9.1393W"u8.ToArray());

        var segmentLength = payload.Count + 2;

        var output = new List<byte>(jpeg.Length + segmentLength + 4);
        output.AddRange(jpeg[..2]); // SOI
        output.Add(0xFF);
        output.Add(0xE1); // APP1
        output.Add((byte)((segmentLength >> 8) & 0xFF));
        output.Add((byte)(segmentLength & 0xFF));
        output.AddRange(payload);
        output.AddRange(jpeg[2..]);

        return [.. output];
    }

    private static bool ContainsApp1Marker(byte[] content)
    {
        for (var i = 0; i < content.Length - 1; i++)
        {
            if (content[i] == 0xFF && content[i + 1] == 0xE1)
                return true;
        }

        return false;
    }

    private static bool ContainsSequence(byte[] content, byte[] sequence) =>
        content.AsSpan().IndexOf(sequence) >= 0;
}
