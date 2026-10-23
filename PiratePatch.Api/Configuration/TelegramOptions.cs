using System.ComponentModel.DataAnnotations;

namespace PiratePatch.Api.Configuration;

public sealed class TelegramOptions
{
    public const string Section = "Telegram";

    /// <summary>Username of the bot behind the login widget (without the @).</summary>
    [Required, RegularExpression(@"^[A-Za-z][A-Za-z0-9_]{4,31}$")]
    public required string BotUsername { get; init; }

    /// <summary>Token from @BotFather. Verifies login payloads and, optionally, sends admin notifications.</summary>
    [Required, RegularExpression(@"^\d{5,}:[A-Za-z0-9_-]{30,}$", ErrorMessage = "The bot token does not look like one from @BotFather.")]
    public required string BotToken { get; init; }

    /// <summary>Chat that receives "new player" and "solved" messages. Notifications are off when unset.</summary>
    public long? AdminChatId { get; init; }

    /// <summary>How old a login payload may be before it is rejected.</summary>
    [Range(30, 86_400)]
    public int LoginMaxAgeSeconds { get; init; } = 3_600;

    public Uri ApiBaseUrl { get; init; } = new("https://api.telegram.org/");
}
