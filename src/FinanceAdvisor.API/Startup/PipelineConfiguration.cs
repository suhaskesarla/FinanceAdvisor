namespace FinanceAdvisor.API.Startup;

using FinanceAdvisor.API.Middleware;

internal static class PipelineConfiguration
{
    internal static WebApplication UseFinanceAdvisorPipeline(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
        {
            app.UseHttpsRedirection();
        }

        app.UseRequestTimeouts();
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.MapControllers();
        app.MapHealthChecks("/health");

        return app;
    }
}
