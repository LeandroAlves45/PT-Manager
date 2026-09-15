using Application.Common.Abstractions;
using Infrastructure.Media.Imaging;
using SkiaSharp;

namespace Infrastructure.IntegrationTests.Media;

/// <summary>
/// Prova o ponto 1 do Gate 5C: ficheiro vazio, MIME falso, imagem corrompida e
/// excesso de limites são recusados.
/// </summary>
/// <remarks>
/// Vive na suite de integração e não nos testes unitários porque exercita os
/// binários nativos do SkiaSharp. Em CI corre em Linux, que é exatamente onde a
/// ausência do pacote NativeAssets falharia — e falharia em runtime, não em
/// build.
/// </remarks>
public sealed class SkiaImageProcessorTests
{
    private static readonly SkiaImageProcessor Processor = new();

    [Fact]
    public async Task NormalizeAsync_WhenContentIsEmpty_ReturnsEmpty()
    {
        var result = await NormalizeAsync([], "image/png", ImageProfiles.TrainerLogo);

        Assert.Null(result.Image);
        Assert.Equal(ImageValidationFailure.Empty, result.Failure);
    }

    [Fact]
    public async Task NormalizeAsync_WhenContentIsNotAnImage_ReturnsNotDecodable()
    {
        // Cabeçalho de PDF: um payload arbitrário que nunca descodifica.
        var payload = "%PDF-1.7\n%âãÏÓ\n"u8.ToArray();

        var result = await NormalizeAsync(payload, "image/png", ImageProfiles.TrainerLogo);

        Assert.Null(result.Image);
        Assert.Equal(ImageValidationFailure.NotDecodable, result.Failure);
    }

    [Fact]
    public async Task NormalizeAsync_WhenDeclaredTypeDoesNotMatchContent_ReturnsMismatch()
    {
        var png = CreateImage(256, 256, SKEncodedImageFormat.Png);

        var result = await NormalizeAsync(png, "image/jpeg", ImageProfiles.TrainerLogo);

        Assert.Null(result.Image);
        Assert.Equal(ImageValidationFailure.ContentTypeMismatch, result.Failure);
    }

    [Fact]
    public async Task NormalizeAsync_WhenPayloadIsTruncated_ReturnsNotDecodable()
    {
        var png = CreateImage(256, 256, SKEncodedImageFormat.Png);
        var truncated = png[..(png.Length / 2)];

        var result = await NormalizeAsync(truncated, "image/png", ImageProfiles.TrainerLogo);

        Assert.Null(result.Image);
        Assert.Equal(ImageValidationFailure.NotDecodable, result.Failure);
    }

    [Fact]
    public async Task NormalizeAsync_WhenImageIsBelowMinimumDimension_ReturnsTooSmall()
    {
        var png = CreateImage(16, 16, SKEncodedImageFormat.Png);

        var result = await NormalizeAsync(png, "image/png", ImageProfiles.TrainerLogo);

        Assert.Null(result.Image);
        Assert.Equal(ImageValidationFailure.DimensionsTooSmall, result.Failure);
    }

    [Fact]
    public async Task NormalizeAsync_WhenContentExceedsProfileBytes_ReturnsTooLarge()
    {
        var png = CreateImage(256, 256, SKEncodedImageFormat.Png);
        var profile = ImageProfiles.TrainerLogo with { MaxBytes = 16 };

        var result = await NormalizeAsync(png, "image/png", profile);

        Assert.Null(result.Image);
        Assert.Equal(ImageValidationFailure.TooLarge, result.Failure);
    }

    [Fact]
    public async Task NormalizeAsync_WhenPixelBudgetIsExceeded_ReturnsBudgetExceeded()
    {
        var png = CreateImage(256, 256, SKEncodedImageFormat.Png);
        // 256 * 256 = 65536 pixeis; o orcamento fica deliberadamente abaixo.
        var profile = ImageProfiles.TrainerLogo with { MaxPixels = 1000 };

        var result = await NormalizeAsync(png, "image/png", profile);

        Assert.Null(result.Image);
        Assert.Equal(ImageValidationFailure.PixelBudgetExceeded, result.Failure);
    }

    [Fact]
    public async Task NormalizeAsync_WhenImageIsValid_ReturnsWebpWithinOutputBox()
    {
        var png = CreateImage(2048, 1024, SKEncodedImageFormat.Png);

        var result = await NormalizeAsync(png, "image/png", ImageProfiles.TrainerLogo);

        Assert.Null(result.Failure);
        var image = Assert.IsType<ProcessedImage>(result.Image);
        Assert.Equal("image/webp", image.ContentType);
        Assert.Equal(512, image.Width);
        Assert.Equal(256, image.Height);

        using var decoded = SKBitmap.Decode(image.Content.ToArray());
        Assert.NotNull(decoded);
        Assert.Equal(512, decoded.Width);
    }

    [Fact]
    public async Task NormalizeAsync_WhenImageIsSmallerThanOutputBox_DoesNotUpscale()
    {
        var png = CreateImage(128, 96, SKEncodedImageFormat.Png);

        var result = await NormalizeAsync(png, "image/png", ImageProfiles.TrainerLogo);

        var image = Assert.IsType<ProcessedImage>(result.Image);
        Assert.Equal(128, image.Width);
        Assert.Equal(96, image.Height);
    }

    /// <summary>
    /// PTM-SEC-02: o PNG de 6000×5000 de cor sólida usado na auditoria tinha ~88 KB
    /// e custava +268 MiB de decode. Agora é recusado pelo cabeçalho, antes de alocar.
    /// </summary>
    [Fact]
    public async Task NormalizeAsync_WhenCompressedPngDeclaresAuditDimensions_ReturnsDimensionsTooLarge()
    {
        var png = CreateSolidPng(6000, 5000);

        var result = await NormalizeAsync(png, "image/png", ImageProfiles.TrainerLogo);

        Assert.Null(result.Image);
        Assert.Equal(ImageValidationFailure.DimensionsTooLarge, result.Failure);
    }

    [Theory]
    [InlineData("trainer_logo")]
    [InlineData("client_avatar")]
    public async Task NormalizeAsync_WhenImageExceedsTwelveMegapixelsWithinMaxDimension_ReturnsBudgetExceeded(
        string profileName)
    {
        // 4096 × 4096 = 16,7 MP: a dimensão passa, o orçamento de píxeis não.
        var png = CreateSolidPng(4096, 4096);
        var profile = profileName == ImageProfiles.TrainerLogo.Name
            ? ImageProfiles.TrainerLogo
            : ImageProfiles.ClientAvatar;

        var result = await NormalizeAsync(png, "image/png", profile);

        Assert.Null(result.Image);
        Assert.Equal(ImageValidationFailure.PixelBudgetExceeded, result.Failure);
    }

    [Fact]
    public async Task NormalizeAsync_WhenImageIsWithinTwelveMegapixels_IsAccepted()
    {
        // 4096 × 2929 ≈ 11,997 MP: o limite novo continua a aceitar fotografias grandes.
        var png = CreateSolidPng(4096, 2929);

        var result = await NormalizeAsync(png, "image/png", ImageProfiles.TrainerLogo);

        Assert.Null(result.Failure);
        var image = Assert.IsType<ProcessedImage>(result.Image);
        Assert.Equal(512, image.Width);
    }

    /// <summary>
    /// O logo do trainer transporta frequentemente transparência. Se o formato de
    /// saída a perdesse, todos os logos ganhariam um fundo sólido — por isso o
    /// canal alfa é uma asserção, não uma suposição.
    /// </summary>
    [Fact]
    public async Task NormalizeAsync_PreservesAlphaChannel()
    {
        var png = CreateImageWithTransparentCorner(256, 256);

        var result = await NormalizeAsync(png, "image/png", ImageProfiles.TrainerLogo);

        var image = Assert.IsType<ProcessedImage>(result.Image);
        using var decoded = SKBitmap.Decode(image.Content.ToArray());

        Assert.NotNull(decoded);
        Assert.Equal(0, decoded.GetPixel(4, 4).Alpha);
        Assert.Equal(255, decoded.GetPixel(decoded.Width - 4, decoded.Height - 4).Alpha);
    }

    private static Task<ImageProcessingResult> NormalizeAsync(
        byte[] content,
        string declaredContentType,
        ImageProfile profile)
    {
        using var stream = new MemoryStream(content);
        return Processor.NormalizeAsync(
            new MediaUpload(stream, declaredContentType, content.Length),
            profile,
            TestContext.Current.CancellationToken);
    }

    private static byte[] CreateImage(int width, int height, SKEncodedImageFormat format)
    {
        using var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.CornflowerBlue);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, 95);
        return data.ToArray();
    }

    /// <summary>
    /// Escreve um PNG RGB preto em streaming. Criar a imagem com SKBitmap alocaria o
    /// mesmo bitmap de resolução total que o teste quer provar que já não é aceite.
    /// </summary>
    private static byte[] CreateSolidPng(int width, int height)
    {
        using var output = new MemoryStream();
        output.Write([137, 80, 78, 71, 13, 10, 26, 10]);

        var header = new byte[13];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(0, 4), width);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4, 4), height);
        header[8] = 8; // bit depth
        header[9] = 2; // truecolor RGB
        WritePngChunk(output, "IHDR", header);

        using var compressed = new MemoryStream();
        using (var zlib = new System.IO.Compression.ZLibStream(
            compressed, System.IO.Compression.CompressionLevel.Fastest, leaveOpen: true))
        {
            // Byte de filtro 0 seguido de píxeis a zero.
            var row = new byte[1 + (width * 3)];
            for (var y = 0; y < height; y++)
                zlib.Write(row);
        }

        WritePngChunk(output, "IDAT", compressed.ToArray());
        WritePngChunk(output, "IEND", []);
        return output.ToArray();
    }

    private static void WritePngChunk(Stream output, string type, byte[] data)
    {
        Span<byte> length = stackalloc byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
        output.Write(length);

        var typeAndData = new byte[4 + data.Length];
        System.Text.Encoding.ASCII.GetBytes(type, typeAndData);
        data.CopyTo(typeAndData, 4);
        output.Write(typeAndData);

        Span<byte> crc = stackalloc byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(crc, Crc32(typeAndData));
        output.Write(crc);
    }

    private static uint Crc32(byte[] bytes)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var value in bytes)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
        }

        return ~crc;
    }

    private static byte[] CreateImageWithTransparentCorner(int width, int height)
    {
        using var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Transparent);
            using var paint = new SKPaint { Color = SKColors.OrangeRed };
            canvas.DrawRect(
                new SKRect(width / 2f, height / 2f, width, height), paint);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}
