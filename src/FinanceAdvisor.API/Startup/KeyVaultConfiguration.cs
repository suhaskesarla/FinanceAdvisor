namespace FinanceAdvisor.API.Startup;

using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;

internal static class KeyVaultConfiguration
{
    private const string _keyVaultUri = "https://finadvai-kv.vault.azure.net/";

    internal static WebApplicationBuilder AddAzureKeyVault(this WebApplicationBuilder builder)
    {
        if (!builder.Environment.IsDevelopment())
        {
            builder.Configuration.AddAzureKeyVault(
                new Uri(_keyVaultUri),
                new DefaultAzureCredential(),
                new AzureKeyVaultConfigurationOptions());
        }

        return builder;
    }
}
