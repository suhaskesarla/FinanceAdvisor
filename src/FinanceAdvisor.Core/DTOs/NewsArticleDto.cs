namespace FinanceAdvisor.Core.DTOs;

/// <summary>A single financial news article parsed from an RSS feed.</summary>
public sealed record NewsArticleDto
{
    /// <summary>Headline of the article with HTML tags stripped.</summary>
    public required string Title { get; init; }

    /// <summary>URL linking to the full article.</summary>
    public required string Link { get; init; }

    /// <summary>Short description of the article with all HTML tags stripped.</summary>
    public required string Summary { get; init; }

    /// <summary>UTC date and time the article was published.</summary>
    public required DateTime PublishedAt { get; init; }
}
