using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using PiratePatch.Api.Configuration;

namespace PiratePatch.Api.Auth;

/// <summary>Telegram's authorization-code flow with PKCE and a protected, short-lived correlation cookie.</summary>
public sealed partial class TelegramOidc(HttpClient client, IDataProtectionProvider protection,
    IOptions<TelegramOptions> telegram, IOptions<SiteOptions> site, ILogger<TelegramOidc> logger)
{
    private const string Cookie = "pp_telegram_login";
    private const string Issuer = "https://oauth.telegram.org";
    private string Callback => new Uri(site.Value.PublicUrl, "/api/auth/telegram/callback").AbsoluteUri;
    private ITimeLimitedDataProtector Protector => protection.CreateProtector("piratepatch.telegram.oidc.v1").ToTimeLimitedDataProtector();
    private static string Random() => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9_]{4,31}$")]
    private static partial Regex UsernameShape();

    public IResult Start(HttpContext http)
    {
        var state = Random();
        var nonce = Random();
        var verifier = Random();
        var cookie = Protector.Protect(JsonSerializer.Serialize(new Correlation(state, nonce, verifier)), TimeSpan.FromMinutes(10));
        http.Response.Cookies.Append(Cookie, cookie, new CookieOptions
        {
            HttpOnly = true, Secure = http.Request.IsHttps, SameSite = SameSiteMode.Lax,
            Path = "/api/auth/telegram/callback", MaxAge = TimeSpan.FromMinutes(10), IsEssential = true
        });
        var query = new Dictionary<string, string?> {
            ["client_id"] = telegram.Value.ClientId, ["redirect_uri"] = Callback,
            ["response_type"] = "code", ["scope"] = "openid profile", ["state"] = state,
            ["nonce"] = nonce, ["code_challenge"] = WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier))),
            ["code_challenge_method"] = "S256"
        };
        http.Response.Headers.CacheControl = "no-store";
        return Results.Redirect(QueryHelpers.AddQueryString(Issuer + "/auth", query));
    }

    public async Task<(TelegramUser? User, TelegramLoginFailure Failure)> FinishAsync(HttpContext http, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "no-store";
        var cookie = http.Request.Cookies[Cookie];
        http.Response.Cookies.Delete(Cookie, new CookieOptions { Path = "/api/auth/telegram/callback", Secure = http.Request.IsHttps, SameSite = SameSiteMode.Lax });
        var stage = "checking callback";
        try
        {
            if (cookie is null || cookie.Length > 4096 || http.Request.Query["state"].Count != 1 || http.Request.Query["code"].Count != 1)
            {
                logger.LogDebug("Telegram login rejected: missing or invalid correlation cookie, state, or code");
                return (null, TelegramLoginFailure.Malformed);
            }
            var correlation = JsonSerializer.Deserialize<Correlation>(Protector.Unprotect(cookie));
            var state = http.Request.Query["state"].ToString();
            var code = http.Request.Query["code"].ToString();
            if (correlation is null || state.Length > 256 || code.Length is 0 or > 2048 ||
                !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(state), Encoding.UTF8.GetBytes(correlation.State)))
            {
                logger.LogWarning("Telegram login rejected: invalid authorization code or state mismatch");
                return (null, TelegramLoginFailure.Malformed);
            }

            stage = "exchanging authorization code";
            using var request = new HttpRequestMessage(HttpMethod.Post, Issuer + "/token");
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{telegram.Value.ClientId}:{telegram.Value.ClientSecret}")));
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string> {
                ["grant_type"] = "authorization_code", ["code"] = code, ["redirect_uri"] = Callback,
                ["client_id"] = telegram.Value.ClientId, ["code_verifier"] = correlation.Verifier
            });
            using var response = await client.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
            using var tokenResponse = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var token = tokenResponse.RootElement.GetProperty("id_token").GetString() ?? "";
            stage = "fetching signing keys";
            using var keysResponse = await client.GetAsync(Issuer + "/.well-known/jwks.json", ct);
            keysResponse.EnsureSuccessStatusCode();
            var keysBody = await keysResponse.Content.ReadAsStringAsync(ct);
            JsonDocument keys;
            try
            {
                keys = JsonDocument.Parse(keysBody);
            }
            catch (JsonException ex)
            {
                // Never log response bodies: a proxy or upstream service may return sensitive data.
                logger.LogDebug("Telegram signing-key response was not JSON: content type {ContentType}, content encoding {ContentEncoding}, body length {BodyLength}, error at line {Line}, byte {Position}",
                    keysResponse.Content.Headers.ContentType?.MediaType,
                    string.Join(",", keysResponse.Content.Headers.ContentEncoding), keysBody.Length, ex.LineNumber, ex.BytePositionInLine);
                throw;
            }
            using (keys)
            {
                stage = "validating ID token";
                var result = VerifyToken(token, keys.RootElement, telegram.Value.ClientId, correlation.Nonce, DateTimeOffset.UtcNow, logger);
                if (result.Failure == TelegramLoginFailure.NoUsername)
                    logger.LogDebug("Telegram login requires a public username");
                else if (result.Failure != TelegramLoginFailure.None)
                    logger.LogWarning("Telegram login rejected during {Stage}: {Failure}", stage, result.Failure);
                return result;
            }
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException or JsonException or HttpRequestException or InvalidOperationException or KeyNotFoundException or ArgumentException or TaskCanceledException)
        {
            logger.LogWarning("Telegram login failed while {Stage}: {ErrorType}; HTTP status: {StatusCode}",
                stage, ex.GetType().Name, (ex as HttpRequestException)?.StatusCode);
            return (null, TelegramLoginFailure.Malformed);
        }
    }

    internal static (TelegramUser? User, TelegramLoginFailure Failure) VerifyToken(string token, JsonElement keys, string audience, string nonce, DateTimeOffset now, ILogger? logger = null)
    {
        var step = "parsing token";
        (TelegramUser?, TelegramLoginFailure) Invalid(string claim)
        {
            logger?.LogDebug("Telegram ID token rejected: invalid or missing {Claim} claim", claim);
            return (null, TelegramLoginFailure.Malformed);
        }

        try
        {
            var parts = token.Split('.');
            if (token.Length > 16384 || parts.Length != 3) return (null, TelegramLoginFailure.Malformed);
            using var header = JsonDocument.Parse(WebEncoders.Base64UrlDecode(parts[0]));
            using var payload = JsonDocument.Parse(WebEncoders.Base64UrlDecode(parts[1]));
            step = "checking signature";
            var h = header.RootElement;
            if (h.GetProperty("alg").GetString() != "RS256") return (null, TelegramLoginFailure.BadSignature);
            var kid = h.GetProperty("kid").GetString();
            var key = keys.GetProperty("keys").EnumerateArray().FirstOrDefault(k =>
                k.GetProperty("kid").GetString() == kid && k.GetProperty("kty").GetString() == "RSA");
            if (key.ValueKind == JsonValueKind.Undefined) return (null, TelegramLoginFailure.BadSignature);
            using var rsa = RSA.Create();
            rsa.ImportParameters(new RSAParameters { Modulus = WebEncoders.Base64UrlDecode(key.GetProperty("n").GetString()!), Exponent = WebEncoders.Base64UrlDecode(key.GetProperty("e").GetString()!) });
            if (!rsa.VerifyData(Encoding.ASCII.GetBytes(parts[0] + "." + parts[1]), WebEncoders.Base64UrlDecode(parts[2]), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
                return (null, TelegramLoginFailure.BadSignature);
            step = "checking claims";
            var p = payload.RootElement;
            if (!p.TryGetProperty("iss", out var issuer) || issuer.ValueKind != JsonValueKind.String || issuer.GetString() != Issuer)
                return Invalid("issuer");
            if (!p.TryGetProperty("aud", out var aud) || aud.ValueKind != JsonValueKind.String || aud.GetString() != audience)
                return Invalid("audience");
            if (!p.TryGetProperty("nonce", out var nonceClaim) || nonceClaim.ValueKind != JsonValueKind.String || nonceClaim.GetString() != nonce)
                return Invalid("nonce");
            if (!p.TryGetProperty("exp", out var exp) || exp.ValueKind != JsonValueKind.Number || !exp.TryGetInt64(out var expires) || expires <= now.ToUnixTimeSeconds())
                return Invalid("expiration");
            if (!p.TryGetProperty("iat", out var iat) || iat.ValueKind != JsonValueKind.Number || !iat.TryGetInt64(out var issued) || issued > now.ToUnixTimeSeconds() + 60)
                return Invalid("issued-at");
            if (!p.TryGetProperty("sub", out var sub) || sub.ValueKind != JsonValueKind.String || string.IsNullOrEmpty(sub.GetString()))
                return Invalid("subject");
            var hasId = p.TryGetProperty("id", out var idValue);
            long id = 0;
            var validId = hasId && (idValue.ValueKind switch
            {
                JsonValueKind.Number => idValue.TryGetInt64(out id),
                JsonValueKind.String => long.TryParse(idValue.GetString(), NumberStyles.None, CultureInfo.InvariantCulture, out id),
                _ => false,
            }) && id > 0;
            if (!validId)
            {
                logger?.LogDebug("Telegram ID claim has JSON type {ValueKind}", hasId ? idValue.ValueKind.ToString() : "missing");
                return Invalid("Telegram ID");
            }
            if (!p.TryGetProperty("preferred_username", out var usernameValue) || usernameValue.ValueKind != JsonValueKind.String || usernameValue.GetString() is not { } username || !UsernameShape().IsMatch(username))
                return (null, TelegramLoginFailure.NoUsername);
            return (new TelegramUser(id, username), TelegramLoginFailure.None);
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException or JsonException or InvalidOperationException or KeyNotFoundException or ArgumentException or OverflowException)
        {
            logger?.LogDebug("Telegram ID token failed while {Step}: {ErrorType}", step, ex.GetType().Name);
            return (null, TelegramLoginFailure.Malformed);
        }
    }

    private sealed record Correlation(string State, string Nonce, string Verifier);
}
