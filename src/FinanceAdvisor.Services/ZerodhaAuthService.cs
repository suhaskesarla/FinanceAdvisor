namespace FinanceAdvisor.Services;

using FinanceAdvisor.Core.Constants;
using FinanceAdvisor.Core.Exceptions;
using FinanceAdvisor.Core.Interfaces;
using FinanceAdvisor.Core.Models.Configuration;
using KiteConnect;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>Handles the Zerodha Kite Connect daily OAuth handshake and token storage.</summary>
internal sealed partial class ZerodhaAuthService : IZerodhaAuthService
{
    private readonly Kite _kite;
    private readonly ZerodhaSettings _settings;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ZerodhaAuthService> _logger;

    /// <summary>Initializes a new instance of <see cref="ZerodhaAuthService"/>.</summary>
    /// <param name="kite">Configured Kite Connect client.</param>
    /// <param name="settings">Zerodha API credentials.</param>
    /// <param name="cache">Memory cache for storing the access token.</param>
    /// <param name="logger">Logger instance.</param>
    public ZerodhaAuthService(
        Kite kite,
        IOptions<ZerodhaSettings> settings,
        IMemoryCache cache,
        ILogger<ZerodhaAuthService> logger)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _kite = kite;
        _settings = settings.Value;
        _cache = cache;
        _logger = logger;
    }

    /// <inheritdoc/>
    public string GetLoginUrl() =>
        $"{AppConstants.ZerodhaEndpoints.LoginBaseUrl}?api_key={_settings.ApiKey}&v=3";

    /// <inheritdoc/>
    public Task ExchangeRequestTokenAsync(string requestToken, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestToken);

        ct.ThrowIfCancellationRequested();

        try
        {
            User session = _kite.GenerateSession(requestToken, _settings.ApiSecret);

            MemoryCacheEntryOptions cacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromHours(AppConstants.CacheTtl.ZerodhaTokenHours));

            _cache.Set(AppConstants.CacheKeys.ZerodhaAccessToken, session.AccessToken, cacheOptions);

            LogTokenStored(_logger);
        }
        catch (Exception ex) when (ex is not ZerodhaAuthException and not OperationCanceledException)
        {
            LogTokenExchangeFailed(_logger, ex);
            throw new ZerodhaAuthException("Failed to exchange Zerodha request token for access token.", ex);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<string> GetAccessTokenAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (_cache.TryGetValue(AppConstants.CacheKeys.ZerodhaAccessToken, out string? token) && token is not null)
        {
            LogTokenCacheHit(_logger);
            return Task.FromResult(token);
        }

        LogTokenCacheMiss(_logger);
        throw new ZerodhaAuthException("No active Zerodha session. Send /login to authenticate.");
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Zerodha access token stored in cache successfully.")]
    private static partial void LogTokenStored(ILogger logger);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Zerodha token exchange failed.")]
    private static partial void LogTokenExchangeFailed(ILogger logger, Exception ex);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Zerodha access token cache hit.")]
    private static partial void LogTokenCacheHit(ILogger logger);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Zerodha access token not found in cache — no active session.")]
    private static partial void LogTokenCacheMiss(ILogger logger);
}
