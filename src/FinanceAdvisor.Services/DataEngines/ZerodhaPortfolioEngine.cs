namespace FinanceAdvisor.Services.DataEngines;

using FinanceAdvisor.Core.Constants;
using FinanceAdvisor.Core.DTOs;
using FinanceAdvisor.Core.Exceptions;
using FinanceAdvisor.Core.Interfaces;
using FinanceAdvisor.Core.Models.Configuration;
using KiteConnect;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>Fetches and caches equity holdings from the Zerodha Kite Connect API.</summary>
internal sealed partial class ZerodhaPortfolioEngine : IPortfolioEngine
{
    private const string _holdingsCacheKey = "portfolio_holdings";

    private readonly IZerodhaAuthService _authService;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ZerodhaPortfolioEngine> _logger;
    private readonly ZerodhaSettings _settings;
    private readonly Kite _kite;

    /// <summary>Initializes a new instance of <see cref="ZerodhaPortfolioEngine"/>.</summary>
    /// <param name="authService">Service used to retrieve the active Zerodha access token.</param>
    /// <param name="cache">Memory cache for portfolio data.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="settings">Zerodha API credentials.</param>
    /// <param name="kite">Configured Kite Connect client.</param>
    public ZerodhaPortfolioEngine(
        IZerodhaAuthService authService,
        IMemoryCache cache,
        ILogger<ZerodhaPortfolioEngine> logger,
        IOptions<ZerodhaSettings> settings,
        Kite kite)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _authService = authService;
        _cache = cache;
        _logger = logger;
        _settings = settings.Value;
        _kite = kite;
    }

    /// <inheritdoc/>
    public async Task<HoldingDto[]> GetHoldingsAsync(CancellationToken ct = default)
    {
        if (_cache.TryGetValue(_holdingsCacheKey, out HoldingDto[]? cached) && cached is not null)
        {
            LogCacheHit(_logger);
            return cached;
        }

        LogCacheMiss(_logger);

        try
        {
            string accessToken = await _authService.GetAccessTokenAsync(ct);
            _kite.SetAccessToken(accessToken);

            List<Holding> raw = _kite.GetHoldings();

            HoldingDto[] holdings = [.. raw.Select(h =>
            {
                decimal pnl = (h.LastPrice - h.AveragePrice) * h.Quantity;
                decimal pnlPct = h.AveragePrice == 0
                    ? 0
                    : ((h.LastPrice - h.AveragePrice) / h.AveragePrice) * 100;
                decimal currentValue = h.LastPrice * h.Quantity;

                return new HoldingDto
                {
                    Ticker = h.TradingSymbol,
                    Exchange = h.Exchange,
                    Quantity = (int)h.Quantity,
                    AveragePrice = h.AveragePrice,
                    LastPrice = h.LastPrice,
                    PnL = pnl,
                    PnLPercentage = pnlPct,
                    CurrentValue = currentValue,
                };
            })];

            _cache.Set(_holdingsCacheKey, holdings,
                TimeSpan.FromSeconds(AppConstants.CacheTtl.PortfolioSeconds));

            LogFetched(_logger, holdings.Length);
            return holdings;
        }
        catch (TokenException ex)
        {
            // Zerodha tokens expire at 6 AM IST — evict the stale entry so the next
            // cache check in TelegramWebhookService correctly triggers the login invite.
            _cache.Remove(AppConstants.CacheKeys.ZerodhaAccessToken);
            LogSessionExpired(_logger);
            throw new ZerodhaAuthException("Zerodha session has expired. Please re-authenticate.", ex);
        }
        catch (Exception ex) when (ex is not ZerodhaAuthException and not OperationCanceledException)
        {
            LogFetchFailed(_logger, ex);
            throw new ExternalApiTimeoutException("Zerodha", ex);
        }
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Portfolio cache hit — returning cached holdings.")]
    private static partial void LogCacheHit(ILogger logger);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Portfolio cache miss — fetching from Zerodha.")]
    private static partial void LogCacheMiss(ILogger logger);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Portfolio fetched. Count={Count}")]
    private static partial void LogFetched(ILogger logger, int count);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Zerodha session token has expired — stale cache entry evicted.")]
    private static partial void LogSessionExpired(ILogger logger);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Portfolio fetch from Zerodha failed.")]
    private static partial void LogFetchFailed(ILogger logger, Exception ex);
}
