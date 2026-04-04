namespace FinanceAdvisor.Core.Interfaces;

using FinanceAdvisor.Core.DTOs;

/// <summary>Retrieves real-time market index snapshots from an external data source.</summary>
public interface IMarketDataProvider
{
    /// <summary>Fetches the current snapshot for the given ticker symbol.</summary>
    /// <param name="ticker">Yahoo Finance ticker e.g. "^NSEI" for Nifty 50.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>MarketSnapshotDto or null if data unavailable.</returns>
    Task<MarketSnapshotDto?> GetMarketSnapshotAsync(string ticker, CancellationToken ct = default);
}
