namespace FinanceAdvisor.Core.Interfaces;

using FinanceAdvisor.Core.Enums;

/// <summary>Routes incoming user messages to the appropriate processing path.</summary>
public interface IQueryRouter
{
    /// <summary>
    /// Determines the processing route for an incoming user message.
    /// </summary>
    /// <param name="message">The raw text sent by the user.</param>
    /// <returns>A <see cref="QueryRoute"/> indicating how to handle the message.</returns>
    QueryRoute Route(string message);
}
