using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace WrenchBox.Functions;

public sealed class WorkOrderMailer
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<WorkOrderMailer> _logger;

    public WorkOrderMailer(IConfiguration configuration, ILogger<WorkOrderMailer> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendAsync(WorkOrderNotification notification, CancellationToken cancellationToken)
    {
        var host = _configuration["Smtp:Host"];
        if (string.IsNullOrWhiteSpace(host))
        {
            _logger.LogInformation("SMTP host is empty. Skipped {EventType} for {OrderNumber}.", notification.EventType, notification.OrderNumber);
            return;
        }

        var port = int.TryParse(_configuration["Smtp:Port"], out var parsed) ? parsed : 1025;
        var from = _configuration["Smtp:From"] ?? "WrenchBox <noreply@wrenchbox.local>";
        var useSsl = bool.TryParse(_configuration["Smtp:UseSsl"], out var ssl) && ssl;

        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(from));
        message.To.Add(MailboxAddress.Parse(notification.CustomerEmail));
        message.Subject = notification.EventType == "BudgetApprovalRequested"
            ? $"Orçamento da OS {notification.OrderNumber}"
            : $"OS {notification.OrderNumber}: {notification.StatusLabel}";
        message.Body = new TextPart("html") { Text = BuildHtml(notification) };

        using var client = new SmtpClient();
        await client.ConnectAsync(host, port, useSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None, cancellationToken);
        var user = _configuration["Smtp:User"];
        if (!string.IsNullOrWhiteSpace(user))
            await client.AuthenticateAsync(user, _configuration["Smtp:Password"] ?? string.Empty, cancellationToken);
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }

    private static string BuildHtml(WorkOrderNotification notification)
    {
        if (notification.EventType == "BudgetApprovalRequested")
        {
            return $"""
                <p>Olá,</p>
                <p>O orçamento da ordem de serviço <strong>{notification.OrderNumber}</strong> no valor de <strong>{notification.TotalAmount:C}</strong> está aguardando a sua decisão.</p>
                <p><a href="{notification.ApproveUrl}">Aprovar orçamento</a> | <a href="{notification.RejectUrl}">Recusar orçamento</a></p>
                """;
        }

        return $"""
            <p>Olá,</p>
            <p>A ordem de serviço <strong>{notification.OrderNumber}</strong> foi atualizada para <strong>{notification.StatusLabel}</strong>.</p>
            """;
    }
}

public sealed class WorkOrderNotification
{
    public string EventType { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string OrderNumber { get; set; } = string.Empty;
    public decimal? TotalAmount { get; set; }
    public string? TrackingToken { get; set; }
    public string? Status { get; set; }
    public string? StatusLabel { get; set; }
    public string? ApproveUrl { get; set; }
    public string? RejectUrl { get; set; }
}
