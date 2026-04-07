namespace FinanceAdvisor.Services.Orchestration;

using System.Diagnostics;
using System.Net;
using FinanceAdvisor.Core.Constants;
using FinanceAdvisor.Core.Interfaces;
using FinanceAdvisor.Core.Models.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

internal sealed partial class SemanticKernelOrchestrator : IAIOrchestrator
{
    private readonly Kernel _kernel;
    private readonly ILogger<SemanticKernelOrchestrator> _logger;
    private readonly string _providerName;

    public SemanticKernelOrchestrator(
        Kernel kernel,
        ILogger<SemanticKernelOrchestrator> logger,
        IOptions<AiProviderSettings> aiOptions)
    {
        _kernel = kernel;
        _logger = logger;
        _providerName = aiOptions.Value.Provider;
    }

    /// <inheritdoc/>
    public async Task<string> ProcessQueryAsync(string userMessage, CancellationToken ct = default)
    {
        using var llmCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        llmCts.CancelAfter(TimeSpan.FromSeconds(AppConstants.Timeouts.LlmSeconds));

        var sw = Stopwatch.StartNew();

        try
        {
            var chat = _kernel.GetRequiredService<IChatCompletionService>();

            var history = new ChatHistory();
            history.AddSystemMessage(
                "You are a concise financial advisor assistant. " +
                "Keep responses under 3 paragraphs. " +
                "Format responses in Telegram MarkdownV2.");
            history.AddUserMessage(userMessage);

            var result = await chat.GetChatMessageContentAsync(
                history,
                cancellationToken: llmCts.Token);

            sw.Stop();
            LogLlmCompleted(_logger, _providerName, string.Empty, sw.ElapsedMilliseconds);

            return result.Content ?? AppConstants.FallbackMessages.TotalFailure;
        }
        catch (OperationCanceledException)
        {
            LogLlmTimeout(_logger, _providerName);
            return AppConstants.FallbackMessages.LlmTimeout;
        }
        catch (HttpOperationException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
        {
            LogLlmRateLimited(_logger, _providerName);
            return AppConstants.FallbackMessages.RateLimitExceeded;
        }
        catch (Exception ex)
        {
            LogLlmFailed(_logger, _providerName, ex);
            return AppConstants.FallbackMessages.TotalFailure;
        }
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "{Provider} call timed out after LLM timeout window.")]
    private static partial void LogLlmTimeout(ILogger logger, string provider);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "{Provider} rate limit exceeded (HTTP 429). Returning fallback to user.")]
    private static partial void LogLlmRateLimited(ILogger logger, string provider);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "{Provider} call failed with unexpected exception.")]
    private static partial void LogLlmFailed(ILogger logger, string provider, Exception ex);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "{Provider} response received. CorrelationId={CorrelationId} LatencyMs={LatencyMs}")]
    private static partial void LogLlmCompleted(
        ILogger logger, string provider, string correlationId, long latencyMs);
}
