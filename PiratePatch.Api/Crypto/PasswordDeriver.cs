using System.Security.Cryptography;
using System.Text;

namespace PiratePatch.Api;

/// <summary>Each player gets their own passphrase. It is derived, not stored, so a re-download is the same program.</summary>
public sealed class PasswordDeriver(SigningKey key)
{
    public string PasswordFor(long telegramId) =>
        "SeaLegs_" + Convert.ToHexStringLower(key.DeriveSecret($"piratepatch/password/v1/{telegramId}", 6));

    /// <summary>Uppercase hex SHA-256, the form the challenge program compares against.</summary>
    public string PasswordHashFor(long telegramId) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(PasswordFor(telegramId))));
}
