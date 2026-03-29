namespace FinanceAdvisor.Services;

using FinanceAdvisor.Core.Constants;
using FinanceAdvisor.Core.DTOs;
using FinanceAdvisor.Core.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;

/// <summary>Handles incoming Telegram webhook update processing.</summary>
internal sealed partial class TelegramWebhookService : ITelegramWebhookService
{
    private readonly ITelegramBotClient _botClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<TelegramWebhookService> _logger;

    /// <summary>Initializes a new instance of <see cref="TelegramWebhookService"/>.</summary>
    /// <param name="botClient">Telegram Bot API client.</param>
    /// <param name="cache">Memory cache used to check for a live Zerodha access token.</param>
    /// <param name="logger">Logger instance.</param>
    public TelegramWebhookService(
        ITelegramBotClient botClient,
        IMemoryCache cache,
        ILogger<TelegramWebhookService> logger)
    {
        _botClient = botClient;
        _cache = cache;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task HandleUpdateAsync(IncomingMessageDto message, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        LogMessageReceived(_logger, message.CorrelationId, message.FromFirstName, message.Text);

        bool isPortfolioCommand =
            message.Text.Contains("portfolio", StringComparison.OrdinalIgnoreCase) ||
            message.Text.Contains("/login", StringComparison.OrdinalIgnoreCase);

        if (!isPortfolioCommand)
        {
            return;
        }

        if (_cache.TryGetValue(AppConstants.CacheKeys.ZerodhaAccessToken, out _))
        {
            LogTokenCacheHit(_logger, message.CorrelationId);
            await _botClient.SendMessage(
                message.ChatId,
                AppConstants.BotMessages.ZerodhaSessionActive,
                cancellationToken: ct);
            return;
        }

        LogInviteSent(_logger, message.CorrelationId);

        InlineKeyboardMarkup keyboard = new(
        [[
            InlineKeyboardButton.WithUrl(
                AppConstants.BotMessages.ZerodhaInviteButtonText,
                AppConstants.AppEndpoints.ZerodhaLoginInvite)
        ]]);

        await _botClient.SendMessage(
            message.ChatId,
            AppConstants.BotMessages.ZerodhaInvite,
            replyMarkup: keyboard,
            cancellationToken: ct);
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Processing message. CorrelationId={CorrelationId} From={FirstName} Text={Text}")]
    private static partial void LogMessageReceived(
        ILogger logger, string correlationId, string firstName, string text);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Zerodha token cache hit — session active. CorrelationId={CorrelationId}")]
    private static partial void LogTokenCacheHit(ILogger logger, string correlationId);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "No Zerodha token found — invite sent. CorrelationId={CorrelationId}")]
    private static partial void LogInviteSent(ILogger logger, string correlationId);
}
