using System.Buffers.Binary;
using System.Text;

namespace Infrastructure.IntegrationTests.Media.Video;

/// <summary>
/// Constrói ficheiros MP4 e MOV mínimos, box a box, com os offsets documentados.
/// Os testes descrevem a estrutura que querem provar em vez de dependerem de
/// ficheiros binários opacos versionados.
/// </summary>
internal static class Mp4TestFile
{
    private static readonly int[] IdentityMatrix = [0x00010000, 0, 0, 0, 0x00010000, 0, 0, 0, 0x40000000];
    private static readonly int[] Rotate90Matrix = [0, 0x00010000, 0, -0x00010000, 0, 0, 0, 0, 0x40000000];

    public static byte[] Box(string type, params byte[][] children)
    {
        var payload = children.SelectMany(child => child).ToArray();
        var box = new byte[8 + payload.Length];
        BinaryPrimitives.WriteUInt32BigEndian(box, (uint)box.Length);
        Encoding.ASCII.GetBytes(type).CopyTo(box, 4);
        payload.CopyTo(box, 8);
        return box;
    }

    public static byte[] FullBox(string type, byte version, int flags, byte[] payload)
    {
        var header = new byte[4];
        header[0] = version;
        header[1] = (byte)(flags >> 16);
        header[2] = (byte)(flags >> 8);
        header[3] = (byte)flags;
        return Box(type, header, payload);
    }

    public static byte[] Ftyp(string majorBrand, params string[] compatibleBrands)
    {
        var payload = new List<byte>();
        payload.AddRange(Encoding.ASCII.GetBytes(majorBrand));
        payload.AddRange(new byte[4]);
        foreach (var brand in compatibleBrands)
            payload.AddRange(Encoding.ASCII.GetBytes(brand));
        return Box("ftyp", payload.ToArray());
    }

    public static byte[] Mvhd(uint timescale, uint duration)
    {
        // v0: creation, modification, timescale, duration, rate, volume, reserved, matrix, pre_defined, next_track_ID.
        var payload = new byte[96];
        BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(8), timescale);
        BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(12), duration);
        BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(16), 0x00010000);
        BinaryPrimitives.WriteInt16BigEndian(payload.AsSpan(20), 0x0100);
        WriteMatrix(payload.AsSpan(32), IdentityMatrix);
        BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(92), 3);
        return FullBox("mvhd", 0, 0, payload);
    }

    public static byte[] Tkhd(int width, int height, bool rotate90, bool enabled, uint duration)
    {
        // v0: creation, modification, track_ID, reserved, duration, reserved, layer,
        // alternate_group, volume, reserved, matrix, width, height.
        var payload = new byte[80];
        BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(16), duration);
        WriteMatrix(payload.AsSpan(36), rotate90 ? Rotate90Matrix : IdentityMatrix);
        BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(72), (uint)width << 16);
        BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(76), (uint)height << 16);
        return FullBox("tkhd", 0, enabled ? 0x3 : 0x2, payload);
    }

    public static byte[] Hdlr(string handler)
    {
        var payload = new byte[21];
        Encoding.ASCII.GetBytes(handler).CopyTo(payload, 4);
        return FullBox("hdlr", 0, 0, payload);
    }

    public static byte[] Stsd(byte[] format)
    {
        var entry = new byte[86];
        BinaryPrimitives.WriteUInt32BigEndian(entry, 86);
        format.CopyTo(entry, 4);
        BinaryPrimitives.WriteUInt16BigEndian(entry.AsSpan(14), 1);
        var payload = new byte[4 + entry.Length];
        BinaryPrimitives.WriteUInt32BigEndian(payload, 1);
        entry.CopyTo(payload, 4);
        return FullBox("stsd", 0, 0, payload);
    }

    public static byte[] Trak(
        string handler,
        string codec,
        int width = 0,
        int height = 0,
        bool rotate90 = false,
        bool enabled = true,
        uint duration = 12_500) =>
        Trak(handler, Encoding.ASCII.GetBytes(codec), width, height, rotate90, enabled, duration);

    public static byte[] Trak(
        string handler,
        byte[] codec,
        int width,
        int height,
        bool rotate90,
        bool enabled,
        uint duration) =>
        Box("trak",
            Tkhd(width, height, rotate90, enabled, duration),
            Box("mdia",
                Hdlr(handler),
                Box("minf",
                    Box("stbl", Stsd(codec)))));

    /// <summary>Moov com um vídeo H.264 1280x720 de 12,5 s e áudio AAC.</summary>
    public static byte[] StandardMoov(params byte[][] extraChildren) =>
        Box("moov",
            new[]
            {
                Mvhd(1000, 12_500),
                Trak("vide", "avc1", 1280, 720),
                Trak("soun", "mp4a")
            }.Concat(extraChildren).ToArray());

    /// <summary>Ficheiro completo com o moov no início ou no fim, e um mdat de tamanho indicado.</summary>
    public static byte[] File(byte[] moov, bool moovAtEnd, int mediaBytes = 1024, string majorBrand = "isom")
    {
        var ftyp = Ftyp(majorBrand, "isom", "mp42");
        var mdat = Box("mdat", new byte[mediaBytes]);
        return moovAtEnd
            ? [.. ftyp, .. mdat, .. moov]
            : [.. ftyp, .. moov, .. mdat];
    }

    private static void WriteMatrix(Span<byte> destination, int[] matrix)
    {
        for (var index = 0; index < matrix.Length; index++)
            BinaryPrimitives.WriteInt32BigEndian(destination[(index * 4)..], matrix[index]);
    }
}
