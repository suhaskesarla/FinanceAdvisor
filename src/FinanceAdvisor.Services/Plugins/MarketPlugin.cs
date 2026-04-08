namespace FinanceAdvisor.Services.Plugins;

using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FinanceAdvisor.Core.Interfaces;
using Microsoft.SemanticKernel;

internal sealed class MarketPlugin
{
    private readonly IMarketDataProvider _marketDataProvider;

    public MarketPlugin(IMarketDataProvider marketDataProvider)
    {
        _marketDataProvider = marketDataProvider;
    }

    [KernelFunction("get_market_snapshot")]
    public async Task<string> GetMarketSnapshotAsync(CancellationToken ct = default)
    {
        try
        {
            var snapshot = await _marketDataProvider.GetMarketSnapshotAsync("^NSEI", ct);
            return JsonSerializer.Serialize(snapshot);
        }
        catch (Exception ex)
        {
            return $"Error: MarketData is unavailable (Reason: {ex.Message}).";
        }
    }
}
