namespace FinanceAdvisor.Core.Exceptions;

/// <summary>
/// Thrown when an AI provider serviceId is referenced in configuration
/// but has not been registered in the Semantic Kernel.
/// </summary>
public sealed class InvalidProviderConfigurationException : Exception
{
    /// <inheritdoc/>
    public InvalidProviderConfigurationException(string serviceId)
        : base(
            $"AI provider '{serviceId}' is not registered in the kernel. " +
            "Ensure the corresponding API key is configured and the provider is registered in DependencyInjection.cs.")
    {
    }

    /// <inheritdoc/>
    public InvalidProviderConfigurationException(string serviceId, Exception inner)
        : base(
            $"AI provider '{serviceId}' is not registered in the kernel. " +
            "Ensure the corresponding API key is configured and the provider is registered in DependencyInjection.cs.",
            inner)
    {
    }
}
