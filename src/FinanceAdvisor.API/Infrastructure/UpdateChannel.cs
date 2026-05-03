namespace FinanceAdvisor.API.Infrastructure;

using FinanceAdvisor.Core.Constants;
using FinanceAdvisor.Core.DTOs;
using FinanceAdvisor.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;

internal sealed partial class UpdateChannel : IUpdateChannel
{
    private readonly Channel<IncomingMessageDto> _channel;
    private readonly ILogger<UpdateChannel> _logger;

    public UpdateChannel(ILogger<UpdateChannel> logger)
    {
        _logger = logger;
        _channel = Channel.CreateBounded<IncomingMessageDto>(new BoundedChannelOptions(AppConstants.Channel.UpdateCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
        });
    }

    public async ValueTask EnqueueAsync(IncomingMessageDto message, CancellationToken ct = default)
    {
        if (_channel.Reader.Count > AppConstants.Channel.UpdateCapacity * 8 / 10)
        {
            LogChannelNearCapacity(_logger);
        }

        await _channel.Writer.WriteAsync(message, CancellationToken.None);
        LogEnqueued(_logger, message.CorrelationId);
    }

    public IAsyncEnumerable<IncomingMessageDto> ReadAllAsync(CancellationToken ct = default) =>
        _channel.Reader.ReadAllAsync(ct);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Channel nearing capacity")]
    private static partial void LogChannelNearCapacity(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Enqueued update {CorrelationId}")]
    private static partial void LogEnqueued(ILogger logger, string correlationId);
}
