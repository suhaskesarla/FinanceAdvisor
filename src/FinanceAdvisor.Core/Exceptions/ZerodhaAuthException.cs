namespace FinanceAdvisor.Core.Exceptions;

/// <summary>Thrown when the Zerodha Kite Connect authentication flow fails.</summary>
public sealed class ZerodhaAuthException : Exception
{
    /// <summary>Initializes a new instance of <see cref="ZerodhaAuthException"/> with a message.</summary>
    /// <param name="message">Description of the auth failure.</param>
    public ZerodhaAuthException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of <see cref="ZerodhaAuthException"/> wrapping an inner exception.</summary>
    /// <param name="message">Description of the auth failure.</param>
    /// <param name="innerException">The underlying exception that caused the failure.</param>
    public ZerodhaAuthException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
