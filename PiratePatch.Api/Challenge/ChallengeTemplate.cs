using System.Buffers.Binary;
using System.Text;

namespace PiratePatch.Api.Challenge;

/// <summary>
/// The challenge program as built by Nix, plus the knowledge of where its per-player slots are.
/// Issuing a binary is a copy and a few overwrites: nothing is compiled and no input reaches a compiler.
/// The layout mirrors <c>struct slots</c> in <c>challenge/challenge.c</c>.
/// </summary>
public sealed class ChallengeTemplate
{
    public const int NameCapacity = 64;
    public const int PasswordHashLength = 64;
    public const int FlagCapacity = 192;

    private const int MagicLength = 16;
    private const int NameOffset = 16;
    private const int HashOffset = 80;
    private const int FlagLengthOffset = 148;
    private const int FlagOffset = 152;
    private const int SlotsLength = 344;

    private static readonly byte[] Magic = "~PIRATEPATCH~V1~"u8.ToArray();

    private readonly byte[] _template;
    private readonly int _slots;

    public ChallengeTemplate(byte[] template)
    {
        _template = template;
        _slots = LocateSlots(template);
    }

    public static ChallengeTemplate Load(string path) => new(File.ReadAllBytes(path));

    /// <param name="name">Shown by the program; must fit its slot, ASCII only.</param>
    /// <param name="passwordHashHex">Uppercase hex SHA-256 of the player's password.</param>
    /// <param name="encryptedFlag">The XOR-ed treasure code.</param>
    public byte[] Render(string name, string passwordHashHex, ReadOnlySpan<byte> encryptedFlag)
    {
        var nameBytes = Encoding.ASCII.GetBytes(name);
        if (nameBytes.Length is 0 or >= NameCapacity || !name.All(c => char.IsAsciiLetterOrDigit(c) || c == '_'))
            throw new ArgumentException("The name does not fit the challenge slot.", nameof(name));
        if (passwordHashHex.Length != PasswordHashLength)
            throw new ArgumentException("Expected a hex-encoded SHA-256.", nameof(passwordHashHex));
        if (encryptedFlag.Length is 0 or > FlagCapacity)
            throw new ArgumentException("The flag does not fit the challenge slot.", nameof(encryptedFlag));

        var binary = (byte[])_template.Clone();
        var slots = binary.AsSpan(_slots, SlotsLength);
        slots.Clear();

        nameBytes.CopyTo(slots[NameOffset..]);
        Encoding.ASCII.GetBytes(passwordHashHex, slots[HashOffset..]);
        BinaryPrimitives.WriteUInt32LittleEndian(slots[FlagLengthOffset..], (uint)encryptedFlag.Length);
        encryptedFlag.CopyTo(slots[FlagOffset..]);

        return binary;
    }

    private static int LocateSlots(byte[] template)
    {
        var first = template.AsSpan().IndexOf(Magic);
        if (first < 0)
            throw new InvalidDataException("The challenge template has no slots marker. Is it the program built from challenge/challenge.c?");
        if (template.AsSpan(first + 1).IndexOf(Magic) >= 0)
            throw new InvalidDataException("The challenge template contains more than one slots marker.");
        if (first + SlotsLength > template.Length)
            throw new InvalidDataException("The challenge template is truncated.");

        return first;
    }
}
