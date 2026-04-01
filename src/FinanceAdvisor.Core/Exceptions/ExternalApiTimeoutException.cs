namespace FinanceAdvisor.Core.Exceptions;

/// <summary>Thrown when a call to an external API fails or exceeds the configured timeout.</summary>
public sealed class ExternalApiTimeoutException : Exception
{
    /// <summary>
    /// Initializes a new instance of <see cref="ExternalApiTimeoutException"/> for the named API.
    /// </summary>
    /// <param name="apiName">The name of the external API that failed (e.g. "Zerodha").</param>
    public ExternalApiTimeoutException(string apiName)
        : base($"{apiName} API call failed or timed out.")
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="ExternalApiTimeoutException"/> wrapping an inner exception.
    /// </summary>
    /// <param name="apiName">The name of the external API that failed (e.g. "Zerodha").</param>
    /// <param name="inner">The underlying exception that caused the failure.</param>
    public ExternalApiTimeoutException(string apiName, Exception inner)
        : base($"{apiName} API call failed or timed out.", inner)
    {
    }
}
