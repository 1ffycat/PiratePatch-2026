using Microsoft.AspNetCore.Http.HttpResults;
using PiratePatch.Api.Auth;
using PiratePatch.Api.Challenge;
using PiratePatch.Api.Hosting;
using PiratePatch.Api.Notifications;
using PiratePatch.Api.Players;

namespace PiratePatch.Api.Endpoints;

public static class ChallengeEndpoints
{
    public static void MapChallengeEndpoints(this RouteGroupBuilder api)
    {
        var challenge = api.MapGroup("/challenge").RequireAuthorization();

        challenge.MapGet("/binary", DownloadBinary).RequireRateLimiting(RateLimits.Binary);
        challenge.MapPost("/submit", Submit).RequireRateLimiting(RateLimits.Submit);
    }

    private static FileContentHttpResult DownloadBinary(HttpContext http, ChallengeBuilder builder)
    {
        var player = http.User.GetPlayer();

        http.Response.Headers.CacheControl = "no-store";
        return TypedResults.File(builder.BuildFor(player.TelegramId, player.Handle), "application/octet-stream", "challenge");
    }

    private static async Task<Ok<SubmitResponse>> Submit(
        SubmitRequest request,
        HttpContext http,
        FlagCodec flags,
        PlayerStore players,
        IAdminNotifier notifier,
        ILogger<PlayerStore> logger,
        CancellationToken ct)
    {
        var player = http.User.GetPlayer();

        // The code must be genuine (signed by us) and must have been minted for this very player.
        var identity = flags.Verify(request.Code);
        if (identity is not { } flag || flag.TelegramId != player.TelegramId)
        {
            logger.LogInformation("Wrong treasure code from @{Handle}", player.Handle);
            return TypedResults.Ok(new SubmitResponse(false, null));
        }

        var (solve, created) = await players.RecordSolveAsync(player.TelegramId, flag.Handle, ct);
        if (created)
        {
            logger.LogInformation("@{Handle} solved the challenge as number {Rank}", flag.Handle, solve.Rank);
            notifier.Notify($"@{flag.Handle} cracked it - solver #{solve.Rank}");
        }

        return TypedResults.Ok(new SubmitResponse(true, solve.ToResponse()));
    }
}
