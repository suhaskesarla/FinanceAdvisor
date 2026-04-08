namespace FinanceAdvisor.Core.Models.Configuration;

/// <summary>Telegram bot credentials and endpoint configuration.</summary>
public sealed record TelegramSettings
{
    /// <summary>The Telegram bot API token.</summary>
    public string BotToken { get; init; } = string.Empty;

    /// <summary>The public HTTPS URL Telegram will POST updates to.</summary>
    public string WebhookUrl { get; init; } = string.Empty;

    /// <summary>The shared secret Telegram sends in X-Telegram-Bot-Api-Secret-Token.</summary>
    public string WebhookSecret { get; init; } = string.Empty;

    /// <summary>The Telegram chat ID for administrative alerts.</summary>
    public string AdminChatId { get; init; } = string.Empty;
}

/// <summary>Zerodha Kite Connect API credentials.</summary>
public sealed record ZerodhaSettings
{
    /// <summary>The Zerodha API key.</summary>
    public string ApiKey { get; init; } = string.Empty;

    /// <summary>The Zerodha API secret.</summary>
    public string ApiSecret { get; init; } = string.Empty;
}

/// <summary>Google Gemini API credentials.</summary>
public sealed record GeminiSettings
{
    /// <summary>The Gemini API key.</summary>
    public string ApiKey { get; init; } = string.Empty;

    /// <summary>The Gemini model ID. Defaults to gemini-2.0-flash when not set.</summary>
    public string ModelId { get; init; } = string.Empty;
}

/// <summary>OpenAI API credentials.</summary>
public sealed record OpenAiSettings
{
    /// <summary>The OpenAI API key.</summary>
    public string ApiKey { get; init; } = string.Empty;

    /// <summary>The OpenAI model ID. Defaults to gpt-4o-mini when not set.</summary>
    public string ModelId { get; init; } = string.Empty;
}

/// <summary>Anthropic Claude API credentials.</summary>
public sealed record AnthropicSettings
{
    /// <summary>The Anthropic API key.</summary>
    public string ApiKey { get; init; } = string.Empty;

    /// <summary>The Claude model ID. Defaults to claude-3-5-haiku-20241022 when not set.</summary>
    public string ModelId { get; init; } = string.Empty;
}

/// <summary>AI provider selection configuration.</summary>
public sealed record AiProviderSettings
{
    /// <summary>Active provider name — either "Gemini", "OpenAI", or "Claude".</summary>
    public string Provider { get; init; } = string.Empty;

    /// <summary>
    /// ServiceId of the chat completion service the DataGatherer agent should use.
    /// Must match a registered serviceId: "OpenAI", "Gemini", or "Claude".
    /// </summary>
    public string GathererServiceId { get; init; } = string.Empty;

    /// <summary>
    /// ServiceId of the chat completion service the FinancialAnalyst agent should use.
    /// Must match a registered serviceId: "OpenAI", "Gemini", or "Claude".
    /// </summary>
    public string AnalystServiceId { get; init; } = string.Empty;
}
