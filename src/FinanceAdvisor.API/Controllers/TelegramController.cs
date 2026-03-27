namespace FinanceAdvisor.API.Controllers;

using FinanceAdvisor.Core.Constants;
using FinanceAdvisor.Core.DTOs;
using FinanceAdvisor.Core.Interfaces;
using FinanceAdvisor.Core.Models.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Telegram.Bot.Types;

/// <summary>Handles incoming Telegram Bot webhook requests.</summary>
[ApiController]
[Route("api/telegram")]
public sealed partial class TelegramController : ControllerBase
{
    private readonly ILogger<TelegramController> _logger;
    private readonly ITelegramWebhookService _webhookService;
    private readonly TelegramSettings _settings;

    /// <summary>Initializes a new instance of <see cref="TelegramController"/>.</summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="webhookService">Webhook processing service.</param>
    /// <param name="settings">Telegram configuration options.</param>
    public TelegramController(
        ILogger<TelegramController> logger,
        ITelegramWebhookService webhookService,
        IOptions<TelegramSettings> settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _logger = logger;
        _webhookService = webhookService;
        _settings = settings.Value;
    }

    /// <summary>Receives a Telegram update and dispatches it for processing.</summary>
    /// <param name="update">The Telegram update payload.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost("webhook")]
    public async Task<IActionResult> WebhookAsync(
        [FromBody] Update update,
        CancellationToken ct = default)
    {
        if (!Request.Headers.TryGetValue("X-Telegram-Bot-Api-Secret-Token", out StringValues headerValue)
            || headerValue != _settings.WebhookSecret)
        {
            LogAuthDebug(_logger, headerValue.ToString().Length, _settings.WebhookSecret?.Length ?? 0);
            return Unauthorized();
        }

        string? text = update?.Message?.Text;
        if (text is null)
        {
            return Ok();
        }

        string correlationId = HttpContext.Items["CorrelationId"] as string ?? "none";
        string firstName = update?.Message?.From?.FirstName ?? string.Empty;

        using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(
            HttpContext.RequestAborted, ct);
        cts.CancelAfter(TimeSpan.FromSeconds(AppConstants.Timeouts.WebhookSeconds));

        try
        {
            IncomingMessageDto message = new()
            {
                Text = text,
                FromFirstName = firstName,
                CorrelationId = correlationId,
            };
            await _webhookService.HandleUpdateAsync(message, cts.Token);
            LogWebhookProcessed(_logger, correlationId, firstName, text);
        }
        catch (OperationCanceledException)
        {
            LogWebhookTimeout(_logger, correlationId);
        }

        return Ok();
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "AUTH_DEBUG: RecLen={RecLen}, ExpLen={ExpLen}")]
    private static partial void LogAuthDebug(ILogger logger, int recLen, int expLen);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Webhook processed. CorrelationId={CorrelationId} From={FirstName} Text={Text}")]
    private static partial void LogWebhookProcessed(
        ILogger logger, string correlationId, string firstName, string text);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Webhook processing timed out. CorrelationId={CorrelationId}")]
    private static partial void LogWebhookTimeout(ILogger logger, string correlationId);
}
