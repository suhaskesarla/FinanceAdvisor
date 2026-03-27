namespace FinanceAdvisor.API.Middleware;

// TODO FIN-50: implement full correlation ID generation and propagation
internal sealed class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        await _next(context);
    }
}
