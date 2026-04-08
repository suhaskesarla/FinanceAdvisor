namespace FinanceAdvisor.Services.Orchestration;

using System.Diagnostics;
using System.Net;
using System.Text;
using FinanceAdvisor.Core.Constants;
using FinanceAdvisor.Core.Exceptions;
using FinanceAdvisor.Core.Interfaces;
using FinanceAdvisor.Core.Models.Configuration;
using FinanceAdvisor.Services.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;

internal sealed partial class SemanticKernelOrchestrator : IAIOrchestrator
{
    private readonly Kernel _gathererKernel;
    private readonly Kernel _analystKernel;
    private readonly ILogger<SemanticKernelOrchestrator> _logger;
    private readonly string _gathererServiceId;
    private readonly string _analystServiceId;

    public SemanticKernelOrchestrator(
        Kernel kernel,
        ILogger<SemanticKernelOrchestrator> logger,
        IOptions<AiProviderSettings> aiOptions,
        PortfolioPlugin portfolioPlugin,
        MarketPlugin marketPlugin,
        NewsPlugin newsPlugin)
    {
        AiProviderSettings settings = aiOptions.Value;

        ValidateServiceId(kernel, settings.GathererServiceId);
        ValidateServiceId(kernel, settings.AnalystServiceId);

        _gathererKernel = kernel.Clone();
        _gathererKernel.Plugins.AddFromObject(portfolioPlugin);
        _gathererKernel.Plugins.AddFromObject(marketPlugin);
        _gathererKernel.Plugins.AddFromObject(newsPlugin);

        _analystKernel = new Kernel(kernel.Services);

        _logger = logger;
        _gathererServiceId = settings.GathererServiceId;
        _analystServiceId = settings.AnalystServiceId;
    }

    private static void ValidateServiceId(Kernel kernel, string serviceId)
    {
        if (string.IsNullOrWhiteSpace(serviceId))
        {
            throw new InvalidProviderConfigurationException(serviceId ?? string.Empty);
        }

        _ = kernel.Services.GetKeyedService<IChatCompletionService>(serviceId)
            ?? throw new InvalidProviderConfigurationException(serviceId);
    }

    /// <inheritdoc/>
    public async Task<string> ProcessQueryAsync(string userMessage, CancellationToken ct = default)
    {
        using var llmCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        llmCts.CancelAfter(TimeSpan.FromSeconds(AppConstants.Timeouts.LlmSeconds));

        var sw = Stopwatch.StartNew();

        try
        {
            var gathererAgent = new ChatCompletionAgent
            {
                Name = "DataGatherer",
                Instructions =
                    "You are a data retrieval agent. Determine which tools are relevant and call them. " +
                    "Return ONLY a valid JSON object. " +
                    "If no tools are needed for a simple greeting, return an empty JSON object {}. " +
                    "Do not speak to the user.",
                Kernel = _gathererKernel,
                Arguments = new KernelArguments(new PromptExecutionSettings
                {
                    ServiceId = _gathererServiceId,
                    // Auto allows the LLM to request multiple tools in a single response;
                    // SK handles all invocations before returning to the caller.
                    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
                })
            };

            var gathererHistory = new ChatHistory();
            gathererHistory.AddUserMessage(userMessage);

            var gatheredPayload = new StringBuilder();
            await foreach (var message in gathererAgent.InvokeAsync(gathererHistory, cancellationToken: llmCts.Token))
            {
                if (message.Message.Content is not null)
                {
                    gatheredPayload.Append(message.Message.Content);
                }
            }

            sw.Stop();
            LogGathererCompleted(_logger, _gathererServiceId, sw.ElapsedMilliseconds);
            sw.Restart();

            var analystAgent = new ChatCompletionAgent
            {
                Name = "FinancialAnalyst",
                Instructions =
                    "You are a concise, data-driven financial advisor. " +
                    "RULE 1: Base response ONLY on provided JSON. " +
                    "RULE 2: Never hallucinate figures. " +
                    "RULE 3: Keep under 3 paragraphs. " +
                    "RULE 4: Format in Telegram MarkdownV2. " +
                    "RULE 5: You MUST escape reserved characters (e.g. '.', '-', '!') with a backslash to comply with MarkdownV2. " +
                    "RULE 6: If the data contains 'USER_ACTION_REQUIRED', stop the analysis and immediately ask the user to perform that specific action (e.g., /login).",
                Kernel = _analystKernel,
                Arguments = new KernelArguments(new PromptExecutionSettings
                {
                    ServiceId = _analystServiceId
                })
            };

            var analystHistory = new ChatHistory();
            analystHistory.AddUserMessage(
                $"User asked: {userMessage}\n\nData gathered:\n{gatheredPayload}");

            var analystResponse = new StringBuilder();
            await foreach (var message in analystAgent.InvokeAsync(analystHistory, cancellationToken: llmCts.Token))
            {
                if (message.Message.Content is not null)
                {
                    analystResponse.Append(message.Message.Content);
                }
            }

            sw.Stop();
            LogLlmCompleted(_logger, _analystServiceId, string.Empty, sw.ElapsedMilliseconds);

            string result = analystResponse.ToString();
            return result.Length > 0 ? result : AppConstants.FallbackMessages.TotalFailure;
        }
        catch (OperationCanceledException)
        {
            LogLlmTimeout(_logger, _gathererServiceId, _analystServiceId);
            return AppConstants.FallbackMessages.LlmTimeout;
        }
        catch (HttpOperationException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
        {
            LogLlmRateLimited(_logger, _gathererServiceId, _analystServiceId);
            return AppConstants.FallbackMessages.RateLimitExceeded;
        }
        catch (Exception ex)
        {
            LogLlmFailed(_logger, _gathererServiceId, _analystServiceId, ex);
            return AppConstants.FallbackMessages.TotalFailure;
        }
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "{Provider} gatherer agent completed. LatencyMs={LatencyMs}")]
    private static partial void LogGathererCompleted(ILogger logger, string provider, long latencyMs);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "LLM call timed out. GathererServiceId={GathererServiceId} AnalystServiceId={AnalystServiceId}")]
    private static partial void LogLlmTimeout(ILogger logger, string gathererServiceId, string analystServiceId);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Rate limit exceeded (HTTP 429). GathererServiceId={GathererServiceId} AnalystServiceId={AnalystServiceId}")]
    private static partial void LogLlmRateLimited(ILogger logger, string gathererServiceId, string analystServiceId);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "LLM call failed. GathererServiceId={GathererServiceId} AnalystServiceId={AnalystServiceId}")]
    private static partial void LogLlmFailed(ILogger logger, string gathererServiceId, string analystServiceId, Exception ex);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "{Provider} analyst response received. CorrelationId={CorrelationId} LatencyMs={LatencyMs}")]
    private static partial void LogLlmCompleted(
        ILogger logger, string provider, string correlationId, long latencyMs);
}
