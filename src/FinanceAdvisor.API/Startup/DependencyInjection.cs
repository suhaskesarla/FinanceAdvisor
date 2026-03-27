namespace FinanceAdvisor.API.Startup;

using FinanceAdvisor.Core.Models.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

internal static class DependencyInjection
{
    internal static IServiceCollection AddFinanceAdvisorServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddControllers();
        services.AddMemoryCache();
        services.AddHealthChecks();

        services.Configure<TelegramSettings>(configuration.GetSection("Telegram"));
        services.Configure<ZerodhaSettings>(configuration.GetSection("Zerodha"));
        services.Configure<GeminiSettings>(configuration.GetSection("Gemini"));

        return services;
    }
}
