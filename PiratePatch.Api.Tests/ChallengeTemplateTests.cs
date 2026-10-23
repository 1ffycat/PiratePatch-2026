using System.Buffers.Binary;
using System.Text;
using PiratePatch.Api.Challenge;

namespace PiratePatch.Api.Tests;

public class ChallengeTemplateTests
{
    private const int SlotsAt = 100;
    private static readonly byte[] Magic = "~PIRATEPATCH~V1~"u8.ToArray();
    private static readonly string Hash = new('A', 64);

    /// <summary>A stand-in for the real program: filler bytes, with the slots (filled with 0xEE) in the middle.</summary>
    private static byte[] FakeProgram(int magicCount = 1)
    {
        var program = new byte[600];
        Array.Fill(program, (byte)0x90);
        Array.Fill(program, (byte)0xEE, SlotsAt, 344);
        Magic.CopyTo(program, SlotsAt);
        if (magicCount > 1)
            Magic.CopyTo(program, 500);
        return program;
    }

    [Fact]
    public void RenderWritesEveryFieldAtItsOffset()
    {
        var binary = new ChallengeTemplate(FakeProgram()).Render("blackbeard", Hash, [1, 2, 3]);
        var slots = binary.AsSpan(SlotsAt, 344);

        Assert.Equal(600, binary.Length);
        Assert.Equal("blackbeard", Encoding.ASCII.GetString(slots[16..80]).TrimEnd('\0'));
        Assert.Equal(Hash, Encoding.ASCII.GetString(slots[80..145]).TrimEnd('\0'));
        Assert.Equal(3u, BinaryPrimitives.ReadUInt32LittleEndian(slots[148..]));
        Assert.Equal<byte>([1, 2, 3], slots[152..155].ToArray());
    }

    [Fact]
    public void RenderRemovesTheMarkerAndLeavesNoStaleBytes()
    {
        var binary = new ChallengeTemplate(FakeProgram()).Render("blackbeard", Hash, [1, 2, 3]);

        Assert.True(binary.AsSpan().IndexOf(Magic) < 0);
        Assert.DoesNotContain((byte)0xEE, binary);
    }

    [Fact]
    public void RenderLeavesTheRestOfTheProgramUntouched()
    {
        var original = FakeProgram();
        var binary = new ChallengeTemplate(original).Render("blackbeard", Hash, [1]);

        Assert.Equal(original[..SlotsAt], binary[..SlotsAt]);
        Assert.Equal(original[(SlotsAt + 344)..], binary[(SlotsAt + 344)..]);
    }

    [Fact]
    public void RenderDoesNotModifyTheTemplate()
    {
        var template = new ChallengeTemplate(FakeProgram());
        template.Render("blackbeard", Hash, [1]);
        var second = template.Render("anne_bonny", Hash, [9]);

        Assert.Equal("anne_bonny", Encoding.ASCII.GetString(second.AsSpan(SlotsAt + 16, 64)).TrimEnd('\0'));
    }

    [Fact]
    public void TemplateWithoutOrWithRepeatedMarkerIsRejected()
    {
        Assert.Throws<InvalidDataException>(() => new ChallengeTemplate(new byte[600]));
        Assert.Throws<InvalidDataException>(() => new ChallengeTemplate(FakeProgram(magicCount: 2)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("a\"; system(\"id\"); //")]
    [InlineData("no spaces")]
    [InlineData("тест")]
    public void NamesThatAreNotHandlesAreRejected(string name)
    {
        Assert.Throws<ArgumentException>(() => new ChallengeTemplate(FakeProgram()).Render(name, Hash, [1]));
    }

    [Fact]
    public void OversizedValuesAreRejected()
    {
        var template = new ChallengeTemplate(FakeProgram());

        Assert.Throws<ArgumentException>(() => template.Render(new string('a', 64), Hash, [1]));
        Assert.Throws<ArgumentException>(() => template.Render("a", "ABC", [1]));
        Assert.Throws<ArgumentException>(() => template.Render("a", Hash, new byte[193]));
    }

    [Fact]
    public void BuilderXorsTheFlagWithTheKeyKnownToTheProgram()
    {
        var key = new SigningKey(new byte[32]);
        var flags = new FlagCodec(key);
        var binary = new ChallengeBuilder(new ChallengeTemplate(FakeProgram()), flags, new PasswordDeriver(key)).BuildFor(5, "blackbeard");

        var slots = binary.AsSpan(SlotsAt, 344);
        var length = (int)BinaryPrimitives.ReadUInt32LittleEndian(slots[148..]);
        var decrypted = slots.Slice(152, length).ToArray();
        for (var i = 0; i < decrypted.Length; i++)
            decrypted[i] ^= "CAPN_HOOK"u8[i % 9];

        Assert.Equal(flags.Issue(5, "blackbeard"), Encoding.ASCII.GetString(decrypted));
    }
}
