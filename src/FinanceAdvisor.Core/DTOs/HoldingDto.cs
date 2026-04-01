namespace FinanceAdvisor.Core.DTOs;

/// <summary>Represents a single equity holding from the Zerodha portfolio.</summary>
public sealed record HoldingDto
{
    /// <summary>The NSE/BSE trading symbol for the instrument (e.g. "INFY").</summary>
    public string Ticker { get; init; } = string.Empty;

    /// <summary>The exchange on which the instrument is listed (e.g. "NSE").</summary>
    public string Exchange { get; init; } = string.Empty;

    /// <summary>The number of shares held.</summary>
    public int Quantity { get; init; }

    /// <summary>The average purchase price per share.</summary>
    public decimal AveragePrice { get; init; }

    /// <summary>The current market price per share.</summary>
    public decimal LastPrice { get; init; }

    /// <summary>Absolute profit/loss: (LastPrice - AveragePrice) * Quantity.</summary>
    public decimal PnL { get; init; }

    /// <summary>
    /// Percentage profit/loss: ((LastPrice - AveragePrice) / AveragePrice) * 100.
    /// Returns 0 when AveragePrice is 0 to avoid a divide-by-zero error.
    /// </summary>
    public decimal PnLPercentage { get; init; }

    /// <summary>Current market value of the holding: LastPrice * Quantity.</summary>
    public decimal CurrentValue { get; init; }
}
