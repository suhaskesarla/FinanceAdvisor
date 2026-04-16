namespace FinanceAdvisor.Services.Plugins;

using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FinanceAdvisor.Core.Constants;
using FinanceAdvisor.Core.DTOs;
using FinanceAdvisor.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

internal sealed partial class NewsPlugin
{
    private const string _rerankerPrompt = """
        You are a financial news relevance scoring system.

        User question:
        {{$userQuery}}

        From the list of news articles below, assign a relevance score between 0 and 1 for each article based on how well it helps explain the user's question or overall market movement.

        Scoring guidelines:

        * 0.9 – 1.0 → Directly explains Nifty/Sensex movement or core macro drivers
        * 0.7 – 0.89 → Strongly relevant macro or market sentiment context
        * 0.4 – 0.69 → Weakly related, partial context
        * 0.1 – 0.39 → Slightly related or indirect reference
        * 0.0 – 0.09 → Not relevant

        Prioritize relevance to:

        * Nifty / Sensex movement
        * Macro economic factors (inflation, interest rates, RBI, global markets)
        * FII/DII flows
        * Broad market sentiment

        Deprioritize:

        * Individual stock news unless directly relevant
        * Sector-specific news unless it impacts broader indices

        Articles:
        {{$articles}}

        Return ONLY a JSON array with this structure:

        [
        { "id": 3, "score": 0.92 },
        { "id": 7, "score": 0.81 },
        { "id": 1, "score": 0.78 }
        ]

        Rules:

        * Include ALL input articles in the output
        * Scores must be between 0 and 1
        * Do NOT include explanations or extra text
        * Do NOT omit any articles
        """;

    // Implicit article categories for diversity control and macro guarantee
    private const string _categoryMacro = "macro";
    private const string _categoryFlows = "flows";
    private const string _categorySector = "sector";
    private const string _categoryStock = "stock";

    private static readonly JsonSerializerOptions _caseInsensitiveOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    // Title keyword sets for lightweight, model-free article classification
    private static readonly string[] _macroKeywords =
    [
        "nifty", "sensex", "rbi", "interest rate", "inflation", "gdp", "global market",
        "federal reserve", "fed rate", "cpi", "wpi", "monetary policy", "repo rate",
        "fiscal deficit", "forex", "rupee", "crude oil", "us market", "global cues",
    ];

    private static readonly string[] _flowsKeywords =
    [
        "fii", "dii", "foreign institutional", "domestic institutional",
        "foreign portfolio", "outflow", "inflow", "net buy", "net sell",
    ];

    private static readonly string[] _sectorKeywords =
    [
        "banking sector", "pharma sector", "it sector", "auto sector",
        "fmcg", "metal sector", "energy sector", "telecom sector",
        "real estate", "realty", "psu bank", "healthcare sector",
    ];

    private readonly INewsEngine _newsEngine;
    private readonly ILogger<NewsPlugin> _logger;

    public NewsPlugin(INewsEngine newsEngine, ILogger<NewsPlugin> logger)
    {
        _newsEngine = newsEngine;
        _logger = logger;
    }

    [KernelFunction("get_top_headlines")]
    [Description("Fetches and reranks top financial news headlines relevant to the user's query.")]
    public async Task<string> GetTopHeadlinesAsync(
        Kernel kernel,
        [Description("The user's question used to score news article relevance.")] string userQuery,
        CancellationToken ct = default)
    {
        try
        {
            var articles = await _newsEngine.GetTopHeadlinesAsync(ct);

            if (articles.Length == 0)
            {
                return AppConstants.FallbackMessages.NewsUnavailable;
            }

            var selected = await RerankAsync(kernel, userQuery, articles, ct);
            return JsonSerializer.Serialize(selected);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogPluginError(_logger, userQuery, ex.Message);
            return AppConstants.FallbackMessages.NewsUnavailable;
        }
    }

    private async Task<NewsArticleDto[]> RerankAsync(
        Kernel kernel,
        string userQuery,
        NewsArticleDto[] articles,
        CancellationToken ct)
    {
        var compact = articles.Select((a, i) => new
        {
            id = i + 1,
            title = a.Title,
            summary = a.Summary.Length > AppConstants.NewsReranking.SummaryMaxLength
                ? a.Summary[..AppConstants.NewsReranking.SummaryMaxLength]
                : a.Summary,
        });
        string articlesJson = JsonSerializer.Serialize(compact);

        try
        {
            FunctionResult result = await kernel.InvokePromptAsync(
                _rerankerPrompt,
                new KernelArguments
                {
                    ["userQuery"] = userQuery,
                    ["articles"] = articlesJson,
                },
                cancellationToken: ct);

            string llmOutput = result.ToString();
            List<NewsScore>? scores = ParseScores(llmOutput);

            if (scores is null || scores.Count == 0)
            {
                LogRerankerEmptyScores(_logger, userQuery);
                return FallbackByRecency(articles);
            }

            // Collapse check on raw LLM scores — freshness boost is not applied here
            double maxScore = scores.Max(s => s.Score);
            double spread = maxScore - scores.Min(s => s.Score);

            if (maxScore < AppConstants.NewsReranking.LowConfidenceMaxScore
                && spread < AppConstants.NewsReranking.LowConfidenceSpread)
            {
                LogRerankerLowConfidence(_logger, maxScore, spread, userQuery);
                return BlendedFallback(scores, articles);
            }

            // Apply freshness boost (C#-side only — LLM scores are unchanged)
            List<NewsScore> boostedScores = scores
                .Select(s =>
                {
                    var article = articles.ElementAtOrDefault(s.Id - 1);
                    double boost = article is null ? 0.0 : ComputeFreshnessBoost(article.PublishedAt);
                    return boost > 0.0 ? s with { Score = s.Score + boost } : s;
                })
                .ToList();

            // Diversity-aware selection: no more than MaxPerCategory per implicit category
            List<NewsArticleDto> selected = ApplyDiversity(
                boostedScores.OrderByDescending(s => s.Score), articles);

            // Macro guarantee: ensure at least one macro/flows article if any exists in the pool
            NewsArticleDto[] final = ApplyMacroGuarantee(selected, boostedScores, articles);

            LogRerankerComplete(_logger, final.Length, maxScore, userQuery);

            return final;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogRerankerLlmFailed(_logger, userQuery, ex.Message);
            return FallbackByRecency(articles);
        }
    }

    private static List<NewsScore>? ParseScores(string llmOutput)
    {
        int start = llmOutput.IndexOf('[');
        int end = llmOutput.LastIndexOf(']');
        if (start < 0 || end <= start)
        {
            return null;
        }

        string json = llmOutput[start..(end + 1)];
        return JsonSerializer.Deserialize<List<NewsScore>>(json, _caseInsensitiveOptions);
    }

    // Collapse fallback: blend top LLM articles with most recent to avoid full signal discard
    private static NewsArticleDto[] BlendedFallback(List<NewsScore> scores, NewsArticleDto[] articles)
    {
        var llmTop = scores
            .OrderByDescending(s => s.Score)
            .Take(AppConstants.NewsReranking.BlendedFallbackLlmCount)
            .Select(s => articles.ElementAtOrDefault(s.Id - 1))
            .Where(a => a is not null)
            .Cast<NewsArticleDto>()
            .ToList();

        var llmLinks = llmTop.Select(a => a.Link).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var recent = articles
            .OrderByDescending(a => a.PublishedAt)
            .Where(a => !llmLinks.Contains(a.Link))
            .Take(AppConstants.NewsReranking.BlendedFallbackRecencyCount);

        return [.. llmTop.Concat(recent).Take(AppConstants.NewsReranking.MaxResults)];
    }

    // Walks ranked scores and enforces at most MaxPerCategory per implicit category.
    // Overflow candidates fill any remaining slots so MaxResults is always reached if possible.
    private static List<NewsArticleDto> ApplyDiversity(
        IEnumerable<NewsScore> rankedScores,
        NewsArticleDto[] articles)
    {
        var categoryCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var selected = new List<NewsArticleDto>(AppConstants.NewsReranking.MaxResults);
        var overflow = new List<NewsArticleDto>();

        foreach (var score in rankedScores)
        {
            var article = articles.ElementAtOrDefault(score.Id - 1);
            if (article is null)
            {
                continue;
            }

            string category = ClassifyArticle(article.Title);
            categoryCounts.TryGetValue(category, out int count);

            if (count < AppConstants.NewsReranking.MaxPerCategory)
            {
                categoryCounts[category] = count + 1;
                selected.Add(article);
                if (selected.Count >= AppConstants.NewsReranking.MaxResults)
                {
                    break;
                }
            }
            else
            {
                overflow.Add(article);
            }
        }

        // Fill any remaining slots when diversity constraints leave gaps
        foreach (var article in overflow)
        {
            if (selected.Count >= AppConstants.NewsReranking.MaxResults)
            {
                break;
            }

            selected.Add(article);
        }

        return selected;
    }

    // If no macro/flows article is in the selection, injects the highest-scored one from the pool.
    private static NewsArticleDto[] ApplyMacroGuarantee(
        List<NewsArticleDto> selected,
        List<NewsScore> boostedScores,
        NewsArticleDto[] articles)
    {
        if (selected.Count == 0 || selected.Any(a => IsMacroSignal(a.Title)))
        {
            return [.. selected];
        }

        var selectedLinks = selected.Select(a => a.Link).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var bestMacro = boostedScores
            .OrderByDescending(s => s.Score)
            .Select(s => articles.ElementAtOrDefault(s.Id - 1))
            .FirstOrDefault(a => a is not null
                && !selectedLinks.Contains(a.Link)
                && IsMacroSignal(a.Title));

        if (bestMacro is null)
        {
            return [.. selected];
        }

        // Append if there is a free slot; otherwise replace the last (lowest-ranked) article
        return selected.Count < AppConstants.NewsReranking.MaxResults
            ? [.. selected.Append(bestMacro)]
            : [.. selected.Take(selected.Count - 1).Append(bestMacro)];
    }

    private static double ComputeFreshnessBoost(DateTime publishedAt)
    {
        var age = DateTime.UtcNow - publishedAt.ToUniversalTime();
        if (age.TotalHours <= AppConstants.NewsReranking.FreshnessRecentHoursThreshold)
        {
            return AppConstants.NewsReranking.FreshnessBoostRecent;
        }

        if (publishedAt.ToUniversalTime().Date == DateTime.UtcNow.Date)
        {
            return AppConstants.NewsReranking.FreshnessBoostSameDay;
        }
        return 0.0;
    }

    // Flows checked before macro so FII/DII articles get their own diversity bucket
    // while still qualifying as macro signals for the guarantee step.
    private static string ClassifyArticle(string title)
    {
        string lower = title.ToLowerInvariant();
        if (_flowsKeywords.Any(k => lower.Contains(k)))
        {
            return _categoryFlows;
        }

        if (_macroKeywords.Any(k => lower.Contains(k)))
        {
            return _categoryMacro;
        }

        if (_sectorKeywords.Any(k => lower.Contains(k)))
        {
            return _categorySector;
        }
        return _categoryStock;
    }

    private static bool IsMacroSignal(string title)
    {
        string category = ClassifyArticle(title);
        return category == _categoryMacro || category == _categoryFlows;
    }

    private static NewsArticleDto[] FallbackByRecency(NewsArticleDto[] articles) =>
        [.. articles.OrderByDescending(a => a.PublishedAt).Take(AppConstants.NewsReranking.FallbackCount)];

    [LoggerMessage(Level = LogLevel.Error, Message = "News plugin failed. UserQuery={UserQuery} Error={Error}")]
    private static partial void LogPluginError(ILogger logger, string userQuery, string error);

    [LoggerMessage(Level = LogLevel.Warning, Message = "News reranker returned empty scores. UserQuery={UserQuery} Falling back to recency.")]
    private static partial void LogRerankerEmptyScores(ILogger logger, string userQuery);

    [LoggerMessage(Level = LogLevel.Warning, Message = "News reranker: low-confidence collapse detected. MaxScore={MaxScore} Spread={Spread} UserQuery={UserQuery} Using blended fallback.")]
    private static partial void LogRerankerLowConfidence(ILogger logger, double maxScore, double spread, string userQuery);

    [LoggerMessage(Level = LogLevel.Information, Message = "News reranker returning {Count} articles. MaxScore={MaxScore} UserQuery={UserQuery}")]
    private static partial void LogRerankerComplete(ILogger logger, int count, double maxScore, string userQuery);

    [LoggerMessage(Level = LogLevel.Warning, Message = "News reranker LLM call failed. UserQuery={UserQuery} Error={Error} Falling back to recency.")]
    private static partial void LogRerankerLlmFailed(ILogger logger, string userQuery, string error);

    private sealed record NewsScore(int Id, double Score);
}
