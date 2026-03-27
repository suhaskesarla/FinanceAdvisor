namespace FinanceAdvisor.Core.Constants;

/// <summary>Application-wide constants. Never hardcode these values anywhere else.</summary>
public static class AppConstants
{
    /// <summary>Cache time-to-live values in seconds or minutes.</summary>
    public static class CacheTtl
    {
        /// <summary>TTL for portfolio data cache entries (seconds).</summary>
        public const int PortfolioSeconds = 30;

        /// <summary>TTL for market data cache entries (seconds).</summary>
        public const int MarketSeconds = 60;

        /// <summary>TTL for news cache entries (minutes).</summary>
        public const int NewsMinutes = 10;
    }

    /// <summary>Timeout thresholds in seconds.</summary>
    public static class Timeouts
    {
        /// <summary>Maximum processing time allowed for a single webhook request (seconds).</summary>
        public const int WebhookSeconds = 10;

        /// <summary>Maximum time to wait for an LLM response (seconds).</summary>
        public const int LlmSeconds = 5;

        /// <summary>Maximum time to wait for an external API response (seconds).</summary>
        public const int ExternalApiSeconds = 3;
    }

    /// <summary>Gemini API rate and token limits.</summary>
    public static class GeminiLimits
    {
        /// <summary>Maximum number of input tokens per request.</summary>
        public const int MaxInputTokens = 8000;

        /// <summary>Maximum number of output tokens per response.</summary>
        public const int MaxOutputTokens = 500;

        /// <summary>Maximum Gemini API requests permitted per minute.</summary>
        public const int MaxRequestsPerMinute = 10;
    }

    /// <summary>Fallback messages returned to users when a subsystem fails.</summary>
    public static class FallbackMessages
    {
        /// <summary>Returned when the LLM call exceeds the configured timeout.</summary>
        public const string LlmTimeout =
            "Analysis is taking too long — please try again in a moment.";

        /// <summary>Returned when the Zerodha portfolio API is unavailable.</summary>
        public const string ZerodhaUnavailable =
            "Portfolio data is temporarily unavailable. Market and news context is still active.";

        /// <summary>Returned when all subsystems fail and no partial response is possible.</summary>
        public const string TotalFailure =
            "Something went wrong on our end. Please try again shortly.";
    }
}
