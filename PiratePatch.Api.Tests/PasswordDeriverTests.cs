using System.Security.Cryptography;
using System.Text;

namespace PiratePatch.Api.Tests;

public class PasswordDeriverTests
{
    private readonly PasswordDeriver _passwords = new(new SigningKey(new byte[32]));

    [Fact]
    public void PasswordIsStablePerPlayerAndDiffersBetweenPlayers()
    {
        Assert.Equal(_passwords.PasswordFor(1), _passwords.PasswordFor(1));
        Assert.NotEqual(_passwords.PasswordFor(1), _passwords.PasswordFor(2));
        Assert.Matches("^SeaLegs_[0-9a-f]{12}$", _passwords.PasswordFor(1));
    }

    [Fact]
    public void PasswordDependsOnTheServerKey()
    {
        var other = new PasswordDeriver(new SigningKey(Enumerable.Repeat((byte)1, 32).ToArray()));

        Assert.NotEqual(_passwords.PasswordFor(1), other.PasswordFor(1));
    }

    [Fact]
    public void HashIsUppercaseHexSha256OfThePassword()
    {
        var expected = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(_passwords.PasswordFor(7))));

        Assert.Equal(expected, _passwords.PasswordHashFor(7));
        Assert.Matches("^[0-9A-F]{64}$", expected);
    }
}
