namespace FinanceAdvisor.Services;

using FinanceAdvisor.Core.Enums;
using FinanceAdvisor.Core.Interfaces;

internal sealed class QueryRouter : IQueryRouter
{
    /// <inheritdoc/>
    public QueryRoute Route(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        string normalised = message.Trim().ToLowerInvariant();

        switch (normalised)
        {
            case "/balance":
            case "/holdings":
            case "/portfolio":
                return QueryRoute.Portfolio;

            case "/brief":
            case "/briefing":
            case "/start":
                return QueryRoute.Briefing;

            case "/login":
                return QueryRoute.ZerodhaLogin;

            case "/help":
                return QueryRoute.Help;
        }

        if (normalised.Contains("portfolio", StringComparison.OrdinalIgnoreCase) ||
            normalised.Contains("holdings", StringComparison.OrdinalIgnoreCase) ||
            normalised.Contains("balance", StringComparison.OrdinalIgnoreCase))
        {
            return QueryRoute.Portfolio;
        }

        if (normalised.Contains("brief", StringComparison.OrdinalIgnoreCase) ||
            normalised.Contains("market", StringComparison.OrdinalIgnoreCase) ||
            normalised.Contains("news", StringComparison.OrdinalIgnoreCase))
        {
            return QueryRoute.Briefing;
        }

        if (normalised.Contains("login", StringComparison.OrdinalIgnoreCase) ||
            normalised.Contains("connect", StringComparison.OrdinalIgnoreCase))
        {
            return QueryRoute.ZerodhaLogin;
        }

        if (normalised.Contains("help", StringComparison.OrdinalIgnoreCase))
        {
            return QueryRoute.Help;
        }

        return QueryRoute.DeepPath;
    }
}
