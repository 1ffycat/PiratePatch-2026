using System.Buffers.Binary;
using System.Text;
using System.Text.RegularExpressions;

namespace PiratePatch.Api;

public readonly record struct FlagIdentity(long TelegramId, string Handle);

/// <summary>
/// Treasure codes. A code is <c>P1R4T3-C0D3{base64url(payload || signature)}</c>: the payload names the
/// player and the signature is Ed25519 over it. Anyone holding the public key can verify a code without this
/// service; only the server can mint one.
/// </summary>
public sealed partial class FlagCodec(SigningKey key)
{
    public const string Prefix = "P1R4T3-C0D3{";
    public const char Suffix = '}';

    private const byte Version = 1;
    private const int HeaderLength = 1 + sizeof(long);
    private static readonly byte[] Domain = "piratepatch/flag/v1\0"u8.ToArray();

    /// <summary>The shape of a code; used to reject junk before any crypto happens.</summary>
    [GeneratedRegex(@"^P1R4T3-C0D3\{[A-Za-z0-9_-]{60,200}\}$")]
    public static partial Regex Shape();

    [GeneratedRegex(@"^[A-Za-z0-9_]{1,32}$")]
    private static partial Regex HandleShape();

    public string Issue(long telegramId, string handle)
    {
        if (!HandleShape().IsMatch(handle))
            throw new ArgumentException("Not a valid Telegram handle.", nameof(handle));

        var handleBytes = Encoding.ASCII.GetBytes(handle);
        var payload = new byte[HeaderLength + handleBytes.Length];
        payload[0] = Version;
        BinaryPrimitives.WriteInt64BigEndian(payload.AsSpan(1), telegramId);
        handleBytes.CopyTo(payload, HeaderLength);

        var signature = key.Sign([.. Domain, .. payload]);
        return Prefix + Base64UrlEncode([.. payload, .. signature]) + Suffix;
    }

    public FlagIdentity? Verify(string flag)
    {
        if (!Shape().IsMatch(flag))
            return null;

        byte[] raw;
        try
        {
            raw = Base64UrlDecode(flag[Prefix.Length..^1]);
        }
        catch (FormatException)
        {
            return null;
        }

        if (raw.Length < HeaderLength + 1 + SigningKey.SignatureLength)
            return null;

        var payload = raw.AsSpan(0, raw.Length - SigningKey.SignatureLength);
        var signature = raw.AsSpan(payload.Length);
        if (payload[0] != Version || !SigningKey.Verify(key.PublicKey, [.. Domain, .. payload], signature))
            return null;

        var handle = Encoding.ASCII.GetString(payload[HeaderLength..]);
        return HandleShape().IsMatch(handle)
            ? new FlagIdentity(BinaryPrimitives.ReadInt64BigEndian(payload[1..]), handle)
            : null;
    }

    private static string Base64UrlEncode(ReadOnlySpan<byte> data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');
        return Convert.FromBase64String(padded);
    }
}
