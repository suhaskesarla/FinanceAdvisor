namespace FinanceAdvisor.Services.Plugins;

using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FinanceAdvisor.Core.Interfaces;
using Microsoft.SemanticKernel;

internal sealed class NewsPlugin
{
    private readonly INewsEngine _newsEngine;

    public NewsPlugin(INewsEngine newsEngine)
    {
        _newsEngine = newsEngine;
    }

    [KernelFunction("get_top_headlines")]
    public async Task<string> GetTopHeadlinesAsync(CancellationToken ct = default)
    {
        var headlines = await _newsEngine.GetTopHeadlinesAsync(ct);
        return JsonSerializer.Serialize(headlines.Take(5));
    }
}
