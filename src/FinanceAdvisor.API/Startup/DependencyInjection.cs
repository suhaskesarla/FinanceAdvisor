namespace FinanceAdvisor.API.Startup;

using FinanceAdvisor.Core.Constants;
using FinanceAdvisor.Core.Interfaces;
using FinanceAdvisor.Core.Models.Configuration;
using FinanceAdvisor.Services;
using FinanceAdvisor.Services.DataEngines;
using FinanceAdvisor.Services.Orchestration;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Google;
using KiteConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Polly;
using Serilog;
using System.Globalization;
using System.Net;
using System.Text.Json;
using Telegram.Bot;

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
        services.Configure<OpenAiSettings>(configuration.GetSection("OpenAI"));
        services.Configure<AiProviderSettings>(configuration.GetSection("AI"));

        services.AddSingleton<ITelegramBotClient>(sp =>
        {
            TelegramSettings telegram = sp.GetRequiredService<IOptions<TelegramSettings>>().Value;
            return new TelegramBotClient(telegram.BotToken);
        });

        services.AddSingleton<IQueryRouter, QueryRouter>();
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

        services.AddScoped<IAIOrchestrator, GeminiOrchestrator>();

        IKernelBuilder kernelBuilder = services.AddKernel();
        string aiProvider = configuration["AI:Provider"] ?? AppConstants.AiProvider.Gemini;

        if (aiProvider.Equals(AppConstants.AiProvider.OpenAI, StringComparison.OrdinalIgnoreCase))
        {
            string openAiKey = configuration["OpenAI:ApiKey"]
                ?? throw new InvalidOperationException("OpenAI:ApiKey is not configured.");
            string openAiModel = configuration["OpenAI:ModelId"] ?? AppConstants.AiProvider.DefaultOpenAiModelId;
            kernelBuilder.AddOpenAIChatCompletion(modelId: openAiModel, apiKey: openAiKey);
        }
        else
        {
            string geminiKey = configuration["Gemini:ApiKey"]
                ?? throw new InvalidOperationException("Gemini:ApiKey is not configured.");
            string geminiModel = configuration["Gemini:ModelId"] ?? AppConstants.AiProvider.DefaultGeminiModelId;
            kernelBuilder.AddGoogleAIGeminiChatCompletion(modelId: geminiModel, apiKey: geminiKey);
        }

        services.AddHttpClient("YahooFinance", client =>
        {
            client.BaseAddress = new Uri("https://query1.finance.yahoo.com/");
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        })
        .AddStandardResilienceHandler(options =>
        {
            options.Retry.MaxRetryAttempts = 2;
            options.Retry.BackoffType = DelayBackoffType.Exponential;
            options.Retry.UseJitter = true;
            options.Retry.Delay = TimeSpan.FromMilliseconds(AppConstants.Timeouts.RetryBaseDelayMs);

            // Retry on network errors, 429 Too Many Requests, and 5xx server errors.
            options.Retry.ShouldHandle = args => ValueTask.FromResult(
                args.Outcome.Exception is HttpRequestException ||
                (args.Outcome.Result is HttpResponseMessage r &&
                 (r.StatusCode == HttpStatusCode.TooManyRequests ||
                  (int)r.StatusCode >= 500)));

            // Honour the Retry-After header when Yahoo signals a rate-limit window.
            // Returning null falls back to the configured exponential delay.
            options.Retry.DelayGenerator = args =>
            {
                if (args.Outcome.Result is HttpResponseMessage { Headers.RetryAfter: { } ra })
                {
                    TimeSpan? serverDelay = ra.Delta
                        ?? (ra.Date.HasValue ? ra.Date.Value - DateTimeOffset.UtcNow : null);
                    if (serverDelay > TimeSpan.Zero)
                    {
                        return new ValueTask<TimeSpan?>(serverDelay);
                    }
                }

                return new ValueTask<TimeSpan?>(result: null);
            };

            options.AttemptTimeout.Timeout =
                TimeSpan.FromSeconds(AppConstants.Timeouts.ExternalApiAttemptSeconds);
            options.TotalRequestTimeout.Timeout =
                TimeSpan.FromSeconds(AppConstants.Timeouts.ExternalApiSeconds);
        });

        services.AddHttpClient("NewsRss", client =>
        {
            client.Timeout =
                TimeSpan.FromSeconds(AppConstants.Timeouts.ExternalApiSeconds);
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 FinanceAdvisor/1.0");
        })
        .AddStandardResilienceHandler(options =>
        {
            options.Retry.MaxRetryAttempts = 1;
            options.Retry.Delay = TimeSpan.FromMilliseconds(200);
            options.AttemptTimeout.Timeout =
                TimeSpan.FromSeconds(AppConstants.Timeouts.ExternalApiAttemptSeconds);
            options.TotalRequestTimeout.Timeout =
                TimeSpan.FromSeconds(AppConstants.Timeouts.ExternalApiSeconds);
        });

        return services;
    }
}
