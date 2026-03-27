namespace FinanceAdvisor.API.Controllers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Telegram.Bot.Types;

[ApiController]
[Route("api/telegram")]
internal sealed partial class TelegramController : ControllerBase
{
    private readonly ILogger<TelegramController> _logger;
    private readonly IConfiguration _configuration;

    public TelegramController(ILogger<TelegramController> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    [HttpPost("webhook")]
    public async Task<IActionResult> WebhookAsync([FromBody] Update update, CancellationToken ct = default)
    {
        string? expectedSecret = _configuration["Telegram:WebhookSecret"];
        if (!Request.Headers.TryGetValue("X-Telegram-Bot-Api-Secret-Token", out Microsoft.Extensions.Primitives.StringValues headerValue)
            || headerValue != expectedSecret)
        {
            return Unauthorized();
        }

        string? text = update?.Message?.Text;
        if (text is null)
        {
            return Ok();
        }

        string correlationId = HttpContext.Items["CorrelationId"] as string ?? "none";
        string firstName = update?.Message?.From?.FirstName ?? string.Empty;

        // TODO FIN-50: replace with AppConstants.Timeouts.WebhookSeconds
        using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(HttpContext.RequestAborted, ct);
        cts.CancelAfter(TimeSpan.FromSeconds(10));

        try
        {
            LogWebhookReceived(_logger, correlationId, firstName, text);

            await Task.CompletedTask;
        }
        catch (OperationCanceledException)
        {
            LogWebhookTimeout(_logger, correlationId);
        }

        return Ok();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Webhook received. CorrelationId={CorrelationId} From={FirstName} Text={Text}")]
    private static partial void LogWebhookReceived(ILogger logger, string correlationId, string firstName, string text);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Webhook processing timed out. CorrelationId={CorrelationId}")]
    private static partial void LogWebhookTimeout(ILogger logger, string correlationId);
}
