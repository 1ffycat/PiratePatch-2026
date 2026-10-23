namespace PiratePatch.Api.Tests;

public class FlagCodecTests
{
    private static readonly SigningKey Key = new(Enumerable.Range(1, 32).Select(i => (byte)i).ToArray());
    private readonly FlagCodec _codec = new(Key);

    [Fact]
    public void IssuedFlagVerifiesAndNamesThePlayer()
    {
        var flag = _codec.Issue(123456789, "One_Eyed_Jack");

        Assert.Equal(new FlagIdentity(123456789, "One_Eyed_Jack"), _codec.Verify(flag));
    }

    [Fact]
    public void FlagsAreDeterministic()
    {
        Assert.Equal(_codec.Issue(42, "blackbeard"), _codec.Issue(42, "blackbeard"));
    }

    [Fact]
    public void FlagFitsTheChallengeSlotForTheLongestHandle()
    {
        var flag = _codec.Issue(long.MaxValue, new string('a', 32));

        Assert.True(flag.Length <= Challenge.ChallengeTemplate.FlagCapacity, $"{flag.Length} bytes");
        Assert.Matches(FlagCodec.Shape(), flag);
    }

    [Fact]
    public void NegativeIdsRoundTrip()
    {
        Assert.Equal(-7, _codec.Verify(_codec.Issue(-7, "dev_pirate"))?.TelegramId);
    }

    [Fact]
    public void TamperedFlagIsRejected()
    {
        var flag = _codec.Issue(42, "blackbeard");
        var middle = flag.Length / 2;
        var tampered = flag[..middle] + (flag[middle] == 'A' ? 'B' : 'A') + flag[(middle + 1)..];

        Assert.Null(_codec.Verify(tampered));
    }

    [Fact]
    public void FlagFromAnotherKeyIsRejected()
    {
        var other = new FlagCodec(new SigningKey(new byte[32]));

        Assert.Null(_codec.Verify(other.Issue(42, "blackbeard")));
    }

    [Theory]
    [InlineData("")]
    [InlineData("P1R4T3-C0D3{}")]
    [InlineData("P1R4T3-C0D3{AAAA}")]
    [InlineData("not a flag")]
    public void JunkIsRejected(string junk)
    {
        Assert.Null(_codec.Verify(junk));
    }

    [Theory]
    [InlineData("")]
    [InlineData("has space")]
    [InlineData("quote\"")]
    [InlineData("тест")]
    public void InvalidHandlesCannotBeIssued(string handle)
    {
        Assert.Throws<ArgumentException>(() => _codec.Issue(1, handle));
    }

    [Fact]
    public void SignatureVerifiesWithTheRawPublicKeyAlone()
    {
        var message = "hello"u8.ToArray();
        var signature = Key.Sign(message);

        Assert.True(SigningKey.Verify(Key.PublicKey, message, signature));
        Assert.False(SigningKey.Verify(Key.PublicKey, "hullo"u8, signature));
    }

    [Fact]
    public void PublicKeyPemIsSpki()
    {
        var pem = Key.PublicKeyPem;
        var der = Convert.FromBase64String(pem.Split('\n')[1]);

        Assert.Equal(44, der.Length);
        Assert.Equal(Key.PublicKey, der[12..]);
    }
}
