namespace FinanceAdvisor.Core.Models.Configuration;

/// <summary>Telegram bot credentials and endpoint configuration.</summary>
public sealed record TelegramSettings
{
    /// <summary>The Telegram bot API token.</summary>
    public string BotToken { get; init; } = string.Empty;

    /// <summary>The public HTTPS URL Telegram will POST updates to.</summary>
    public string WebhookUrl { get; init; } = string.Empty;

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
}
