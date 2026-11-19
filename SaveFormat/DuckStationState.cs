using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

using ZstdSharp;

namespace BreathOfFire3SaveEditor.SaveFormat;

/// <summary>
/// A DuckStation save state (.sav).
///
/// Layout (format version 102, and every recent version using the same header):
/// <code>
///   0x00  u32  magic "DUCC"
///   0x04  u32  version
///   0x08  char title[128]
///   0x88  char serial[32]
///   0xA8  u32  media path length          0xAC  u32  offset to media path
///   0xB0  u32  media sub image            0xB4  u32  screenshot compression
///   0xB8  u32  screenshot width           0xBC  u32  screenshot height
///   0xC0  u32  screenshot compressed size 0xC4  u32  offset to screenshot
///   0xC8  u32  data compression           0xCC  u32  data compressed size
///   0xD0  u32  data uncompressed size     0xD4  u32  offset to data
/// </code>
/// Compression: 0 = none, 1 = deflate, 2 = Zstandard. The screenshot is RGBA8888.
///
/// The data block is the emulator's serialised machine. Main RAM sits inside the
/// "Bus" section and is located by the BIOS exception vector, which the kernel
/// always copies to RAM address 0x80.
/// </summary>
public sealed class DuckStationState
{
    public const uint Magic = 0x43435544; // "DUCC"

    private const int OffTitle = 0x08;
    private const int OffSerial = 0x88;
    private const int OffMediaLength = 0xA8;
    private const int OffMediaOffset = 0xAC;
    private const int OffShotCompression = 0xB4;
    private const int OffShotWidth = 0xB8;
    private const int OffShotHeight = 0xBC;
    private const int OffShotSize = 0xC0;
    private const int OffShotOffset = 0xC4;
    private const int OffDataCompression = 0xC8;
    private const int OffDataSize = 0xCC;
    private const int OffDataUncompressed = 0xD0;
    private const int OffDataOffset = 0xD4;
    private const int MinHeaderSize = 0xD8;

    public const int RamSize = 2 * 1024 * 1024;

    // lui k0,0 / addiu k0,k0,0x0C80 / jr k0 : the kernel's handler stub at 0x80.
    private static readonly byte[] ExceptionVector =
        [0x00, 0x00, 0x1A, 0x3C, 0x80, 0x0C, 0x5A, 0x27, 0x08, 0x00, 0x40, 0x03];

    private readonly byte[] _file;

    private DuckStationState(byte[] file, byte[] data, int ramOffset)
    {
        _file = file;
        Data = data;
        RamOffset = ramOffset;
    }

    public uint Version { get; private init; }
    public string Title { get; private init; } = string.Empty;
    public string Serial { get; private init; } = string.Empty;
    public string MediaPath { get; private init; } = string.Empty;

    /// <summary>The decompressed machine state. Edits go straight into this array.</summary>
    public byte[] Data { get; }

    /// <summary>Where PS1 RAM address 0 lives inside <see cref="Data"/>.</summary>
    public int RamOffset { get; }

    public int ScreenshotWidth { get; private init; }
    public int ScreenshotHeight { get; private init; }

    /// <summary>The emulator's thumbnail as RGBA bytes, or null if it could not be read.</summary>
    public byte[]? ScreenshotRgba { get; private init; }

    public static DuckStationState Load(string path) => Parse(File.ReadAllBytes(path));

    public static DuckStationState Parse(byte[] file)
    {
        if (file.Length < MinHeaderSize || U32(file, 0) != Magic)
        {
            throw new InvalidDataException("This is not a DuckStation save state (missing the DUCC header).");
        }

        int dataOffset = checked((int)U32(file, OffDataOffset));
        int dataSize = checked((int)U32(file, OffDataSize));
        int dataUncompressed = checked((int)U32(file, OffDataUncompressed));
        uint compression = U32(file, OffDataCompression);

        if (dataOffset <= 0 || dataSize <= 0 || (long)dataOffset + dataSize > file.Length)
        {
            throw new InvalidDataException(
                $"Unsupported DuckStation state layout (version {U32(file, 4)}): the data block is out of range.");
        }

        byte[] data = Decompress(file.AsSpan(dataOffset, dataSize), compression, dataUncompressed);
        int ram = FindRam(data);

        int mediaLength = (int)U32(file, OffMediaLength);
        int mediaOffset = (int)U32(file, OffMediaOffset);
        string media = mediaLength > 0 && mediaOffset > 0 && mediaOffset + mediaLength <= file.Length
            ? Encoding.UTF8.GetString(file, mediaOffset, mediaLength)
            : string.Empty;

        int width = (int)U32(file, OffShotWidth);
        int height = (int)U32(file, OffShotHeight);

        return new DuckStationState(file, data, ram)
        {
            Version = U32(file, 4),
            Title = CString(file, OffTitle, 128),
            Serial = CString(file, OffSerial, 32),
            MediaPath = media,
            ScreenshotWidth = width,
            ScreenshotHeight = height,
            ScreenshotRgba = TryReadScreenshot(file, width, height),
        };
    }

    /// <summary>
    /// Builds the file again: the header, media path and screenshot are copied as
    /// they were and only the data block is recompressed (always as Zstandard,
    /// which every DuckStation version that writes this header can read).
    /// </summary>
    public byte[] Build()
    {
        int dataOffset = (int)U32(_file, OffDataOffset);
        int oldSize = (int)U32(_file, OffDataSize);

        byte[] packed;
        using (var compressor = new Compressor(3))
        {
            packed = compressor.Wrap(Data).ToArray();
        }

        int tail = _file.Length - (dataOffset + oldSize);
        var output = new byte[dataOffset + packed.Length + tail];
        Buffer.BlockCopy(_file, 0, output, 0, dataOffset);
        Buffer.BlockCopy(packed, 0, output, dataOffset, packed.Length);
        if (tail > 0)
        {
            Buffer.BlockCopy(_file, dataOffset + oldSize, output, dataOffset + packed.Length, tail);
        }

        // Anything stored after the data block moves by the change in size.
        int delta = packed.Length - oldSize;
        foreach (int field in new[] { OffMediaOffset, OffShotOffset })
        {
            uint at = U32(output, field);
            if (at > dataOffset)
            {
                PutU32(output, field, (uint)(at + delta));
            }
        }

        PutU32(output, OffDataCompression, 2);
        PutU32(output, OffDataSize, (uint)packed.Length);
        PutU32(output, OffDataUncompressed, (uint)Data.Length);
        return output;
    }

    // ------------------------------------------------------------- RAM access

    public byte Ram8(int address) => Data[RamOffset + address];

    public ushort Ram16(int address) => BinaryPrimitives.ReadUInt16LittleEndian(Data.AsSpan(RamOffset + address));

    public uint Ram32(int address) => BinaryPrimitives.ReadUInt32LittleEndian(Data.AsSpan(RamOffset + address));

    public void SetRam8(int address, int value) => Data[RamOffset + address] = (byte)value;

    public void SetRam16(int address, int value) =>
        BinaryPrimitives.WriteUInt16LittleEndian(Data.AsSpan(RamOffset + address), (ushort)value);

    public void SetRam32(int address, uint value) =>
        BinaryPrimitives.WriteUInt32LittleEndian(Data.AsSpan(RamOffset + address), value);

    public ReadOnlySpan<byte> RamSpan => Data.AsSpan(RamOffset, RamSize);

    // ---------------------------------------------------------------- helpers

    /// <summary>
    /// Finds RAM address 0. The same handler bytes also turn up earlier, in the
    /// CPU section's cache, so the search starts at the "Bus" section marker
    /// (a length-prefixed name) that holds RAM.
    /// </summary>
    private static int FindRam(byte[] data)
    {
        int bus = data.AsSpan().IndexOf(BusMarker);
        int from = bus < 0 ? 0 : bus;

        while (from < data.Length)
        {
            int hit = data.AsSpan(from).IndexOf(ExceptionVector);
            if (hit < 0)
            {
                break;
            }

            int ram = from + hit - 0x80;
            if (ram >= 0 && ram + RamSize <= data.Length)
            {
                return ram;
            }

            from += hit + 1;
        }

        throw new InvalidDataException("Could not find the PlayStation's main RAM inside the save state.");
    }

    private static readonly byte[] BusMarker = [0x03, 0x00, 0x00, 0x00, (byte)'B', (byte)'u', (byte)'s'];

    private static byte[] Decompress(ReadOnlySpan<byte> source, uint compression, int expected)
    {
        byte[] result = compression switch
        {
            0 => source.ToArray(),
            1 => Inflate(source.ToArray()),
            2 => Unzstd(source),
            _ => throw new InvalidDataException($"Unknown compression type {compression} in save state."),
        };

        if (expected > 0 && result.Length != expected)
        {
            throw new InvalidDataException(
                $"Save state data decompressed to {result.Length} bytes, expected {expected}.");
        }

        return result;
    }

    private static byte[] Unzstd(ReadOnlySpan<byte> source)
    {
        using var decompressor = new Decompressor();
        return decompressor.Unwrap(source).ToArray();
    }

    private static byte[] Inflate(byte[] source)
    {
        // DuckStation's deflate states carry a zlib header; fall back to raw
        // deflate in case a build ever wrote them without one.
        try
        {
            using var input = new MemoryStream(source);
            using var z = new ZLibStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            z.CopyTo(output);
            return output.ToArray();
        }
        catch (InvalidDataException)
        {
            using var input = new MemoryStream(source);
            using var d = new DeflateStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            d.CopyTo(output);
            return output.ToArray();
        }
    }

    private static byte[]? TryReadScreenshot(byte[] file, int width, int height)
    {
        try
        {
            int offset = (int)U32(file, OffShotOffset);
            int size = (int)U32(file, OffShotSize);
            uint compression = U32(file, OffShotCompression);
            int expected = width * height * 4;

            if (width <= 0 || height <= 0 || width > 4096 || height > 4096 || offset <= 0 || size <= 0 ||
                offset + size > file.Length)
            {
                return null;
            }

            byte[] rgba = Decompress(file.AsSpan(offset, size), compression, expected);
            return rgba.Length == expected ? rgba : null;
        }
        catch (Exception ex) when (ex is InvalidDataException or ZstdException or OverflowException)
        {
            // The thumbnail is decoration; a state with an odd one still edits fine.
            return null;
        }
    }

    private static uint U32(byte[] b, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(offset, 4));

    private static void PutU32(byte[] b, int offset, uint value) =>
        BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(offset, 4), value);

    private static string CString(byte[] b, int offset, int max)
    {
        int end = Array.IndexOf(b, (byte)0, offset, max);
        int length = (end < 0 ? max : end - offset);
        return Encoding.UTF8.GetString(b, offset, length);
    }
}
