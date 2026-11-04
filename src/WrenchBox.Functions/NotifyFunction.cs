using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace WrenchBox.Functions;

public class NotifyFunction
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly WorkOrderMailer _mailer;
    private readonly string _key;

    public NotifyFunction(WorkOrderMailer mailer, Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        _mailer = mailer;
        _key = configuration["Notify:Key"] ?? string.Empty;
    }

    [Function("NotifyWorkOrder")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "notifications/work-orders")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var provided = req.Headers["x-functions-key"].ToString();
        if (string.IsNullOrWhiteSpace(_key) || !string.Equals(provided, _key, StringComparison.Ordinal))
            return new UnauthorizedResult();

        var notification = await req.ReadFromJsonAsync<WorkOrderNotification>(JsonOptions, cancellationToken);
        if (notification is null || string.IsNullOrWhiteSpace(notification.CustomerEmail) || string.IsNullOrWhiteSpace(notification.OrderNumber))
            return new BadRequestObjectResult(new { error = "customerEmail and orderNumber are required." });

        await _mailer.SendAsync(notification, cancellationToken);
        return new AcceptedResult();
    }
}
