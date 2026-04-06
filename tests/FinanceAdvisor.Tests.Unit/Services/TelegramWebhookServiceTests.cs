namespace FinanceAdvisor.Tests.Unit.Services;

using FinanceAdvisor.Core.Constants;
using FinanceAdvisor.Core.DTOs;
using FinanceAdvisor.Core.Enums;
using FinanceAdvisor.Core.Exceptions;
using FinanceAdvisor.Core.Interfaces;
using FinanceAdvisor.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Telegram.Bot;
using Telegram.Bot.Requests;
using Telegram.Bot.Types.ReplyMarkups;
using Xunit;

public sealed class TelegramWebhookServiceTests : IDisposable
{
    private readonly ITelegramBotClient _botClient = Substitute.For<ITelegramBotClient>();
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly IPortfolioEngine _portfolioEngine = Substitute.For<IPortfolioEngine>();
    private readonly IMarketDataProvider _marketProvider = Substitute.For<IMarketDataProvider>();
    private readonly INewsEngine _newsEngine = Substitute.For<INewsEngine>();
    private readonly IQueryRouter _router = Substitute.For<IQueryRouter>();
    private readonly IAIOrchestrator _orchestrator = Substitute.For<IAIOrchestrator>();
    private readonly TelegramWebhookService _sut;

    private static readonly IncomingMessageDto _briefingMessage = new()
    {
        ChatId = 42L,
        Text = "/brief",
        FromFirstName = "Test",
        CorrelationId = "corr-1",
    };

    private static readonly MarketSnapshotDto _sampleMarket = new()
    {
        Ticker = "^NSEI",
        Name = "NIFTY 50",
        CurrentPrice = 22000m,
        ChangePercent = 0.5m,
        ChangeAbsolute = 110m,
        Direction = "▲",
        AsOf = DateTime.UtcNow,
    };

    private static readonly NewsArticleDto _sampleArticle = new()
    {
        Title = "Markets rally",
        Link = "https://example.com",
        Summary = "Summary text",
        PublishedAt = DateTime.UtcNow,
    };

    public TelegramWebhookServiceTests()
    {
        _sut = new TelegramWebhookService(
            _botClient,
            _cache,
            _portfolioEngine,
            _marketProvider,
            _newsEngine,
            _router,
            _orchestrator,
            NullLogger<TelegramWebhookService>.Instance);
    }

    public void Dispose() => _cache.Dispose();

    [Fact]
    public async Task GivenBriefingRoute_WhenZerodhaAuthExceptionThrown_ThenSendsZerodhaInviteAsync()
    {
        _router.Route(_briefingMessage.Text).Returns(QueryRoute.Briefing);
        _marketProvider
            .GetMarketSnapshotAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((MarketSnapshotDto?)null);
        _portfolioEngine
            .GetHoldingsAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new ZerodhaAuthException("session expired"));
        _newsEngine.GetTopHeadlinesAsync(Arg.Any<CancellationToken>()).Returns([]);

        await _sut.HandleUpdateAsync(_briefingMessage);

        await _botClient.Received(1).SendRequest(
            Arg.Is<SendMessageRequest>(r =>
                r.ChatId == _briefingMessage.ChatId &&
                r.Text == AppConstants.BotMessages.ZerodhaInvite &&
                r.ReplyMarkup is InlineKeyboardMarkup),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GivenBriefingRoute_WhenAllEnginesSucceed_ThenSendsBriefingMessageAsync()
    {
        _router.Route(_briefingMessage.Text).Returns(QueryRoute.Briefing);
        _portfolioEngine
            .GetHoldingsAsync(Arg.Any<CancellationToken>())
            .Returns([new HoldingDto { Ticker = "INFY", Quantity = 10, LastPrice = 1500m, PnL = 200m }]);
        _marketProvider
            .GetMarketSnapshotAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_sampleMarket);
        _newsEngine.GetTopHeadlinesAsync(Arg.Any<CancellationToken>()).Returns([_sampleArticle]);

        await _sut.HandleUpdateAsync(_briefingMessage);

        await _botClient.Received(1).SendRequest(
            Arg.Is<SendMessageRequest>(r => r.ChatId == _briefingMessage.ChatId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GivenBriefingRoute_WhenExternalApiTimeoutThrown_ThenSendsFallbackMessageAsync()
    {
        _router.Route(_briefingMessage.Text).Returns(QueryRoute.Briefing);
        _portfolioEngine
            .GetHoldingsAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new ExternalApiTimeoutException("timeout"));
        _marketProvider
            .GetMarketSnapshotAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((MarketSnapshotDto?)null);
        _newsEngine.GetTopHeadlinesAsync(Arg.Any<CancellationToken>()).Returns([]);

        await _sut.HandleUpdateAsync(_briefingMessage);

        await _botClient.Received(1).SendRequest(
            Arg.Is<SendMessageRequest>(r =>
                r.ChatId == _briefingMessage.ChatId &&
                r.Text == AppConstants.FallbackMessages.ZerodhaUnavailable),
            Arg.Any<CancellationToken>());
    }
}
