using System.ComponentModel.DataAnnotations;

namespace PiratePatch.Api.Configuration;

public sealed class SiteOptions
{
    public const string Section = "Site";

    /// <summary>Public origin of the site, e.g. <c>https://ahoy.example.org</c>. Used for the origin check and printed on certificates.</summary>
    [Required]
    public required Uri PublicUrl { get; init; }

    /// <summary>Lifetime of a login session.</summary>
    [Range(1, 24 * 30)]
    public int SessionHours { get; init; } = 12;

    /// <summary>Directory for ASP.NET Data Protection keys (they protect the session cookie). Must persist across restarts.</summary>
    public string? KeysPath { get; init; }

    /// <summary>Lets <c>POST /api/auth/dev</c> log in without Telegram. Honoured in the Development environment only.</summary>
    public bool DevLogin { get; init; }
}
