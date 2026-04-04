namespace FinanceAdvisor.Core.Interfaces;

using FinanceAdvisor.Core.DTOs;

/// <summary>Retrieves top financial news headlines from an external feed.</summary>
public interface INewsEngine
{
    /// <summary>Fetches the top financial news headlines.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Array of top 5 articles.</returns>
    Task<NewsArticleDto[]> GetTopHeadlinesAsync(CancellationToken ct = default);
}
