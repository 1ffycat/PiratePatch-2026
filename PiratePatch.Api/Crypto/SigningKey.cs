using System.Security.Cryptography;
using System.Text;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;

namespace PiratePatch.Api;

/// <summary>
/// The server's Ed25519 identity. Everything secret in the game derives from the one seed:
/// the signatures on treasure codes and the per-player passwords.
/// </summary>
public sealed class SigningKey
{
    public const int SeedLength = 32;
    public const int PublicKeyLength = 32;
    public const int SignatureLength = 64;

    // SubjectPublicKeyInfo for Ed25519 is this fixed prefix followed by the raw key.
    private static readonly byte[] SpkiPrefix = [0x30, 0x2a, 0x30, 0x05, 0x06, 0x03, 0x2b, 0x65, 0x70, 0x03, 0x21, 0x00];

    private readonly byte[] _seed;
    private readonly Ed25519PrivateKeyParameters _privateKey;

    public SigningKey(ReadOnlySpan<byte> seed)
    {
        if (seed.Length != SeedLength)
            throw new ArgumentException($"The seed must be {SeedLength} bytes.", nameof(seed));

        _seed = seed.ToArray();
        _privateKey = new Ed25519PrivateKeyParameters(_seed, 0);
        PublicKey = _privateKey.GeneratePublicKey().GetEncoded();
    }

    /// <summary>Raw 32-byte Ed25519 public key.</summary>
    public byte[] PublicKey { get; }

    /// <summary>The public key as SPKI PEM, the form <c>openssl pkeyutl -verify -pubin</c> expects.</summary>
    public string PublicKeyPem =>
        $"-----BEGIN PUBLIC KEY-----\n{Convert.ToBase64String([.. SpkiPrefix, .. PublicKey])}\n-----END PUBLIC KEY-----\n";

    public byte[] Sign(ReadOnlySpan<byte> message)
    {
        var signer = new Ed25519Signer();
        signer.Init(true, _privateKey);
        signer.BlockUpdate(message.ToArray(), 0, message.Length);
        return signer.GenerateSignature();
    }

    public static bool Verify(ReadOnlySpan<byte> publicKey, ReadOnlySpan<byte> message, ReadOnlySpan<byte> signature)
    {
        if (publicKey.Length != PublicKeyLength || signature.Length != SignatureLength)
            return false;

        var signer = new Ed25519Signer();
        signer.Init(false, new Ed25519PublicKeyParameters(publicKey.ToArray(), 0));
        signer.BlockUpdate(message.ToArray(), 0, message.Length);
        return signer.VerifySignature(signature.ToArray());
    }

    /// <summary>Derives a stable secret for <paramref name="purpose"/> (HKDF-SHA256 over the seed).</summary>
    public byte[] DeriveSecret(string purpose, int length) =>
        HKDF.DeriveKey(HashAlgorithmName.SHA256, _seed, length, salt: [], info: Encoding.UTF8.GetBytes(purpose));
}
