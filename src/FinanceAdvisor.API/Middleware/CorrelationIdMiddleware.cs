namespace FinanceAdvisor.API.Middleware;

using Serilog.Context;
using System.Globalization;
using System.Text.Json;

internal sealed class CorrelationIdMiddleware
{
    private const string _correlationIdKey = "CorrelationId";
    private const string _correlationIdHeader = "X-Correlation-ID";
    private const string _updateIdJsonProperty = "update_id";

    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        string correlationId = await ResolveCorrelationIdAsync(context);
        context.Items[_correlationIdKey] = correlationId;

        using IDisposable logScope = LogContext.PushProperty(_correlationIdKey, correlationId);
        await _next(context);
    }

    private static async Task<string> ResolveCorrelationIdAsync(HttpContext context)
    {
        // Priority 1: honour an X-Correlation-ID header sent by a caller (tests, integrations).
        string headerValue = context.Request.Headers[_correlationIdHeader].ToString();
        if (!string.IsNullOrWhiteSpace(headerValue))
        {
            return headerValue;
        }

        // Priority 2: extract the Telegram update_id from the JSON body (native Telegram traffic).
        if (context.Request.Method == HttpMethods.Post && context.Request.ContentLength is not (0 or null))
        {
            try
            {
                context.Request.EnableBuffering();

                using StreamReader reader = new(context.Request.Body, leaveOpen: true);
                string body = await reader.ReadToEndAsync(context.RequestAborted);
                context.Request.Body.Position = 0;

                if (!string.IsNullOrWhiteSpace(body))
                {
                    using JsonDocument doc = JsonDocument.Parse(body);
                    if (doc.RootElement.TryGetProperty(_updateIdJsonProperty, out JsonElement updateIdElement))
                    {
                        return updateIdElement.GetInt32().ToString(CultureInfo.InvariantCulture);
                    }
                }
            }
            catch (JsonException)
            {
                // Non-JSON body — fall through to Guid.
            }
            catch (OperationCanceledException)
            {
                // Request aborted before body could be read — fall through to Guid.
            }
        }

        // Priority 3: generate a new Guid as a guaranteed-unique fallback.
        return Guid.NewGuid().ToString();
    }
}
