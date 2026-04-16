namespace FinanceAdvisor.Core.Interfaces;

using FinanceAdvisor.Core.DTOs;

/// <summary>In-process queue that decouples webhook reception from message processing.</summary>
public interface IUpdateChannel
{
    /// <summary>Attempts to enqueue a message for background processing.</summary>
    /// <param name="message">The incoming message to enqueue.</param>
    /// <returns>
    /// <c>true</c> if the message was accepted;
    /// <c>false</c> if the channel was full and the oldest pending item was dropped to make room.
    /// </returns>
    bool TryEnqueue(IncomingMessageDto message);

    /// <summary>Returns an async enumerable that yields messages as they become available.</summary>
    /// <param name="ct">Cancellation token that stops the enumeration when the host shuts down.</param>
    IAsyncEnumerable<IncomingMessageDto> ReadAllAsync(CancellationToken ct = default);
}
