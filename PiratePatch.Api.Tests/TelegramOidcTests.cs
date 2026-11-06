using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using PiratePatch.Api.Auth;

namespace PiratePatch.Api.Tests;

public class TelegramOidcTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(1800000000);

    [Theory]
    [InlineData("valid", TelegramLoginFailure.None)]
    [InlineData("signature", TelegramLoginFailure.BadSignature)]
    [InlineData("audience", TelegramLoginFailure.Malformed)]
    [InlineData("issuer", TelegramLoginFailure.Malformed)]
    [InlineData("nonce", TelegramLoginFailure.Malformed)]
    [InlineData("expired", TelegramLoginFailure.Malformed)]
    [InlineData("username", TelegramLoginFailure.NoUsername)]
    public void ValidatesSignedClaims(string change, TelegramLoginFailure expected)
    {
        using var rsa = RSA.Create(2048);
        var pub = rsa.ExportParameters(false);
        using var keys = JsonDocument.Parse(JsonSerializer.Serialize(new { keys = new[] { new { kid = "key", kty = "RSA", n = WebEncoders.Base64UrlEncode(pub.Modulus!), e = WebEncoders.Base64UrlEncode(pub.Exponent!) } } }));
        var header = WebEncoders.Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(new { alg = "RS256", kid = "key" }));
        var payload = WebEncoders.Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(new {
            iss = change == "issuer" ? "https://evil.example" : "https://oauth.telegram.org",
            aud = change == "audience" ? "other" : "123", nonce = change == "nonce" ? "other" : "nonce",
            exp = Now.ToUnixTimeSeconds() + (change == "expired" ? -1 : 300), iat = Now.ToUnixTimeSeconds(),
            sub = "subject", id = 42, preferred_username = change == "username" ? "bad" : "blackbeard"
        }));
        var content = header + "." + payload;
        var signature = rsa.SignData(Encoding.ASCII.GetBytes(content), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        if (change == "signature") signature[0] ^= 1;
        var (user, failure) = TelegramOidc.VerifyToken(content + "." + WebEncoders.Base64UrlEncode(signature), keys.RootElement, "123", "nonce", Now);
        Assert.Equal(expected, failure);
        if (expected == TelegramLoginFailure.None) Assert.Equal(new TelegramUser(42, "blackbeard"), user);
        else Assert.Null(user);
    }
}
