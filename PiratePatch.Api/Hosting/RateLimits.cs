using System.Threading.RateLimiting;
using PiratePatch.Api.Auth;

namespace PiratePatch.Api.Hosting;

/// <summary>
/// Layers of rate limiting, cheapest first: a cap on requests in flight, a per-address budget for the whole API,
/// and tighter budgets on the endpoints that do real work. nginx adds its own limits in front of all of this.
/// </summary>
public static class RateLimits
{
    public const string Login = "login";
    public const string Binary = "binary";
    public const string Submit = "submit";
    public const string Certificate = "certificate";

    public static IServiceCollection AddPiratePatchRateLimiting(this IServiceCollection services) =>
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, ct) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds + 1).ToString();

                await Results.Problem("Too many requests. Slow yer oars, matey.", statusCode: StatusCodes.Status429TooManyRequests)
                    .ExecuteAsync(context.HttpContext);
            };

            options.GlobalLimiter = PartitionedRateLimiter.CreateChained(
                PartitionedRateLimiter.Create<HttpContext, string>(_ => RateLimitPartition.GetConcurrencyLimiter("all", _ => new ConcurrencyLimiterOptions
                {
                    PermitLimit = 128,
                    QueueLimit = 0,
                })),
                PartitionedRateLimiter.Create<HttpContext, string>(http => RateLimitPartition.GetSlidingWindowLimiter(ClientAddress(http), _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = 120,
                    Window = TimeSpan.FromMinutes(1),
                    SegmentsPerWindow = 6,
                    QueueLimit = 0,
                })));

            options.AddPolicy(Login, http => PerAddress(http, 10));
            options.AddPolicy(Certificate, http => PerAddress(http, 20));
            options.AddPolicy(Binary, http => PerPlayer(http, 6));
            options.AddPolicy(Submit, http => PerPlayer(http, 10));
        });

    private static RateLimitPartition<string> PerAddress(HttpContext http, int perMinute) =>
        RateLimitPartition.GetSlidingWindowLimiter($"ip:{ClientAddress(http)}", _ => Window(perMinute));

    private static RateLimitPartition<string> PerPlayer(HttpContext http, int perMinute) =>
        RateLimitPartition.GetSlidingWindowLimiter(
            http.User.TryGetPlayer() is { } player ? $"player:{player.TelegramId}" : $"ip:{ClientAddress(http)}",
            _ => Window(perMinute));

    private static SlidingWindowRateLimiterOptions Window(int perMinute) => new()
    {
        PermitLimit = perMinute,
        Window = TimeSpan.FromMinutes(1),
        SegmentsPerWindow = 6,
        QueueLimit = 0,
    };

    /// <summary>The caller's address as set by the forwarded-headers middleware (Kestrel itself sees only nginx's socket).</summary>
    private static string ClientAddress(HttpContext http) => http.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
