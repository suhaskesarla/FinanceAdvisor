namespace FinanceAdvisor.Core.Interfaces;

using FinanceAdvisor.Core.DTOs;

/// <summary>Processes incoming Telegram webhook updates.</summary>
public interface ITelegramWebhookService
{
    /// <summary>Handles a parsed incoming Telegram message.</summary>
    /// <param name="message">The incoming message payload.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task HandleUpdateAsync(IncomingMessageDto message, CancellationToken ct = default);
}
