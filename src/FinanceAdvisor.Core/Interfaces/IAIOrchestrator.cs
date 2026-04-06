namespace FinanceAdvisor.Core.Interfaces;

/// <summary>Orchestrates AI pipeline calls and returns formatted responses.</summary>
public interface IAIOrchestrator
{
    /// <summary>
    /// Processes a user query through the AI pipeline and returns
    /// a formatted Markdown response suitable for Telegram.
    /// </summary>
    /// <param name="userMessage">The raw message text from the user.</param>
    /// <param name="ct">Cancellation token — carries the global webhook timeout.</param>
    /// <returns>Formatted Markdown string response.</returns>
    Task<string> ProcessQueryAsync(string userMessage, CancellationToken ct = default);
}
