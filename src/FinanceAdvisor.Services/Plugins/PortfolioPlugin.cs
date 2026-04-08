namespace FinanceAdvisor.Services.Plugins;

using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FinanceAdvisor.Core.Exceptions;
using FinanceAdvisor.Core.Interfaces;
using Microsoft.SemanticKernel;

internal sealed class PortfolioPlugin
{
    private readonly IPortfolioEngine _portfolioEngine;

    public PortfolioPlugin(IPortfolioEngine portfolioEngine)
    {
        _portfolioEngine = portfolioEngine;
    }

    [KernelFunction("get_portfolio_holdings")]
    public async Task<string> GetPortfolioHoldingsAsync(CancellationToken ct = default)
    {
        try
        {
            var holdings = await _portfolioEngine.GetHoldingsAsync(ct);
            return JsonSerializer.Serialize(holdings);
        }
        catch (ZerodhaAuthException)
        {
            return "USER_ACTION_REQUIRED: Zerodha session expired. Tell the user to run /login.";
        }
        catch (Exception ex)
        {
            return $"Error: Portfolio is unavailable (Reason: {ex.Message}).";
        }
    }
}
