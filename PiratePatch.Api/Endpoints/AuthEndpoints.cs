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
    public static void MapAuthEndpoints(this RouteGroupBuilder api, bool devLogin)
    {
        var auth = api.MapGroup("/auth");
        auth.MapGet("/telegram", StartTelegram).RequireRateLimiting(RateLimits.Login);
        auth.MapGet("/telegram/callback", FinishTelegram).RequireRateLimiting(RateLimits.Login);
        auth.MapPost("/logout", (Delegate)Logout);
        if (devLogin)
            auth.MapPost("/dev", LoginForDevelopment).RequireRateLimiting(RateLimits.Login);
        api.MapGet("/me", GetMe);
    }

    private static IResult StartTelegram(HttpContext http, TelegramOidc oidc) => oidc.Start(http);

    private static async Task<IResult> FinishTelegram(
        HttpContext http, TelegramOidc oidc, PlayerStore players, IAdminNotifier notifier, CancellationToken ct)
    {
        var (player, failure) = await oidc.FinishAsync(http, ct);
        if (player is null)
            return Results.Redirect("/challenge?login=" + (failure == TelegramLoginFailure.NoUsername ? "username" : "failed"));

        if (await players.RecordLoginAsync(player.Value.Id, player.Value.Username, ct))
            notifier.Notify($"New player: @{player.Value.Username}");
        await PlayerSession.SignInAsync(http, player.Value);
        return Results.Redirect("/challenge");
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


}

public static class SolveMapping
{
    public static SolveResponse ToResponse(this Solve solve) =>
        new(solve.Rank, solve.SolvedAt, CertificateUrl(solve.CertificateId));

    public static string CertificateUrl(string certificateId) => string.Create(CultureInfo.InvariantCulture, $"/api/certificates/{certificateId}");
}
