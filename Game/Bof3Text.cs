using System.Text;

namespace BreathOfFire3SaveEditor.Game;

/// <summary>
/// The game's text encoding: plain ASCII for letters and digits, with a handful
/// of punctuation marks moved to other codes and 0xFF used as the space.
/// </summary>
public static class Bof3Text
{
    private static readonly Dictionary<byte, char> Decode = new()
    {
        [0x3A] = '(', [0x3B] = ')', [0x3C] = ',', [0x3D] = '-', [0x3E] = '.', [0x3F] = '/',
        [0x5C] = '?', [0x5D] = '!', [0x8B] = '+', [0x8C] = '~', [0x8D] = '&', [0x8E] = '\'',
        [0x8F] = ':', [0x90] = '"', [0x91] = ';', [0x93] = '%', [0xFF] = ' ',
    };

    private static readonly Dictionary<char, byte> Encode = Decode.ToDictionary(p => p.Value, p => p.Key);

    public static string Read(ReadOnlySpan<byte> bytes)
    {
        var sb = new StringBuilder(bytes.Length);
        foreach (byte b in bytes)
        {
            if (b == 0)
            {
                break;
            }

            if (Decode.TryGetValue(b, out char c))
            {
                sb.Append(c);
            }
            else if (IsPlain(b))
            {
                sb.Append((char)b);
            }
            else
            {
                sb.Append('?');
            }
        }

        return sb.ToString();
    }

    /// <summary>Encodes up to <paramref name="length"/> characters, zero padded.</summary>
    public static byte[] Write(string text, int length)
    {
        var output = new byte[length];
        int i = 0;
        foreach (char c in text)
        {
            if (i >= length)
            {
                break;
            }

            if (CanEncode(c, out byte b))
            {
                output[i++] = b;
            }
        }

        return output;
    }

    public static bool CanEncode(char c, out byte b)
    {
        if (Encode.TryGetValue(c, out b))
        {
            return true;
        }

        if (c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9')
        {
            b = (byte)c;
            return true;
        }

        b = 0;
        return false;
    }

    /// <summary>True for bytes a name record can hold, used to sanity check RAM.</summary>
    public static bool IsNameByte(byte b) => b == 0 || IsPlain(b) || Decode.ContainsKey(b);

    private static bool IsPlain(byte b) => b is >= (byte)'A' and <= (byte)'Z' or >= (byte)'a' and <= (byte)'z'
        or >= (byte)'0' and <= (byte)'9';
}
