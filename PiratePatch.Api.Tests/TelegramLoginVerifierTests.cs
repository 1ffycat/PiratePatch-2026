using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Time.Testing;
using PiratePatch.Api.Auth;

namespace PiratePatch.Api.Tests;

public class TelegramLoginVerifierTests
{
    private const string BotToken = "123456789:TEST-ONLY-token-for-unit-tests-0000";
    private static readonly DateTimeOffset Now = new(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider _clock = new(Now);
    private readonly TelegramLoginVerifier _verifier;

    public TelegramLoginVerifierTests()
    {
        _verifier = new TelegramLoginVerifier(BotToken, TimeSpan.FromHours(1), _clock);
    }

    /// <summary>Signs fields the way telegram.org documents it, independently of the code under test.</summary>
    private static Dictionary<string, string> Signed(Dictionary<string, string> fields, string token = BotToken)
    {
        var check = string.Join("\n", fields.OrderBy(f => f.Key, StringComparer.Ordinal).Select(f => $"{f.Key}={f.Value}"));
        var secret = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        fields["hash"] = Convert.ToHexStringLower(HMACSHA256.HashData(secret, Encoding.UTF8.GetBytes(check)));
        return fields;
    }

    private static Dictionary<string, string> Login(DateTimeOffset? at = null, string? username = "blackbeard", string token = BotToken)
    {
        var fields = new Dictionary<string, string>
        {
            ["id"] = "123456789",
            ["first_name"] = "Эдвард",
            ["auth_date"] = (at ?? Now).ToUnixTimeSeconds().ToString(),
            ["photo_url"] = "https://t.me/i/userpic/320/x.jpg",
        };
        if (username is not null)
            fields["username"] = username;
        return Signed(fields, token);
    }

    [Fact]
    public void ValidLoginIsAccepted()
    {
        var (user, failure) = _verifier.Verify(Login());

        Assert.Equal(TelegramLoginFailure.None, failure);
        Assert.Equal(new TelegramUser(123456789, "blackbeard"), user);
    }

    [Fact]
    public void LoginSignedWithAnotherBotIsRejected()
    {
        Assert.Equal(TelegramLoginFailure.BadSignature, _verifier.Verify(Login(token: "987654321:SOMEONE-ELSES-bot-token-000000000")).Failure);
    }

    [Fact]
    public void ChangedFieldsInvalidateTheSignature()
    {
        var fields = Login();
        fields["id"] = "1";

        Assert.Equal(TelegramLoginFailure.BadSignature, _verifier.Verify(fields).Failure);
    }

    [Fact]
    public void ExtraUnsignedFieldInvalidatesTheSignature()
    {
        var fields = Login();
        fields["is_admin"] = "1";

        Assert.Equal(TelegramLoginFailure.BadSignature, _verifier.Verify(fields).Failure);
    }

    [Fact]
    public void StaleLoginIsRejected()
    {
        var fields = Login(Now.AddHours(-2));

        Assert.Equal(TelegramLoginFailure.Expired, _verifier.Verify(fields).Failure);
    }

    [Fact]
    public void LoginFromTheFarFutureIsRejected()
    {
        Assert.Equal(TelegramLoginFailure.Expired, _verifier.Verify(Login(Now.AddHours(1))).Failure);
    }

    [Fact]
    public void LoginAgeIsMeasuredAgainstTheClock()
    {
        var fields = Login(Now);
        _clock.Advance(TimeSpan.FromMinutes(59));
        Assert.Equal(TelegramLoginFailure.None, _verifier.Verify(fields).Failure);

        _clock.Advance(TimeSpan.FromMinutes(2));
        Assert.Equal(TelegramLoginFailure.Expired, _verifier.Verify(fields).Failure);
    }

    [Fact]
    public void AccountWithoutUsernameIsRejected()
    {
        Assert.Equal(TelegramLoginFailure.NoUsername, _verifier.Verify(Login(username: null)).Failure);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("has space")]
    [InlineData("1startsWithDigit")]
    public void UsernamesTelegramWouldNotIssueAreRejected(string username)
    {
        Assert.Equal(TelegramLoginFailure.NoUsername, _verifier.Verify(Login(username: username)).Failure);
    }

    [Theory]
    [InlineData("hash")]
    [InlineData("id")]
    [InlineData("auth_date")]
    public void MissingRequiredFieldIsMalformed(string missing)
    {
        var fields = Login();
        fields.Remove(missing);

        Assert.Equal(TelegramLoginFailure.Malformed, _verifier.Verify(fields).Failure);
    }

    [Fact]
    public void GarbageHashIsMalformed()
    {
        var fields = Login();
        fields["hash"] = new string('z', 64);

        Assert.Equal(TelegramLoginFailure.Malformed, _verifier.Verify(fields).Failure);
    }
}
