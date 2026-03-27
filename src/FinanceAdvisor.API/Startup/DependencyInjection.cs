namespace FinanceAdvisor.API.Startup;

using FinanceAdvisor.Core.Constants;
using FinanceAdvisor.Core.Interfaces;
using FinanceAdvisor.Core.Models.Configuration;
using FinanceAdvisor.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System.Globalization;

internal static class DependencyInjection
{
    internal static WebApplicationBuilder AddFinanceAdvisorLogging(this WebApplicationBuilder builder)
    {
        builder.Host.UseSerilog((ctx, lc) =>
            lc.ReadFrom.Configuration(ctx.Configuration)
              .Enrich.FromLogContext()
              .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture));

        return builder;
    }

    internal static IServiceCollection AddFinanceAdvisorServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddControllers();
        services.AddMemoryCache();
        services.AddHealthChecks();

        services.AddRequestTimeouts(options =>
        {
            options.DefaultPolicy = new RequestTimeoutPolicy
            {
                Timeout = TimeSpan.FromSeconds(AppConstants.Timeouts.WebhookSeconds),
            };
        });

        services.Configure<TelegramSettings>(configuration.GetSection("Telegram"));
        services.Configure<ZerodhaSettings>(configuration.GetSection("Zerodha"));
        services.Configure<GeminiSettings>(configuration.GetSection("Gemini"));

        services.AddScoped<ITelegramWebhookService, TelegramWebhookService>();

        return services;
    }
}
