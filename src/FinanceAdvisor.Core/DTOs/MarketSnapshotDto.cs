namespace FinanceAdvisor.Core.DTOs;

/// <summary>Snapshot of a market index at a point in time.</summary>
public sealed record MarketSnapshotDto
{
    /// <summary>Yahoo Finance ticker symbol (e.g. "^NSEI" for Nifty 50).</summary>
    public required string Ticker { get; init; }

    /// <summary>Human-readable name of the index (e.g. "Nifty 50").</summary>
    public required string Name { get; init; }

    /// <summary>Latest traded price of the index.</summary>
    public required decimal CurrentPrice { get; init; }

    /// <summary>Percentage change from the previous close. Calculated as ((current - previous) / previous) * 100.</summary>
    public required decimal ChangePercent { get; init; }

    /// <summary>Absolute point change from the previous close. Calculated as current - previous.</summary>
    public required decimal ChangeAbsolute { get; init; }

    /// <summary>Direction indicator: "▲" if positive, "▼" if negative, "─" if zero.</summary>
    public required string Direction { get; init; }

    /// <summary>UTC timestamp when the snapshot data was retrieved.</summary>
    public required DateTime AsOf { get; init; }
}
