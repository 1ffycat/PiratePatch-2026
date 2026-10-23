namespace PiratePatch.Api.Configuration;

/// <summary>Locations of the files the API ships with. The defaults point at the build output; Nix overrides the challenge template.</summary>
public sealed class AssetOptions
{
    public const string Section = "Assets";

    private static string Bundled(string file) => Path.Combine(AppContext.BaseDirectory, "Assets", file);

    /// <summary>The compiled challenge program with empty per-player slots (see <c>challenge/challenge.c</c>).</summary>
    public string ChallengeTemplate { get; init; } = Bundled("challenge-template");

    public string CertificateTemplate { get; init; } = Bundled("template.pdf");

    public string CertificateFont { get; init; } = Bundled("font.ttf");
}
