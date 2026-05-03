namespace FinanceAdvisor.Core.Interfaces;

using FinanceAdvisor.Core.DTOs;

/// <summary>In-process queue that decouples webhook reception from message processing.</summary>
public interface IUpdateChannel
{
    /// <summary>Enqueues a message for background processing.</summary>
    /// <param name="message">The incoming message to enqueue.</param>
    /// <param name="ct">Cancellation token.</param>
    ValueTask EnqueueAsync(IncomingMessageDto message, CancellationToken ct = default);

    /// <summary>Returns an async enumerable that yields messages as they become available.</summary>
    /// <param name="ct">Cancellation token that stops the enumeration when the host shuts down.</param>
    IAsyncEnumerable<IncomingMessageDto> ReadAllAsync(CancellationToken ct = default);
}
