namespace FinanceAdvisor.API.Controllers;

using FinanceAdvisor.Core.Exceptions;
using FinanceAdvisor.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

/// <summary>Handles the Zerodha Kite Connect OAuth login and callback flow.</summary>
[ApiController]
[Route("api/auth")]
public sealed partial class AuthController : ControllerBase
{
    private readonly IZerodhaAuthService _authService;
    private readonly ILogger<AuthController> _logger;

    /// <summary>Initializes a new instance of <see cref="AuthController"/>.</summary>
    /// <param name="authService">Zerodha authentication service.</param>
    /// <param name="logger">Logger instance.</param>
    public AuthController(IZerodhaAuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    /// <summary>Redirects the browser to the Zerodha Kite Connect OAuth login page.</summary>
    [HttpGet("login")]
    public IActionResult Login()
    {
        string loginUrl = _authService.GetLoginUrl();
        LogLoginRedirect(_logger, loginUrl);
        return Redirect(loginUrl);
    }

    /// <summary>Receives the Zerodha OAuth callback, exchanges the request token, and stores the access token.</summary>
    /// <param name="requestToken">One-time token returned by Zerodha after user login.</param>
    /// <param name="status">Login outcome reported by Zerodha ("success" or "error").</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("callback")]
    public async Task<IActionResult> CallbackAsync(
        [FromQuery(Name = "request_token")] string? requestToken,
        [FromQuery] string? status,
        CancellationToken ct = default)
    {
        string correlationId = HttpContext.Items["CorrelationId"] as string ?? "none";

        if (status != "success" || string.IsNullOrWhiteSpace(requestToken))
        {
            LogCallbackFailed(_logger, correlationId, status);
            return BadRequest("Zerodha login was not successful or request_token is missing.");
        }

        try
        {
            await _authService.ExchangeRequestTokenAsync(requestToken, ct);
            LogCallbackSuccess(_logger, correlationId);
            return Ok("Zerodha authentication successful. Access token stored.");
        }
        catch (ZerodhaAuthException ex)
        {
            LogCallbackError(_logger, correlationId, ex);
            return StatusCode(502, "Failed to exchange Zerodha request token. Please try again.");
        }
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Redirecting to Zerodha login. Url={LoginUrl}")]
    private static partial void LogLoginRedirect(ILogger logger, string loginUrl);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Zerodha callback received with failed status. CorrelationId={CorrelationId} Status={Status}")]
    private static partial void LogCallbackFailed(ILogger logger, string correlationId, string? status);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Zerodha callback processed successfully. CorrelationId={CorrelationId}")]
    private static partial void LogCallbackSuccess(ILogger logger, string correlationId);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Zerodha token exchange failed in callback. CorrelationId={CorrelationId}")]
    private static partial void LogCallbackError(ILogger logger, string correlationId, Exception ex);
}
