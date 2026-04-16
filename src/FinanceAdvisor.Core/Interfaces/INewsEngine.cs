namespace FinanceAdvisor.Core.Interfaces;

using FinanceAdvisor.Core.DTOs;

/// <summary>Retrieves top financial news headlines from an external feed.</summary>
public interface INewsEngine
{
    /// <summary>Fetches recent financial news headlines from the configured feed.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Array of recent articles up to the configured fetch limit.</returns>
    Task<NewsArticleDto[]> GetTopHeadlinesAsync(CancellationToken ct = default);
}
