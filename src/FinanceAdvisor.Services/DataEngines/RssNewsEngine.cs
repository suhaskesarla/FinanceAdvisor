namespace FinanceAdvisor.Services.DataEngines;

using System.ServiceModel.Syndication;
using System.Text.RegularExpressions;
using System.Xml;
using FinanceAdvisor.Core.Constants;
using FinanceAdvisor.Core.DTOs;
using FinanceAdvisor.Core.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

/// <summary>Fetches and caches financial news headlines from an RSS feed.</summary>
internal sealed partial class RssNewsEngine : INewsEngine
{
    private const string _feedUrl =
        "https://economictimes.indiatimes.com/markets/rss.cms";

    private readonly IMemoryCache _cache;
    private readonly ILogger<RssNewsEngine> _logger;
    private readonly HttpClient _httpClient;

    /// <summary>Initializes a new instance of <see cref="RssNewsEngine"/>.</summary>
    /// <param name="cache">Memory cache for news headlines.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="httpClientFactory">Factory used to create the named RSS HTTP client.</param>
    public RssNewsEngine(
        IMemoryCache cache,
        ILogger<RssNewsEngine> logger,
        IHttpClientFactory httpClientFactory)
    {
        _cache = cache;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient("NewsRss");
    }

    /// <inheritdoc/>
    public async Task<NewsArticleDto[]> GetTopHeadlinesAsync(CancellationToken ct = default)
    {
        try
        {
            if (_cache.TryGetValue(AppConstants.CacheKeys.NewsHeadlines, out NewsArticleDto[]? cached) && cached is not null)
            {
                LogCacheHit(_logger);
                return cached;
            }

            LogCacheMiss(_logger);

            using var stream = await _httpClient.GetStreamAsync(_feedUrl, ct);
            var xmlSettings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Parse };
            using var xmlReader = XmlReader.Create(stream, xmlSettings);
            var feed = SyndicationFeed.Load(xmlReader);

            NewsArticleDto[] articles = [.. feed.Items.Take(5).Select(item =>
            {
                string title = StripHtml(item.Title?.Text ?? string.Empty);
                string link = item.Links.FirstOrDefault()?.Uri.ToString() ?? string.Empty;
                string summary = StripHtml(item.Summary?.Text ?? string.Empty);

                return new NewsArticleDto
                {
                    Title = title,
                    Link = link,
                    Summary = summary,
                    PublishedAt = item.PublishDate.UtcDateTime,
                };
            })];

            _cache.Set(AppConstants.CacheKeys.NewsHeadlines, articles,
                TimeSpan.FromMinutes(AppConstants.CacheTtl.NewsMinutes));

            return articles;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogFetchFailed(_logger, ex);
            return Array.Empty<NewsArticleDto>();
        }
    }

    private static string StripHtml(string input) =>
        Regex.Replace(input, "<[^>]*>", string.Empty).Trim();

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "News cache hit — returning cached headlines.")]
    private static partial void LogCacheHit(ILogger logger);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "News cache miss — fetching RSS.")]
    private static partial void LogCacheMiss(ILogger logger);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "RSS feed fetch failed.")]
    private static partial void LogFetchFailed(ILogger logger, Exception ex);
}
