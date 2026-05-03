namespace FinanceAdvisor.API.Controllers;

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
    private readonly IUpdateChannel _updateChannel;
    private readonly TelegramSettings _settings;

    /// <summary>Initializes a new instance of <see cref="TelegramController"/>.</summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="updateChannel">Channel for queuing updates for background processing.</param>
    /// <param name="settings">Telegram configuration options.</param>
    public TelegramController(
        ILogger<TelegramController> logger,
        IUpdateChannel updateChannel,
        IOptions<TelegramSettings> settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _logger = logger;
        _updateChannel = updateChannel;
        _settings = settings.Value;
    }

    /// <summary>
    /// Validates the incoming Telegram update, enqueues it for background processing,
    /// and returns 200 OK immediately — decoupling HTTP response time from orchestration latency.
    /// </summary>
    /// <param name="update">The Telegram update payload.</param>
    [HttpPost("webhook")]
    public async Task<IActionResult> Webhook([FromBody] Update update)
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

        IncomingMessageDto message = new()
        {
            Text = text,
            FromFirstName = firstName,
            ChatId = update?.Message?.Chat.Id ?? 0,
            CorrelationId = correlationId,
        };

        await _updateChannel.EnqueueAsync(message);
        LogUpdateEnqueued(_logger, correlationId, firstName);

        return Ok();
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "AUTH_DEBUG: RecLen={RecLen}, ExpLen={ExpLen}")]
    private static partial void LogAuthDebug(ILogger logger, int recLen, int expLen);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Update enqueued for background processing. CorrelationId={CorrelationId} From={FirstName}")]
    private static partial void LogUpdateEnqueued(ILogger logger, string correlationId, string firstName);
}
