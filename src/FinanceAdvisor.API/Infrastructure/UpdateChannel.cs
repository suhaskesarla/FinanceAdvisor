namespace FinanceAdvisor.API.Infrastructure;

using FinanceAdvisor.Core.Constants;
using FinanceAdvisor.Core.DTOs;
using FinanceAdvisor.Core.Interfaces;
using System.Threading.Channels;

internal sealed class UpdateChannel : IUpdateChannel
{
    private readonly Channel<IncomingMessageDto> _channel;

    public UpdateChannel()
    {
        _channel = Channel.CreateBounded<IncomingMessageDto>(new BoundedChannelOptions(AppConstants.Channel.UpdateCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
        });
    }

    public bool TryEnqueue(IncomingMessageDto message) => _channel.Writer.TryWrite(message);

    public IAsyncEnumerable<IncomingMessageDto> ReadAllAsync(CancellationToken ct = default) =>
        _channel.Reader.ReadAllAsync(ct);
}
