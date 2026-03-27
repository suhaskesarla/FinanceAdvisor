namespace FinanceAdvisor.Services;

using FinanceAdvisor.Core.DTOs;
using FinanceAdvisor.Core.Interfaces;
using Microsoft.Extensions.Logging;

/// <summary>Handles incoming Telegram webhook update processing.</summary>
public sealed partial class TelegramWebhookService : ITelegramWebhookService
{
    private readonly ILogger<TelegramWebhookService> _logger;

    /// <summary>Initializes a new instance of <see cref="TelegramWebhookService"/>.</summary>
    /// <param name="logger">Logger instance.</param>
    public TelegramWebhookService(ILogger<TelegramWebhookService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task HandleUpdateAsync(IncomingMessageDto message, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        LogMessageReceived(_logger, message.CorrelationId, message.FromFirstName, message.Text);
        await Task.CompletedTask;
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Processing message. CorrelationId={CorrelationId} From={FirstName} Text={Text}")]
    private static partial void LogMessageReceived(
        ILogger logger, string correlationId, string firstName, string text);
}
