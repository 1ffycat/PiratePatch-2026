using System.Threading.Channels;
using Microsoft.Extensions.Options;
using PiratePatch.Api.Configuration;

namespace PiratePatch.Api.Notifications;

public interface IAdminNotifier
{
    /// <summary>Queues a message for the admin chat. Never blocks and never throws; messages may be dropped under load.</summary>
    void Notify(string message);
}

/// <summary>Sends admin messages through the Bot API from a background queue, so a slow Telegram never slows a request.</summary>
public sealed class TelegramNotifier(
    IHttpClientFactory httpFactory,
    IOptions<TelegramOptions> options,
    ILogger<TelegramNotifier> logger) : BackgroundService, IAdminNotifier
{
    public const string HttpClientName = nameof(TelegramNotifier);

    private readonly Channel<string> _queue = Channel.CreateBounded<string>(new BoundedChannelOptions(64)
    {
        FullMode = BoundedChannelFullMode.DropOldest,
        SingleReader = true,
    });

    public void Notify(string message)
    {
        if (options.Value.AdminChatId is not null)
            _queue.Writer.TryWrite(message);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (options.Value.AdminChatId is not { } chatId)
        {
            logger.LogInformation("No admin chat configured; Telegram notifications are off");
            return;
        }

        var http = httpFactory.CreateClient(HttpClientName);
        var endpoint = new Uri(options.Value.ApiBaseUrl, $"bot{options.Value.BotToken}/sendMessage");

        await foreach (var message in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var response = await http.PostAsJsonAsync(endpoint, new { chat_id = chatId, text = message }, stoppingToken);
                if (!response.IsSuccessStatusCode)
                    logger.LogWarning("Telegram rejected a notification with status {Status}", (int)response.StatusCode);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !stoppingToken.IsCancellationRequested)
            {
                // The token is part of the URL, so log the type only, never the exception message.
                logger.LogWarning("Could not reach Telegram ({ErrorType})", ex.GetType().Name);
            }

            // Telegram allows about one message per second to a chat.
            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }
}
