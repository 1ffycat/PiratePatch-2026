using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace PiratePatch.Api.Configuration;

public sealed class SigningOptions
{
    public const string Section = "Signing";

    /// <summary>Base64 of a 32-byte Ed25519 seed. Signs the treasure codes and derives the per-player passwords.</summary>
    [Required]
    public required string PrivateKey { get; init; }

    public byte[] DecodeSeed() => Convert.FromBase64String(PrivateKey.Trim());
}

public sealed class SigningOptionsValidator : IValidateOptions<SigningOptions>
{
    public ValidateOptionsResult Validate(string? name, SigningOptions options)
    {
        Span<byte> seed = stackalloc byte[SigningKey.SeedLength + 1];
        var valid = Convert.TryFromBase64String(options.PrivateKey.Trim(), seed, out var written) && written == SigningKey.SeedLength;

        return valid
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail($"{SigningOptions.Section}:PrivateKey must be the base64 encoding of exactly {SigningKey.SeedLength} bytes.");
    }
}
