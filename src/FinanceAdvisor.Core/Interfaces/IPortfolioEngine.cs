namespace FinanceAdvisor.Core.Interfaces;

using FinanceAdvisor.Core.DTOs;

/// <summary>Retrieves the user's equity holdings from the broker.</summary>
public interface IPortfolioEngine
{
    /// <summary>
    /// Returns the current portfolio holdings, served from cache when available.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An array of <see cref="HoldingDto"/> representing every holding.</returns>
    /// <exception cref="FinanceAdvisor.Core.Exceptions.ZerodhaAuthException">
    /// Thrown when no valid Zerodha session exists.
    /// </exception>
    /// <exception cref="FinanceAdvisor.Core.Exceptions.ExternalApiTimeoutException">
    /// Thrown when the Zerodha API call fails or times out.
    /// </exception>
    Task<HoldingDto[]> GetHoldingsAsync(CancellationToken ct = default);
}
