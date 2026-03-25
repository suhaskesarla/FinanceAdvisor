# FinanceAdvisor — Claude Code instructions

This file is read by Claude Code at the start of every session.
Follow all rules below without being reminded.

---

## Architecture rules

- Agent 1 (DataGatheringAgent.cs) owns ALL [KernelFunction] plugins.
- Agent 2 (FinancialAnalystAgent.cs) has ZERO plugins. Never add one.
- All interfaces live in FinanceAdvisor.Core/Interfaces/ only.
- All DTOs live in FinanceAdvisor.Core/DTOs/ only.
- All typed exceptions live in FinanceAdvisor.Core/Exceptions/ only.
- Never reference a concrete class across project boundaries — only interfaces.
- All DI registrations belong in API/Startup/DependencyInjection.cs.
- Program.cs must stay under 20 lines — it calls extension methods only.
- AgentPayloadDto is the contract between Agent 1 and Agent 2. Never bypass it.

---

## Coding standards

- Every async method must have `CancellationToken ct = default` as last parameter.
- Never use .Result or .Wait() on a Task — always await.
- Never throw `new Exception()` — use typed exceptions from Core/Exceptions/.
- All classes are sealed unless explicitly designed for inheritance.
- No magic numbers anywhere — all constants in Core/Constants/AppConstants.cs.
- Every public interface method must have XML doc comments (///).
- Private fields are always _camelCase.
- Interfaces always begin with I.
- Async methods always end with Async.
- Use file-scoped namespaces in every .cs file.
- Central Package Management: NEVER add version numbers to <PackageReference>
  tags in .csproj files. When adding a new NuGet package, always:
  1. Add the <PackageVersion Include="..." Version="..." /> entry to
     Directory.Packages.props first
  2. Then add the version-less <PackageReference Include="..." /> to the
     relevant .csproj file
  Adding a version directly to a .csproj breaks CPM and causes a build error.

## File layout order (every .cs file)

1. File-scoped namespace declaration
2. Using statements (outside namespace, alphabetically sorted)
3. Class declaration (sealed by default)
4. Private readonly fields
5. Constructor
6. Public methods (interface implementations first)
7. Private helper methods

---

## Constants reference

All timeouts, TTLs, and LLM limits come from AppConstants — never hardcode these values:

- Cache TTL portfolio:  AppConstants.CacheTtl.PortfolioSeconds (30)
- Cache TTL market:     AppConstants.CacheTtl.MarketSeconds (60)
- Cache TTL news:       AppConstants.CacheTtl.NewsMinutes (10)
- Webhook timeout:      AppConstants.Timeouts.WebhookSeconds (10)
- LLM timeout:          AppConstants.Timeouts.LlmSeconds (5)
- External API timeout: AppConstants.Timeouts.ExternalApiSeconds (3)
- Max input tokens:     AppConstants.GeminiLimits.MaxInputTokens (8000)
- Max output tokens:    AppConstants.GeminiLimits.MaxOutputTokens (500)
- Max requests/minute:  AppConstants.GeminiLimits.MaxRequestsPerMinute (10)

Fallback messages come from AppConstants.FallbackMessages — never hardcode strings.

The full AppConstants structure to implement in Core/Constants/AppConstants.cs:

  public static class AppConstants
  {
      public static class CacheTtl
      {
          public const int PortfolioSeconds = 30;
          public const int MarketSeconds    = 60;
          public const int NewsMinutes      = 10;
      }

      public static class Timeouts
      {
          public const int WebhookSeconds     = 10;
          public const int LlmSeconds         = 5;
          public const int ExternalApiSeconds = 3;
      }

      public static class GeminiLimits
      {
          public const int MaxInputTokens       = 8000;
          public const int MaxOutputTokens      = 500;
          public const int MaxRequestsPerMinute = 10;
      }

      public static class FallbackMessages
      {
          public const string LlmTimeout =
              "Analysis is taking too long — please try again in a moment.";
          public const string ZerodhaUnavailable =
              "Portfolio data is temporarily unavailable. Market and news context is still active.";
          public const string TotalFailure =
              "Something went wrong on our end. Please try again shortly.";
      }
  }

---

## Observability rules

### Correlation ID (Sprint 1 — implement immediately)
Every incoming Telegram webhook request must be assigned a CorrelationId.
- Create Middleware/CorrelationIdMiddleware.cs in the API project
- Generate a new Guid per request if no X-Correlation-ID header is present
- Store it in HttpContext.Items["CorrelationId"]
- Pass it into every log statement throughout the request lifecycle
- The CorrelationId allows a single user query to be traced end-to-end
  across Controller -> QueryRouter -> Agent 1 -> engines -> Agent 2 -> formatter

### Structured logging (all sprints)
Use .NET 8 built-in ILogger with structured properties — never string interpolation:

  // Correct — structured, queryable in Application Insights
  _logger.LogInformation(
      "Agent query completed. CorrelationId={CorrelationId} Query={Query} " +
      "ToolsTriggered={Tools} LatencyMs={Latency}",
      correlationId, query, tools, elapsed.TotalMilliseconds);

  // Wrong — interpolated string loses structure
  _logger.LogInformation($"Query completed in {elapsed.TotalMilliseconds}ms");

### LLM token usage logging (Sprint 3 — every Agent 2 call)
After every Gemini API call, log token usage so costs are visible before the invoice:

  _logger.LogInformation(
      "LLM call completed. CorrelationId={CorrelationId} " +
      "InputTokens={Input} OutputTokens={Output} LatencyMs={Latency}",
      correlationId,
      response.Metadata["InputTokenCount"],
      response.Metadata["OutputTokenCount"],
      elapsed.TotalMilliseconds);

### Log levels
- LogInformation — normal flow: cache hits, fast-path routes, LLM completions
- LogWarning     — degraded but recoverable: cache miss + API slow, partial payload
- LogError       — failed, fallback returned to user
- LogCritical    — startup failures only (missing secrets, DI errors)

Never use LogDebug in production paths — it creates noise in Application Insights.

---

## Resilience rules (Sprint 2 — wire when building engines)

Every HttpClient for an external API must use AddStandardResilienceHandler.
Configure resilience in DependencyInjection.cs only — never inside an engine class.

  services.AddHttpClient<IPortfolioEngine, ZerodhaPortfolioEngine>()
      .AddStandardResilienceHandler(options =>
      {
          options.Retry.MaxRetryAttempts = 2;
          options.Retry.Delay = TimeSpan.FromMilliseconds(200);
          options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
          options.TotalRequestTimeout.Timeout =
              TimeSpan.FromSeconds(AppConstants.Timeouts.ExternalApiSeconds);
      });

  // Apply the same pattern for IMarketDataProvider and INewsEngine

Rules:
- 2 retries maximum — a third attempt wastes your remaining timeout budget
- Circuit breaker prevents hammering a dead API during market open
- Log a LogWarning on every retry so you know which APIs are flaky:
    _logger.LogWarning("Retry attempt {Attempt} for {Engine}", attempt, nameof(ZerodhaPortfolioEngine));

---

## Git workflow

### Branch naming
Always branch from main. Never commit directly to main.
Format: <type>/<TICKET-ID>-short-description
Types: feature, fix, chore, refactor
Example: feature/FIN-12-zerodha-auth-token-management

### Commit message format (Conventional Commits — strictly enforced)
<type>(<scope>): <description>

Types:  feat, fix, chore, refactor, test, docs, perf
Scopes: api, services, core, worker, infra, ci

Rules:
- Description is lowercase, present tense, no full stop, max 72 chars
- Describe what the commit does, not what file was changed
- Add Jira ticket ID in footer when known (e.g. FIN-12)

Good examples:
  feat(services): add yahoo finance market engine with 60s cache ttl
  fix(services): handle missing zerodha token with descriptive exception
  chore(infra): register all engine interfaces in di container
  test(services): add unit tests for portfolio cache hit and miss paths

Bad examples (do not use):
  Added cache layer          <- past tense
  Fix bug                    <- no scope, vague
  feat(API): Add Webhook.    <- wrong case, full stop

---

## Testing rules

- Framework: xUnit + NSubstitute + FluentAssertions
- Test file location mirrors source file location exactly
- Test method naming: GivenX_WhenY_ThenZ — always three parts, no exceptions
- Use real MemoryCache (new MemoryCache(new MemoryCacheOptions())) in tests
- Use NullLogger<T>.Instance for logger dependencies in tests
- Never test Program.cs, DI registrations, or third-party SDK internals
- Every engine must have tests for: cache hit, cache miss, and exception path
- QueryRouter must have tests for every fast-path command and a deep-path fallback

---

## Before marking any task complete

Run these in order — all must pass before committing:

1. dotnet restore --use-lock-file   <- generates packages.lock.json (required for CI caching)
2. dotnet format --verify-no-changes --severity error
3. dotnet build --configuration Release
4. dotnet test --no-build --configuration Release

If any step fails, fix it before committing. Do not suppress warnings
without a code comment explaining why.

---

## What never to do

- Never hardcode API keys, tokens, or secrets in any file
- Never add a plugin to Agent 2 (FinancialAnalystAgent)
- Never use .Result or .Wait()
- Never commit directly to main
- Never reference a concrete implementation across project boundaries
- Never put DI registrations in Program.cs directly
- Never put magic numbers in production code
- Never suppress a warning without a comment
- Never use string interpolation in log statements — always structured properties
- Never configure retry/resilience inside an engine class — DI only
- Never call the Gemini API without logging the token count response
