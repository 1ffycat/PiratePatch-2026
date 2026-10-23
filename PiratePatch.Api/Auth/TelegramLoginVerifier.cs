using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace PiratePatch.Api.Auth;

public readonly record struct TelegramUser(long Id, string Username);

public enum TelegramLoginFailure
{
    None,
    Malformed,
    BadSignature,
    Expired,
    NoUsername,
}

/// <summary>
/// Checks the payload of the Telegram Login Widget as described in
/// https://core.telegram.org/widgets/login#checking-authorization: an HMAC-SHA256, keyed with the
/// SHA-256 of the bot token, over the sorted <c>key=value</c> lines of every field but <c>hash</c>.
/// </summary>
public sealed partial class TelegramLoginVerifier(string botToken, TimeSpan maxAge, TimeProvider clock)
{
    private static readonly TimeSpan AllowedClockSkew = TimeSpan.FromMinutes(1);

    /// <summary>Telegram usernames: 5-32 characters of Latin letters, digits and underscores.</summary>
    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9_]{4,31}$")]
    private static partial Regex UsernameShape();

    private readonly byte[] _secretKey = SHA256.HashData(Encoding.UTF8.GetBytes(botToken));

    public (TelegramUser? User, TelegramLoginFailure Failure) Verify(IReadOnlyDictionary<string, string> fields)
    {
        if (!fields.TryGetValue("hash", out var hash) || !fields.TryGetValue("id", out var idText) || !fields.TryGetValue("auth_date", out var authDateText))
            return (null, TelegramLoginFailure.Malformed);

        if (!long.TryParse(idText, NumberStyles.None, CultureInfo.InvariantCulture, out var id)
            || !long.TryParse(authDateText, NumberStyles.None, CultureInfo.InvariantCulture, out var authDate)
            || hash.Length != 64)
            return (null, TelegramLoginFailure.Malformed);

        byte[] provided;
        try
        {
            provided = Convert.FromHexString(hash);
        }
        catch (FormatException)
        {
            return (null, TelegramLoginFailure.Malformed);
        }

        var dataCheckString = string.Join('\n',
            fields.Where(f => f.Key != "hash")
                  .OrderBy(f => f.Key, StringComparer.Ordinal)
                  .Select(f => $"{f.Key}={f.Value}"));
        var expected = HMACSHA256.HashData(_secretKey, Encoding.UTF8.GetBytes(dataCheckString));

        if (!CryptographicOperations.FixedTimeEquals(expected, provided))
            return (null, TelegramLoginFailure.BadSignature);

        var age = clock.GetUtcNow() - DateTimeOffset.FromUnixTimeSeconds(authDate);
        if (age > maxAge || age < -AllowedClockSkew)
            return (null, TelegramLoginFailure.Expired);

        if (!fields.TryGetValue("username", out var username) || !UsernameShape().IsMatch(username))
            return (null, TelegramLoginFailure.NoUsername);

        return (new TelegramUser(id, username), TelegramLoginFailure.None);
    }
}
