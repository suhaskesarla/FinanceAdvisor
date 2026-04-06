namespace FinanceAdvisor.Services.Orchestration;

using System.Diagnostics;
using FinanceAdvisor.Core.Constants;
using FinanceAdvisor.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

internal sealed partial class GeminiOrchestrator : IAIOrchestrator
{
    private readonly Kernel _kernel;
    private readonly ILogger<GeminiOrchestrator> _logger;

    public GeminiOrchestrator(Kernel kernel, ILogger<GeminiOrchestrator> logger)
    {
        _kernel = kernel;
        _logger = logger;
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
            LogLlmCompleted(_logger, string.Empty, sw.ElapsedMilliseconds);

            return result.Content ?? AppConstants.FallbackMessages.TotalFailure;
        }
        catch (OperationCanceledException)
        {
            LogLlmTimeout(_logger);
            return AppConstants.FallbackMessages.LlmTimeout;
        }
        catch (Exception ex)
        {
            LogLlmFailed(_logger, ex);
            return AppConstants.FallbackMessages.TotalFailure;
        }
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Gemini call timed out after LLM timeout window.")]
    private static partial void LogLlmTimeout(ILogger logger);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Gemini call failed with unexpected exception.")]
    private static partial void LogLlmFailed(ILogger logger, Exception ex);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Gemini response received. CorrelationId={CorrelationId} LatencyMs={LatencyMs}")]
    private static partial void LogLlmCompleted(
        ILogger logger, string correlationId, long latencyMs);
}
