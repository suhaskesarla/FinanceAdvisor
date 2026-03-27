namespace FinanceAdvisor.Core.DTOs;

/// <summary>Parsed payload representing a single incoming Telegram message.</summary>
public sealed record IncomingMessageDto
{
    /// <summary>The text content of the message.</summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>The first name of the Telegram user who sent the message.</summary>
    public string FromFirstName { get; init; } = string.Empty;

    /// <summary>The correlation ID used to trace this request end-to-end.</summary>
    public string CorrelationId { get; init; } = string.Empty;
}
