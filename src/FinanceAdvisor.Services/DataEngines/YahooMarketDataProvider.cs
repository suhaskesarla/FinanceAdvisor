namespace FinanceAdvisor.Services.DataEngines;

using System.Globalization;
using System.Text;
using FinanceAdvisor.Core.Constants;
using FinanceAdvisor.Core.DTOs;
using FinanceAdvisor.Core.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using YahooFinanceApi;

/// <summary>Fetches and caches market index snapshots via the YahooFinanceApi library.</summary>
internal sealed partial class YahooMarketDataProvider : IMarketDataProvider
{
    private static readonly CompositeFormat _cacheKeyFormat =
        CompositeFormat.Parse(AppConstants.CacheKeys.MarketData);

    private readonly IMemoryCache _cache;
    private readonly ILogger<YahooMarketDataProvider> _logger;

    /// <summary>Initializes a new instance of <see cref="YahooMarketDataProvider"/>.</summary>
    /// <param name="cache">Memory cache for market snapshot data.</param>
    /// <param name="logger">Logger instance.</param>
    public YahooMarketDataProvider(IMemoryCache cache, ILogger<YahooMarketDataProvider> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<MarketSnapshotDto?> GetMarketSnapshotAsync(string ticker, CancellationToken ct = default)
    {
        try
        {
            string cacheKey = string.Format(CultureInfo.InvariantCulture, _cacheKeyFormat, ticker);

            if (_cache.TryGetValue(cacheKey, out MarketSnapshotDto? cached) && cached is not null)
            {
                LogCacheHit(_logger, ticker);
                return cached;
            }

            LogCacheMiss(_logger, ticker);

            var securities = await Yahoo.Symbols(ticker).Fields(
                Field.RegularMarketPrice,
                Field.RegularMarketPreviousClose,
                Field.LongName,
                Field.RegularMarketChangePercent).QueryAsync(ct);

            if (securities is null || !securities.ContainsKey(ticker))
            {
                return null;
            }

            var security = securities[ticker];
            decimal current = (decimal)security[Field.RegularMarketPrice];
            decimal previous = (decimal)security[Field.RegularMarketPreviousClose];
            string name = security[Field.LongName]?.ToString() ?? ticker;

            decimal changeAbsolute = current - previous;
            decimal changePercent = previous == 0
                ? 0
                : ((current - previous) / previous) * 100;

            string direction = changePercent > 0 ? "▲" : changePercent < 0 ? "▼" : "─";

            var dto = new MarketSnapshotDto
            {
                Ticker = ticker,
                Name = name,
                CurrentPrice = current,
                ChangePercent = changePercent,
                ChangeAbsolute = changeAbsolute,
                Direction = direction,
                AsOf = DateTime.UtcNow,
            };

            _cache.Set(cacheKey, dto, TimeSpan.FromSeconds(AppConstants.CacheTtl.MarketSeconds));

            return dto;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogFetchFailed(_logger, ticker, ex);
            return null;
        }
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Market cache hit for {Ticker}.")]
    private static partial void LogCacheHit(ILogger logger, string ticker);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Market cache miss for {Ticker}.")]
    private static partial void LogCacheMiss(ILogger logger, string ticker);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Market data fetch failed for {Ticker}.")]
    private static partial void LogFetchFailed(ILogger logger, string ticker, Exception ex);
}
