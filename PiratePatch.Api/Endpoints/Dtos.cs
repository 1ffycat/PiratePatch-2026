using System.ComponentModel.DataAnnotations;

namespace PiratePatch.Api.Endpoints;

public sealed record SiteConfigResponse(string BotUsername, string PublicKey, bool DevLogin);

public sealed record PlayerResponse(string Handle);

public sealed record SolveResponse(long Rank, DateTimeOffset SolvedAt, string CertificateUrl);

public sealed record MeResponse(PlayerResponse? Player, SolveResponse? Solve);

public sealed record SubmitResponse(bool Success, SolveResponse? Solve);

public sealed record LeaderboardResponse(long Total, IReadOnlyList<LeaderboardEntryResponse> Entries);

public sealed record LeaderboardEntryResponse(long Rank, string Handle, DateTimeOffset SolvedAt, string CertificateUrl);

public sealed class SubmitRequest
{
    [Required(ErrorMessage = "Ye must present a treasure code.")]
    [StringLength(256)]
    [RegularExpression(@"^P1R4T3-C0D3\{[A-Za-z0-9_-]{60,200}\}$", ErrorMessage = "That be not the shape of a treasure code.")]
    public string Code { get; init; } = "";
}

public sealed class DevLoginRequest
{
    [Required, RegularExpression(@"^[A-Za-z][A-Za-z0-9_]{4,31}$")]
    public string Handle { get; init; } = "";
}

public sealed class LeaderboardQuery
{
    // Nullable because minimal APIs treat non-nullable query properties as required, defaults or not.
    [Range(1, 200)]
    public int? Limit { get; init; }

    [Range(0, 1_000_000)]
    public int? Offset { get; init; }
}
