namespace FinanceAdvisor.Core.Enums;

/// <summary>Routing destinations for incoming user messages.</summary>
public enum QueryRoute
{
    /// <summary>Route directly to Portfolio Engine. Bypass AI.</summary>
    Portfolio,

    /// <summary>Route to Market + Portfolio + News briefing. Bypass AI.</summary>
    Briefing,

    /// <summary>Send connect-to-Zerodha invite. No data fetch.</summary>
    ZerodhaLogin,

    /// <summary>Return static help text. No data fetch.</summary>
    Help,

    /// <summary>Forward to AI orchestration (Semantic Kernel — Sprint 3).</summary>
    DeepPath,
}
