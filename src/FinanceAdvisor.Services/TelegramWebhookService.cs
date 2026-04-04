namespace FinanceAdvisor.Services;

using System.Globalization;
using System.Text;
using FinanceAdvisor.Core.Constants;
using FinanceAdvisor.Core.DTOs;
using FinanceAdvisor.Core.Exceptions;
using FinanceAdvisor.Core.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

/// <summary>Handles incoming Telegram webhook update processing.</summary>
internal sealed partial class TelegramWebhookService : ITelegramWebhookService
{
    private readonly ITelegramBotClient _botClient;
    private readonly IMemoryCache _cache;
    private readonly IPortfolioEngine _portfolioEngine;
    private readonly IMarketDataProvider _marketProvider;
    private readonly INewsEngine _newsEngine;
    private readonly ILogger<TelegramWebhookService> _logger;

    /// <summary>Initializes a new instance of <see cref="TelegramWebhookService"/>.</summary>
    /// <param name="botClient">Telegram Bot API client.</param>
    /// <param name="cache">Memory cache used to check for a live Zerodha access token.</param>
    /// <param name="portfolioEngine">Engine that retrieves and caches Zerodha holdings.</param>
    /// <param name="marketProvider">Provider that fetches real-time market index snapshots.</param>
    /// <param name="newsEngine">Engine that fetches top financial news headlines.</param>
    /// <param name="logger">Logger instance.</param>
    public TelegramWebhookService(
        ITelegramBotClient botClient,
        IMemoryCache cache,
        IPortfolioEngine portfolioEngine,
        IMarketDataProvider marketProvider,
        INewsEngine newsEngine,
        ILogger<TelegramWebhookService> logger)
    {
        _botClient = botClient;
        _cache = cache;
        _portfolioEngine = portfolioEngine;
        _marketProvider = marketProvider;
        _newsEngine = newsEngine;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task HandleUpdateAsync(IncomingMessageDto message, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        LogMessageReceived(_logger, message.CorrelationId, message.FromFirstName, message.Text);

        bool isPortfolio =
            message.Text.Contains("portfolio", StringComparison.OrdinalIgnoreCase) ||
            message.Text.Contains("/login", StringComparison.OrdinalIgnoreCase);

        bool isBriefing =
            message.Text.Contains("brief", StringComparison.OrdinalIgnoreCase) ||
            message.Text.Contains("/start", StringComparison.OrdinalIgnoreCase);

        if (!isPortfolio && !isBriefing)
        {
            return;
        }

        if (!_cache.TryGetValue(AppConstants.CacheKeys.ZerodhaAccessToken, out _))
        {
            LogInviteSent(_logger, message.CorrelationId);

            InlineKeyboardMarkup inviteKeyboard = new(
                [[
                    InlineKeyboardButton.WithUrl(
                        AppConstants.BotMessages.ZerodhaInviteButtonText,
                        AppConstants.AppEndpoints.ZerodhaLoginInvite)
                ]]);

            await _botClient.SendMessage(
                message.ChatId,
                AppConstants.BotMessages.ZerodhaInvite,
                replyMarkup: inviteKeyboard,
                cancellationToken: ct);

            return;
        }

        if (isBriefing)
        {
            LogBriefingRequest(_logger, message.CorrelationId);
            await HandleBriefingAsync(message.ChatId, ct);
            return;
        }

        LogPortfolioRequest(_logger, message.CorrelationId);

        try
        {
            HoldingDto[] holdings = await _portfolioEngine.GetHoldingsAsync(ct);

            StringBuilder sb = new();
            sb.AppendLine(CultureInfo.InvariantCulture, $"📊 Portfolio ({holdings.Length} holdings)");
            foreach (HoldingDto h in holdings)
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"• {h.Ticker}: {h.Quantity} @ ₹{h.LastPrice:F2} | P&L: ₹{h.PnL:F2} ({h.PnLPercentage:F1}%)");
            }

            await _botClient.SendMessage(message.ChatId, sb.ToString(), cancellationToken: ct);
        }
        catch (ZerodhaAuthException)
        {
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
        catch (ExternalApiTimeoutException)
        {
            await _botClient.SendMessage(
                message.ChatId,
                AppConstants.FallbackMessages.ZerodhaUnavailable,
                cancellationToken: ct);
        }
    }

    private async Task HandleBriefingAsync(long chatId, CancellationToken ct)
    {
        try
        {
            MarketSnapshotDto? market = await _marketProvider.GetMarketSnapshotAsync("^NSEI", ct);
            HoldingDto[] holdings = await _portfolioEngine.GetHoldingsAsync(ct);
            NewsArticleDto[] news = await _newsEngine.GetTopHeadlinesAsync(ct);

            StringBuilder sb = new();

            if (market is not null)
            {
                sb.Append(CultureInfo.InvariantCulture,
                    $"🏛️ *Market:* {market.Name}\n{market.Direction} {market.CurrentPrice:N2} ({market.ChangePercent:F2}%)\n\n");
            }
            else
            {
                sb.Append(CultureInfo.InvariantCulture,
                    $"🏛️ *Market:* {AppConstants.FallbackMessages.MarketUnavailable}\n\n");
            }

            decimal totalPnL = holdings.Sum(h => h.PnL);
            sb.Append(CultureInfo.InvariantCulture,
                $"📊 *Portfolio:* {holdings.Length} holdings\nTotal Net P&L: ₹{totalPnL:N2}\n\n");

            sb.AppendLine("📰 *Top Headlines:*");
            if (news.Length == 0)
            {
                sb.AppendLine(AppConstants.FallbackMessages.NewsUnavailable);
            }
            else
            {
                foreach (NewsArticleDto article in news.Take(3))
                {
                    sb.AppendLine(CultureInfo.InvariantCulture, $"• {article.Title}");
                }
            }

            await _botClient.SendMessage(
                chatId,
                sb.ToString(),
                parseMode: ParseMode.Markdown,
                cancellationToken: ct);
        }
        catch (ExternalApiTimeoutException)
        {
            await _botClient.SendMessage(
                chatId,
                AppConstants.FallbackMessages.ZerodhaUnavailable,
                cancellationToken: ct);
        }
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Processing message. CorrelationId={CorrelationId} From={FirstName} Text={Text}")]
    private static partial void LogMessageReceived(
        ILogger logger, string correlationId, string firstName, string text);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "No Zerodha token found — invite sent. CorrelationId={CorrelationId}")]
    private static partial void LogInviteSent(ILogger logger, string correlationId);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Processing briefing request. CorrelationId={CorrelationId}")]
    private static partial void LogBriefingRequest(ILogger logger, string correlationId);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Processing portfolio request. CorrelationId={CorrelationId}")]
    private static partial void LogPortfolioRequest(ILogger logger, string correlationId);
}
