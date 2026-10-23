using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using PiratePatch.Api.Certificates;
using PiratePatch.Api.Configuration;
using PiratePatch.Api.Hosting;
using PiratePatch.Api.Players;

namespace PiratePatch.Api.Endpoints;

public static partial class PublicEndpoints
{
    [GeneratedRegex("^[0-9a-f]{20}$")]
    private static partial Regex CertificateIdShape();

    // Rendering a certificate is the one expensive thing an anonymous visitor can ask for.
    private static readonly SemaphoreSlim RenderGate = new(2);

    public static void MapPublicEndpoints(this RouteGroupBuilder api, bool devLogin)
    {
        api.MapGet("/config", (IOptions<TelegramOptions> telegram, SigningKey key) =>
            TypedResults.Ok(new SiteConfigResponse(telegram.Value.BotUsername, Convert.ToBase64String(key.PublicKey), devLogin)));

        api.MapGet("/signing-key", (SigningKey key) => TypedResults.Text(key.PublicKeyPem, "application/x-pem-file"));

        api.MapGet("/leaderboard", GetLeaderboard);

        api.MapGet("/certificates/{certificateId}", GetCertificate).RequireRateLimiting(RateLimits.Certificate);

        api.MapGet("/health", async (PlayerStore players, CancellationToken ct) =>
            await players.PingAsync(ct) ? Results.Ok() : Results.StatusCode(StatusCodes.Status503ServiceUnavailable));
    }

    private static async Task<Ok<LeaderboardResponse>> GetLeaderboard([AsParameters] LeaderboardQuery query, PlayerStore players, CancellationToken ct)
    {
        var board = await players.GetLeaderboardAsync(query.Limit ?? 50, query.Offset ?? 0, ct);
        var entries = board.Entries
            .Select(e => new LeaderboardEntryResponse(e.Rank, e.Username, e.SolvedAt, SolveMapping.CertificateUrl(e.CertificateId)))
            .ToList();
        return TypedResults.Ok(new LeaderboardResponse(board.Total, entries));
    }

    private static async Task<Results<FileContentHttpResult, NotFound>> GetCertificate(
        string certificateId,
        HttpContext http,
        PlayerStore players,
        FlagCodec flags,
        CertificateRenderer renderer,
        IMemoryCache cache,
        IOptions<SiteOptions> site,
        CancellationToken ct)
    {
        if (!CertificateIdShape().IsMatch(certificateId))
            return TypedResults.NotFound();

        var solve = await players.FindSolveByCertificateAsync(certificateId, ct);
        if (solve is null)
            return TypedResults.NotFound();

        if (!cache.TryGetValue(certificateId, out byte[]? pdf))
        {
            await RenderGate.WaitAsync(ct);
            try
            {
                pdf = renderer.Render(new CertificateData(solve.Username, flags.Issue(solve.TelegramId, solve.Username), solve.SolvedAt, site.Value.PublicUrl));
                cache.Set(certificateId, pdf, new MemoryCacheEntryOptions { Size = 1, AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10) });
            }
            finally
            {
                RenderGate.Release();
            }
        }

        http.Response.Headers.CacheControl = "public, max-age=3600";
        http.Response.Headers.ContentDisposition = $"inline; filename=\"certificate-{solve.Username}.pdf\"";
        return TypedResults.File(pdf!, "application/pdf");
    }
}
