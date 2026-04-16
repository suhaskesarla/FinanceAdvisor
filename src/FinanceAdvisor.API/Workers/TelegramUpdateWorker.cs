namespace FinanceAdvisor.API.Workers;

using FinanceAdvisor.Core.DTOs;
using FinanceAdvisor.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

internal sealed partial class TelegramUpdateWorker : BackgroundService
{
    private readonly IUpdateChannel _channel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TelegramUpdateWorker> _logger;

    public TelegramUpdateWorker(
        IUpdateChannel channel,
        IServiceScopeFactory scopeFactory,
        ILogger<TelegramUpdateWorker> logger)
    {
        _channel = channel;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (IncomingMessageDto message in _channel.ReadAllAsync(stoppingToken))
        {
            LogDequeued(_logger, message.CorrelationId);
            await ProcessMessageAsync(message, stoppingToken);
        }
    }

    private async Task ProcessMessageAsync(IncomingMessageDto message, CancellationToken ct)
    {
        Stopwatch sw = Stopwatch.StartNew();
        LogProcessingStarted(_logger, message.CorrelationId, message.ChatId);

        try
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            ITelegramWebhookService service =
                scope.ServiceProvider.GetRequiredService<ITelegramWebhookService>();

            await service.HandleUpdateAsync(message, ct);
            sw.Stop();
            LogProcessingCompleted(_logger, message.CorrelationId, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            LogProcessingFailed(_logger, ex, message.CorrelationId, sw.ElapsedMilliseconds);
        }
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Update dequeued. CorrelationId={CorrelationId}")]
    private static partial void LogDequeued(ILogger logger, string correlationId);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Processing started. CorrelationId={CorrelationId} ChatId={ChatId}")]
    private static partial void LogProcessingStarted(ILogger logger, string correlationId, long chatId);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Processing completed. CorrelationId={CorrelationId} LatencyMs={LatencyMs}")]
    private static partial void LogProcessingCompleted(ILogger logger, string correlationId, long latencyMs);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Processing failed. CorrelationId={CorrelationId} LatencyMs={LatencyMs}")]
    private static partial void LogProcessingFailed(
        ILogger logger, Exception ex, string correlationId, long latencyMs);
}
