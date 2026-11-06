namespace PiratePatch.Api.Auth;

public readonly record struct TelegramUser(long Id, string Username);

public enum TelegramLoginFailure
{
    None,
    Malformed,
    BadSignature,
    NoUsername,
}
