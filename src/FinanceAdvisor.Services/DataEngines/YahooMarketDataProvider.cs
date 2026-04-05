namespace FinanceAdvisor.Services.DataEngines;

using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using FinanceAdvisor.Core.Constants;
using FinanceAdvisor.Core.DTOs;
using FinanceAdvisor.Core.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

/// <summary>Fetches and caches market index snapshots via the Yahoo Finance v8 chart API.</summary>
internal sealed partial class YahooMarketDataProvider : IMarketDataProvider
{
    private static readonly CompositeFormat _cacheKeyFormat =
        CompositeFormat.Parse(AppConstants.CacheKeys.MarketData);

    private readonly IMemoryCache _cache;
    private readonly ILogger<YahooMarketDataProvider> _logger;
    private readonly HttpClient _httpClient;

    /// <summary>Initializes a new instance of <see cref="YahooMarketDataProvider"/>.</summary>
    /// <param name="cache">Memory cache for market snapshot data.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="httpClientFactory">Factory used to create the named Yahoo Finance HTTP client.</param>
    public YahooMarketDataProvider(
        IMemoryCache cache,
        ILogger<YahooMarketDataProvider> logger,
        IHttpClientFactory httpClientFactory)
    {
        _cache = cache;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient("YahooFinance");
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

            var stopwatch = Stopwatch.StartNew();
            string url = $"v8/finance/chart/{Uri.EscapeDataString(ticker)}?interval=1d&range=1d&includePrePost=false";
            using var response = await _httpClient.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    double? retryAfterSeconds = response.Headers.RetryAfter?.Delta?.TotalSeconds;
                    LogRateLimited(_logger, ticker, retryAfterSeconds);
                }
                else
                {
                    LogHttpError(_logger, ticker, (int)response.StatusCode);
                }

                response.EnsureSuccessStatusCode();
            }

            using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, default, ct);

            var meta = ParseMeta(doc, ticker);
            if (meta is null)
            {
                return null;
            }

            if (!meta.Value.TryGetProperty("regularMarketPrice", out var priceEl) ||
                priceEl.ValueKind != JsonValueKind.Number)
            {
                LogMalformedResponse(_logger, ticker, "missing or non-numeric regularMarketPrice");
                return null;
            }

            decimal current = priceEl.GetDecimal();

            decimal previous = 0;
            if (meta.Value.TryGetProperty("chartPreviousClose", out var chartPrevEl) &&
                chartPrevEl.ValueKind == JsonValueKind.Number)
            {
                previous = chartPrevEl.GetDecimal();
            }
            else if (meta.Value.TryGetProperty("previousClose", out var prevEl) &&
                     prevEl.ValueKind == JsonValueKind.Number)
            {
                previous = prevEl.GetDecimal();
            }

            string name = ticker;
            if (meta.Value.TryGetProperty("longName", out var longNameEl) &&
                longNameEl.GetString() is { Length: > 0 } longName)
            {
                name = longName;
            }
            else if (meta.Value.TryGetProperty("shortName", out var shortNameEl) &&
                     shortNameEl.GetString() is { Length: > 0 } shortName)
            {
                name = shortName;
            }

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

            stopwatch.Stop();
            _cache.Set(cacheKey, dto, TimeSpan.FromSeconds(AppConstants.CacheTtl.MarketSeconds));
            LogRequestCompleted(_logger, ticker, stopwatch.Elapsed.TotalMilliseconds);

            return dto;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogFetchFailed(_logger, ticker, ex);
            return null;
        }
    }

    /// <summary>
    /// Navigates to chart.result[0].meta with explicit presence and bounds checks.
    /// Returns null and logs a warning if the shape is unexpected.
    /// </summary>
    private JsonElement? ParseMeta(JsonDocument doc, string ticker)
    {
        if (!doc.RootElement.TryGetProperty("chart", out var chart))
        {
            LogMalformedResponse(_logger, ticker, "missing chart element");
            return null;
        }

        if (!chart.TryGetProperty("result", out var resultArray) ||
            resultArray.ValueKind != JsonValueKind.Array ||
            resultArray.GetArrayLength() == 0)
        {
            LogMalformedResponse(_logger, ticker, "missing or empty chart.result array");
            return null;
        }

        if (!resultArray[0].TryGetProperty("meta", out var meta))
        {
            LogMalformedResponse(_logger, ticker, "missing meta element in chart.result[0]");
            return null;
        }

        return meta;
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Market cache hit for {Ticker}.")]
    private static partial void LogCacheHit(ILogger logger, string ticker);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Market cache miss for {Ticker} — fetching from Yahoo Finance.")]
    private static partial void LogCacheMiss(ILogger logger, string ticker);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Market data fetched for {Ticker}. LatencyMs={LatencyMs:F0}.")]
    private static partial void LogRequestCompleted(ILogger logger, string ticker, double latencyMs);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Yahoo Finance rate-limited for {Ticker}. RetryAfterSeconds={RetryAfterSeconds}.")]
    private static partial void LogRateLimited(ILogger logger, string ticker, double? retryAfterSeconds);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Yahoo Finance returned HTTP {StatusCode} for {Ticker}.")]
    private static partial void LogHttpError(ILogger logger, string ticker, int statusCode);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Yahoo Finance response malformed for {Ticker}. Reason={Reason}.")]
    private static partial void LogMalformedResponse(ILogger logger, string ticker, string reason);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Market data fetch failed for {Ticker}.")]
    private static partial void LogFetchFailed(ILogger logger, string ticker, Exception ex);
}
