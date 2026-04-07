namespace FinanceAdvisor.Services;

using System.Diagnostics.Metrics;
using FinanceAdvisor.Core.Constants;
using FinanceAdvisor.Core.Enums;
using FinanceAdvisor.Core.Interfaces;
using Microsoft.Extensions.Logging;

internal sealed partial class QueryRouter : IQueryRouter
{
    // Layer 1: exact slash-command → route. Checked first; zero ambiguity, zero cost.
    private static readonly Dictionary<string, QueryRoute> _exactCommands =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["/balance"] = QueryRoute.Portfolio,
            ["/holdings"] = QueryRoute.Portfolio,
            ["/portfolio"] = QueryRoute.Portfolio,
            ["/brief"] = QueryRoute.Briefing,
            ["/briefing"] = QueryRoute.Briefing,
            ["/start"] = QueryRoute.Briefing,
            ["/login"] = QueryRoute.ZerodhaLogin,
            ["/help"] = QueryRoute.Help,
        };

    // Layer 3: keyword sets for each shallow route. Only consulted for short inputs
    // (≤ ShallowMaxWordCount words) to avoid false positives on longer sentences.
    // Also reused by the phrase-matching layer (Layer 3a).
    private static readonly Dictionary<QueryRoute, string[]> _shallowKeywords = new()
    {
        [QueryRoute.Portfolio] = ["portfolio", "holdings", "balance"],
        [QueryRoute.Briefing] = ["market", "news", "brief", "briefing"],
        [QueryRoute.ZerodhaLogin] = ["login", "connect"],
    };

    // Interrogative and imperative verbs that signal the user wants AI reasoning.
    // Imperative starters ("explain", "analyze", etc.) are equivalent to a '?'.
    private static readonly string[] _questionStarters =
    [
        "what", "how", "why", "should", "can",
        "is", "are", "do", "does",
        "explain", "analyze", "compare", "describe",
    ];

    // Action verbs that pair with a keyword to form unambiguous shallow phrases,
    // even when the overall input is longer than ShallowMaxWordCount words.
    // Example: "show me my portfolio performance" → Portfolio.
    private static readonly string[] _actionVerbs =
        ["show", "get", "give", "tell", "display", "check", "view"];

    // Greetings and acknowledgements that carry no data intent. Detected before the
    // generic fallback so telemetry can distinguish "user said hi" from "unknown input".
    private static readonly string[] _smallTalk =
        ["hi", "hello", "hey", "thanks", "thank you", "ok", "okay", "bye"];

    private readonly ILogger<QueryRouter> _logger;
    private readonly Counter<long> _routeCounter;

    public QueryRouter(ILogger<QueryRouter> logger, IMeterFactory meterFactory)
    {
        _logger = logger;
        _routeCounter = meterFactory
            .Create(AppConstants.Metrics.QueryRouterMeterName)
            .CreateCounter<long>(
                AppConstants.Metrics.RouteDecisionsCounter,
                description: "Number of routing decisions. Tags: route, reason, confidence.");
    }

    /// <inheritdoc/>
    public QueryRoute Route(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        string trimmed = message.Trim();
        string normalised = trimmed.ToLowerInvariant();

        // Layer 1: exact command — highest priority, no allocation beyond the dict lookup.
        if (_exactCommands.TryGetValue(normalised, out QueryRoute exactRoute))
        {
            return Record(exactRoute, "exact-command", normalised, AppConstants.Metrics.Confidence.High);
        }

        // Layer 2: question detection — always forward to AI regardless of keyword presence.
        // A question mark or analytical/interrogative starter implies the user wants reasoning.
        if (IsQuestion(normalised))
        {
            return Record(QueryRoute.DeepPath, "question-detected", normalised, AppConstants.Metrics.Confidence.High);
        }

        // Layer 3a: phrase matching — action verb + keyword covers medium-length inputs that
        // exceed the strict word-count threshold but still map clearly to a shallow handler.
        // Example: "show me my portfolio performance" (5 words) → Portfolio.
        if (IsPhraseMatch(normalised, out QueryRoute phraseRoute))
        {
            return Record(phraseRoute, "phrase-match", normalised, AppConstants.Metrics.Confidence.Medium);
        }

        // Layer 3b: short + strong keyword signal → cheap shallow handler.
        // Restricting to short inputs avoids routing long, nuanced messages to the wrong handler.
        string[] words = normalised.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (IsStrongKeywordMatch(words, out QueryRoute shallowRoute))
        {
            return Record(shallowRoute, "keyword-match", normalised, AppConstants.Metrics.Confidence.High);
        }

        // Layer 4a: inputs longer than the length threshold carry complexity the rules cannot
        // decode — justify the AI cost even without an explicit question marker.
        if (trimmed.Length > AppConstants.QueryRouting.AiWorthyMinLength)
        {
            return Record(QueryRoute.DeepPath, "ai-worthy-length", normalised, AppConstants.Metrics.Confidence.Medium);
        }

        // Layer 4b: multi-word inputs (≥ AiWorthyMinWordCount) that passed all shallow layers
        // are likely complex natural-language requests — forward to AI.
        if (words.Length >= AppConstants.QueryRouting.AiWorthyMinWordCount)
        {
            return Record(QueryRoute.DeepPath, "ai-worthy-word-count", normalised, AppConstants.Metrics.Confidence.Medium);
        }

        // Layer 5a: explicit small-talk — return Help immediately rather than treating
        // a greeting as an unknown input. Kept separate so telemetry can quantify social noise.
        if (_smallTalk.Contains(normalised))
        {
            return Record(QueryRoute.Help, "small-talk", normalised, AppConstants.Metrics.Confidence.High);
        }

        // Layer 5b: generic fallback — no signal at all. Return Help rather than guessing
        // intent (e.g. sending market news to a user who typed a single unknown word).
        return Record(QueryRoute.Help, "fallback", normalised, AppConstants.Metrics.Confidence.Low);
    }

    // Returns true if the input is phrased as a question or an explicit analytical request.
    // Checks both punctuation ('?') and leading interrogative / imperative verbs.
    private static bool IsQuestion(string normalised)
    {
        if (normalised.Contains('?'))
        {
            return true;
        }

        foreach (string starter in _questionStarters)
        {
            // Whole-word prefix match: "what " or exactly "what".
            if (normalised.StartsWith(starter, StringComparison.Ordinal) &&
                (normalised.Length == starter.Length || normalised[starter.Length] == ' '))
            {
                return true;
            }
        }

        return false;
    }

    // Returns true when the input begins with an action verb and contains a shallow keyword
    // as a whole word (not a substring), regardless of total word count.
    // Whole-word check prevents "newsworthy" matching "news", "rebalance" matching "balance", etc.
    private static bool IsPhraseMatch(string normalised, out QueryRoute route)
    {
        bool startsWithAction = false;
        foreach (string verb in _actionVerbs)
        {
            if (normalised.StartsWith(verb, StringComparison.Ordinal) &&
                (normalised.Length == verb.Length || normalised[verb.Length] == ' '))
            {
                startsWithAction = true;
                break;
            }
        }

        if (!startsWithAction)
        {
            route = default;
            return false;
        }

        foreach ((QueryRoute candidate, string[] keywords) in _shallowKeywords)
        {
            foreach (string keyword in keywords)
            {
                if (ContainsWholeWord(normalised, keyword))
                {
                    route = candidate;
                    return true;
                }
            }
        }

        route = default;
        return false;
    }

    // Returns true when the input is short (≤ ShallowMaxWordCount words) and one of its
    // words exactly matches a shallow-route keyword. Receives the pre-split word array so
    // Route() does not allocate a second array.
    private static bool IsStrongKeywordMatch(string[] words, out QueryRoute route)
    {
        if (words.Length > AppConstants.QueryRouting.ShallowMaxWordCount)
        {
            route = default;
            return false;
        }

        foreach ((QueryRoute candidate, string[] keywords) in _shallowKeywords)
        {
            foreach (string keyword in keywords)
            {
                foreach (string word in words)
                {
                    if (word == keyword)
                    {
                        route = candidate;
                        return true;
                    }
                }
            }
        }

        route = default;
        return false;
    }

    // Whole-word substring check: splits on spaces and compares each token exactly.
    // Avoids false positives such as "newsworthy" → "news" or "rebalance" → "balance".
    private static bool ContainsWholeWord(string input, string keyword)
    {
        string[] words = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.Any(w => w == keyword);
    }

    // Emits one structured log line and increments the tagged counter, then returns the route
    // so callers can use it as an expression: `return Record(route, ...)`.
    private QueryRoute Record(QueryRoute route, string reason, string normalised, string confidence)
    {
        LogRouting(_logger, route, reason, confidence, normalised);
        _routeCounter.Add(1,
            new KeyValuePair<string, object?>("route", route.ToString()),
            new KeyValuePair<string, object?>("reason", reason),
            new KeyValuePair<string, object?>("confidence", confidence));
        return route;
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "QueryRouter decision. Route={Route} Reason={Reason} Confidence={Confidence} Input={Input}")]
    private static partial void LogRouting(ILogger logger, QueryRoute route, string reason, string confidence, string input);
}
