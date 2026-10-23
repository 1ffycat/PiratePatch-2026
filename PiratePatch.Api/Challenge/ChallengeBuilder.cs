using System.Text;

namespace PiratePatch.Api.Challenge;

/// <summary>Produces a player's personal copy of the challenge program.</summary>
public sealed class ChallengeBuilder(ChallengeTemplate template, FlagCodec flags, PasswordDeriver passwords)
{
    /// <summary>The key the challenge program XORs its treasure code with. Matches <c>xor_key</c> in challenge.c.</summary>
    private static readonly byte[] XorKey = "CAPN_HOOK"u8.ToArray();

    public byte[] BuildFor(long telegramId, string handle)
    {
        var flag = Encoding.ASCII.GetBytes(flags.Issue(telegramId, handle));
        for (var i = 0; i < flag.Length; i++)
            flag[i] ^= XorKey[i % XorKey.Length];

        return template.Render(handle, passwords.PasswordHashFor(telegramId), flag);
    }
}
