namespace FinanceAdvisor.API.Startup;

using FinanceAdvisor.Core.Constants;
using FinanceAdvisor.Core.Interfaces;
using FinanceAdvisor.Core.Models.Configuration;
using FinanceAdvisor.Services;
using FinanceAdvisor.Services.DataEngines;
using Microsoft.Extensions.Http.Resilience;
using KiteConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Serilog;
using Telegram.Bot;
using System.Globalization;
using System.Text.Json;

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
        services.AddControllers()
    .AddJsonOptions(options =>
    {
        // 1. This handles Telegram's snake_case (first_name -> FirstName)
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;

        // 2. This allows your future controllers to still accept PascalCase (FirstName -> FirstName)
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });
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

        services.AddSingleton<ITelegramBotClient>(sp =>
        {
            TelegramSettings telegram = sp.GetRequiredService<IOptions<TelegramSettings>>().Value;
            return new TelegramBotClient(telegram.BotToken);
        });

        services.AddScoped<ITelegramWebhookService, TelegramWebhookService>();

        services.AddTransient(sp =>
        {
            ZerodhaSettings zerodha = sp.GetRequiredService<IOptions<ZerodhaSettings>>().Value;
            return new Kite(zerodha.ApiKey);
        });
        services.AddScoped<IZerodhaAuthService, ZerodhaAuthService>();
        services.AddScoped<IPortfolioEngine, ZerodhaPortfolioEngine>();

        services.AddScoped<IMarketDataProvider, YahooMarketDataProvider>();
        services.AddScoped<INewsEngine, RssNewsEngine>();

        services.AddHttpClient("NewsRss", client =>
        {
            client.Timeout =
                TimeSpan.FromSeconds(AppConstants.Timeouts.ExternalApiSeconds);
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 FinanceAdvisor/1.0");
        })
        .AddStandardResilienceHandler(options =>
        {
            options.Retry.MaxRetryAttempts = 2;
            options.Retry.Delay = TimeSpan.FromMilliseconds(200);
            options.AttemptTimeout.Timeout =
                TimeSpan.FromSeconds(AppConstants.Timeouts.ExternalApiAttemptSeconds);
            options.TotalRequestTimeout.Timeout =
                TimeSpan.FromSeconds(AppConstants.Timeouts.ExternalApiSeconds);
        });

        return services;
    }
}
