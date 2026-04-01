namespace FinanceAdvisor.Core.Interfaces;

/// <summary>Handles the Zerodha Kite Connect daily OAuth handshake and token storage.</summary>
public interface IZerodhaAuthService
{
    /// <summary>Constructs the Zerodha Kite Connect OAuth login URL.</summary>
    /// <returns>The fully-qualified URL the user must visit to authenticate.</returns>
    string GetLoginUrl();

    /// <summary>
    /// Exchanges a Zerodha <paramref name="requestToken"/> for an access token and
    /// stores it in the memory cache for <see cref="FinanceAdvisor.Core.Constants.AppConstants.CacheTtl.ZerodhaTokenHours"/> hours.
    /// </summary>
    /// <param name="requestToken">The one-time request token returned by Zerodha's callback.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="FinanceAdvisor.Core.Exceptions.ZerodhaAuthException">
    /// Thrown when the Kite session exchange fails.
    /// </exception>
    Task ExchangeRequestTokenAsync(string requestToken, CancellationToken ct = default);

    /// <summary>
    /// Returns the current valid Zerodha access token from cache.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The cached Zerodha access token string.</returns>
    /// <exception cref="FinanceAdvisor.Core.Exceptions.ZerodhaAuthException">
    /// Thrown when no valid session token exists in cache.
    /// </exception>
    Task<string> GetAccessTokenAsync(CancellationToken ct = default);
}
