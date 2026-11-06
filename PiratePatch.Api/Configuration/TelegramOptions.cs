using System.ComponentModel.DataAnnotations;

namespace PiratePatch.Api.Configuration;

public sealed class TelegramOptions
{
    public const string Section = "Telegram";

    /// <summary>Username of the bot behind the login widget (without the @).</summary>
    [Required, RegularExpression(@"^[A-Za-z][A-Za-z0-9_]{4,31}$")]
    public required string BotUsername { get; init; }

    /// <summary>Bot token from @BotFather, used for optional admin notifications.</summary>
    [Required, RegularExpression(@"^\d{5,}:[A-Za-z0-9_-]{30,}$", ErrorMessage = "The bot token does not look like one from @BotFather.")]
    public required string BotToken { get; init; }

    /// <summary>OIDC client ID and secret from the BotFather Login Widget settings.</summary>
    [Required, RegularExpression(@"^\d+$")]
    public required string ClientId { get; init; }

    [Required]
    public required string ClientSecret { get; init; }

    /// <summary>Chat that receives "new player" and "solved" messages. Notifications are off when unset.</summary>
    public long? AdminChatId { get; init; }

    public Uri ApiBaseUrl { get; init; } = new("https://api.telegram.org/");
}
