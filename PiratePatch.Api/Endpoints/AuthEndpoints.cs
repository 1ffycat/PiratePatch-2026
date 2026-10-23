using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using PiratePatch.Api.Auth;
using PiratePatch.Api.Configuration;
using PiratePatch.Api.Hosting;
using PiratePatch.Api.Notifications;
using PiratePatch.Api.Players;

namespace PiratePatch.Api.Endpoints;

public static class AuthEndpoints
{
    private const int MaxLoginFields = 16;
    private const int MaxLoginFieldLength = 1024;

    public static void MapAuthEndpoints(this RouteGroupBuilder api, bool devLogin)
    {
        var auth = api.MapGroup("/auth");
        auth.MapPost("/telegram", LoginWithTelegram).RequireRateLimiting(RateLimits.Login);
        auth.MapPost("/logout", (Delegate)Logout);

        if (devLogin)
            auth.MapPost("/dev", LoginForDevelopment).RequireRateLimiting(RateLimits.Login);

        api.MapGet("/me", GetMe);
    }

    private static async Task<Results<Ok<MeResponse>, ProblemHttpResult>> LoginWithTelegram(
        JsonElement body,
        HttpContext http,
        TelegramLoginVerifier verifier,
        PlayerStore players,
        IAdminNotifier notifier,
        ILogger<TelegramLoginVerifier> logger,
        CancellationToken ct)
    {
        if (!TryReadFields(body, out var fields))
            return TypedResults.Problem("That login did not make sense.", statusCode: StatusCodes.Status400BadRequest);

        var (user, failure) = verifier.Verify(fields);
        switch (failure)
        {
            case TelegramLoginFailure.None:
                break;
            case TelegramLoginFailure.NoUsername:
                return TypedResults.Problem(
                    "Ye need a public @username on Telegram (Settings > Username) so the certificate can name ye.",
                    statusCode: StatusCodes.Status422UnprocessableEntity);
            case TelegramLoginFailure.Expired:
                return TypedResults.Problem("That login has gone stale. Try again.", statusCode: StatusCodes.Status401Unauthorized);
            default:
                logger.LogWarning("Rejected a Telegram login: {Failure}", failure);
                return TypedResults.Problem("Telegram would not vouch for ye.", statusCode: StatusCodes.Status401Unauthorized);
        }

        var player = user!.Value;
        if (await players.RecordLoginAsync(player.Id, player.Username, ct))
            notifier.Notify($"New player: @{player.Username}");

        await PlayerSession.SignInAsync(http, player);
        return TypedResults.Ok(await BuildMe(new PlayerIdentity(player.Id, player.Username), players, ct));
    }

    private static async Task<Ok<MeResponse>> LoginForDevelopment(
        DevLoginRequest request, HttpContext http, PlayerStore players, CancellationToken ct)
    {
        // A stable fake id derived from the handle. Negative, so it can never be a real Telegram id.
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(request.Handle));
        var id = -1 - (BinaryPrimitives.ReadInt64BigEndian(hash) & long.MaxValue);
        await players.RecordLoginAsync(id, request.Handle, ct);
        await PlayerSession.SignInAsync(http, new TelegramUser(id, request.Handle));
        return TypedResults.Ok(await BuildMe(new PlayerIdentity(id, request.Handle), players, ct));
    }

    private static async Task<IResult> Logout(HttpContext http)
    {
        await PlayerSession.SignOutAsync(http);
        return TypedResults.NoContent();
    }

    private static async Task<Ok<MeResponse>> GetMe(HttpContext http, PlayerStore players, CancellationToken ct)
    {
        var player = http.User.TryGetPlayer();
        return TypedResults.Ok(player is null ? new MeResponse(null, null) : await BuildMe(player.Value, players, ct));
    }

    private static async Task<MeResponse> BuildMe(PlayerIdentity player, PlayerStore players, CancellationToken ct)
    {
        var solve = await players.FindSolveAsync(player.TelegramId, ct);
        return new MeResponse(new PlayerResponse(player.Handle), solve?.ToResponse());
    }

    /// <summary>Flattens the widget's JSON object into the string pairs Telegram signed.</summary>
    private static bool TryReadFields(JsonElement body, out Dictionary<string, string> fields)
    {
        fields = [];
        if (body.ValueKind != JsonValueKind.Object)
            return false;

        foreach (var property in body.EnumerateObject())
        {
            var value = property.Value.ValueKind switch
            {
                JsonValueKind.String => property.Value.GetString(),
                JsonValueKind.Number => property.Value.GetRawText(),
                _ => null,
            };

            if (value is null || value.Length > MaxLoginFieldLength || fields.Count >= MaxLoginFields)
                return false;

            fields[property.Name] = value;
        }

        return true;
    }
}

public static class SolveMapping
{
    public static SolveResponse ToResponse(this Solve solve) =>
        new(solve.Rank, solve.SolvedAt, CertificateUrl(solve.CertificateId));

    public static string CertificateUrl(string certificateId) => string.Create(CultureInfo.InvariantCulture, $"/api/certificates/{certificateId}");
}
