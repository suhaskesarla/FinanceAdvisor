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
    private readonly TelegramWebhookService _sut;

    private static readonly IncomingMessageDto BriefingMessage = new()
    {
        ChatId = 42L,
        Text = "/brief",
        FromFirstName = "Test",
        CorrelationId = "corr-1",
    };

    private static readonly MarketSnapshotDto SampleMarket = new()
    {
        Ticker = "^NSEI",
        Name = "NIFTY 50",
        CurrentPrice = 22000m,
        ChangePercent = 0.5m,
        ChangeAbsolute = 110m,
        Direction = "▲",
        AsOf = DateTime.UtcNow,
    };

    private static readonly NewsArticleDto SampleArticle = new()
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
            NullLogger<TelegramWebhookService>.Instance);
    }

    public void Dispose() => _cache.Dispose();

    [Fact]
    public async Task GivenBriefingRoute_WhenZerodhaAuthExceptionThrown_ThenSendsZerodhaInvite()
    {
        _router.Route(BriefingMessage.Text).Returns(QueryRoute.Briefing);
        _marketProvider
            .GetMarketSnapshotAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((MarketSnapshotDto?)null);
        _portfolioEngine
            .GetHoldingsAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new ZerodhaAuthException("session expired"));
        _newsEngine.GetTopHeadlinesAsync(Arg.Any<CancellationToken>()).Returns([]);

        await _sut.HandleUpdateAsync(BriefingMessage);

        await _botClient.Received(1).SendRequest(
            Arg.Is<SendMessageRequest>(r =>
                r.ChatId == BriefingMessage.ChatId &&
                r.Text == AppConstants.BotMessages.ZerodhaInvite &&
                r.ReplyMarkup is InlineKeyboardMarkup),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GivenBriefingRoute_WhenAllEnginesSucceed_ThenSendsBriefingMessage()
    {
        _router.Route(BriefingMessage.Text).Returns(QueryRoute.Briefing);
        _portfolioEngine
            .GetHoldingsAsync(Arg.Any<CancellationToken>())
            .Returns([new HoldingDto { Ticker = "INFY", Quantity = 10, LastPrice = 1500m, PnL = 200m }]);
        _marketProvider
            .GetMarketSnapshotAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(SampleMarket);
        _newsEngine.GetTopHeadlinesAsync(Arg.Any<CancellationToken>()).Returns([SampleArticle]);

        await _sut.HandleUpdateAsync(BriefingMessage);

        await _botClient.Received(1).SendRequest(
            Arg.Is<SendMessageRequest>(r => r.ChatId == BriefingMessage.ChatId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GivenBriefingRoute_WhenExternalApiTimeoutThrown_ThenSendsFallbackMessage()
    {
        _router.Route(BriefingMessage.Text).Returns(QueryRoute.Briefing);
        _portfolioEngine
            .GetHoldingsAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new ExternalApiTimeoutException("timeout"));
        _marketProvider
            .GetMarketSnapshotAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((MarketSnapshotDto?)null);
        _newsEngine.GetTopHeadlinesAsync(Arg.Any<CancellationToken>()).Returns([]);

        await _sut.HandleUpdateAsync(BriefingMessage);

        await _botClient.Received(1).SendRequest(
            Arg.Is<SendMessageRequest>(r =>
                r.ChatId == BriefingMessage.ChatId &&
                r.Text == AppConstants.FallbackMessages.ZerodhaUnavailable),
            Arg.Any<CancellationToken>());
    }
}
