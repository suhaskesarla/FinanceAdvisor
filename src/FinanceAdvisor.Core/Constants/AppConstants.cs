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

        /// <summary>TTL for the Zerodha Kite Connect access token (hours).</summary>
        public const int ZerodhaTokenHours = 24;
    }

    /// <summary>Timeout thresholds in seconds.</summary>
    public static class Timeouts
    {
        /// <summary>Maximum processing time allowed for a single webhook request (seconds). Telegram supports up to 60 s.</summary>
        public const int WebhookSeconds = 25;

        /// <summary>Maximum time to wait for an LLM response (seconds).</summary>
        public const int LlmSeconds = 5;

        /// <summary>Total pipeline timeout (all attempts + delays) for a single external API call (seconds).</summary>
        public const int ExternalApiSeconds = 10;

        /// <summary>Per-attempt timeout for resilience handlers — must be strictly less than ExternalApiSeconds.</summary>
        public const int ExternalApiAttemptSeconds = 7;

        /// <summary>Base delay for exponential back-off on retry (milliseconds). Jitter is applied on top by the resilience handler.</summary>
        public const int RetryBaseDelayMs = 500;
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

    /// <summary>IMemoryCache string keys for well-known cache entries.</summary>
    public static class CacheKeys
    {
        /// <summary>Cache key for the Zerodha Kite Connect access token.</summary>
        public const string ZerodhaAccessToken = "zerodha:access_token";

        /// <summary>Cache key for market index data.</summary>
        public const string MarketData = "market:data:{0}"; // format with ticker symbol

        /// <summary>Cache key for top news headlines.</summary>
        public const string NewsHeadlines = "news:headlines";
    }

    /// <summary>Known external endpoint base URLs.</summary>
    public static class ZerodhaEndpoints
    {
        /// <summary>Base URL for the Kite Connect OAuth login page.</summary>
        public const string LoginBaseUrl = "https://kite.trade/connect/login";
    }

    /// <summary>Public-facing app endpoint URLs.</summary>
    public static class AppEndpoints
    {
        /// <summary>URL the user must visit to initiate the Zerodha OAuth login flow.</summary>
        public const string ZerodhaLoginInvite = "https://finadvai.azurewebsites.net/api/auth/login";
    }

    /// <summary>Bot reply messages sent to Telegram users.</summary>
    public static class BotMessages
    {
        /// <summary>Invite prompt shown when no Zerodha token is cached.</summary>
        public const string ZerodhaInvite =
            "👋 To view your portfolio, please connect your Zerodha account first.";

        /// <summary>Label for the inline keyboard button in the Zerodha invite.</summary>
        public const string ZerodhaInviteButtonText = "🔐 Connect Zerodha";

        /// <summary>Reply shown when a valid Zerodha token is already cached.</summary>
        public const string ZerodhaSessionActive =
            "✅ Zerodha session is active. (Portfolio engine coming soon).";

        /// <summary>Temporary reply for deep-path queries before AI is wired.</summary>
        public const string AiComingSoon =
            "🤖 AI analysis is coming soon. Try /portfolio or /brief for now.";

        /// <summary>Help text listing available commands.</summary>
        public const string HelpText =
            "📖 *Available commands:*\n" +
            "• /portfolio or /balance — view your Zerodha holdings\n" +
            "• /brief — morning briefing (market + portfolio + news)\n" +
            "• /login — connect your Zerodha account\n" +
            "• /help — show this message\n\n" +
            "_Natural language also works: try 'show my portfolio' or 'market update'_";
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

        /// <summary>Returned when market data is unavailable.</summary>
        public const string MarketUnavailable =
            "Market data is temporarily unavailable.";

        /// <summary>Returned when news headlines are unavailable.</summary>
        public const string NewsUnavailable =
            "News headlines are temporarily unavailable.";
    }
}
