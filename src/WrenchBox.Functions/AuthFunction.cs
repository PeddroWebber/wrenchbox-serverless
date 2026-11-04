using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace WrenchBox.Functions;

public class AuthFunction
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly TokenIssuer _tokens;
    private readonly CustomerDirectory _customers;
    private readonly ILogger<AuthFunction> _logger;

    public AuthFunction(TokenIssuer tokens, CustomerDirectory customers, ILogger<AuthFunction> logger)
    {
        _tokens = tokens;
        _customers = customers;
        _logger = logger;
    }

    [Function("Authenticate")]
    public async Task<IActionResult> Authenticate(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auth/token")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var body = await req.ReadFromJsonAsync<CpfRequest>(JsonOptions, cancellationToken);
        var digits = Cpf.OnlyDigits(body?.Cpf);
        var valid = Cpf.IsValid(digits);
        var customer = valid ? await _customers.FindByCpfAsync(digits, cancellationToken) : null;
        var denial = AuthDecision.Decide(valid, customer);

        if (denial != AuthDenial.None || customer is null)
        {
            _logger.LogInformation(
                "AuthDenied Reason={Reason} TraceId={TraceId}",
                denial,
                Activity.Current?.TraceId.ToString() ?? string.Empty);
            return new UnauthorizedObjectResult(new { error = "Não foi possível autenticar." });
        }

        var (token, expiresAt) = _tokens.Issue(customer.Id);
        return new OkObjectResult(new { token, expiresAt, tokenType = "Bearer" });
    }

    [Function("Validate")]
    public IActionResult Validate(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "auth/validate")] HttpRequest req)
    {
        var role = req.Query["role"].ToString();
        var authorized = _tokens.TryValidate(req.Headers.Authorization.ToString(), string.IsNullOrWhiteSpace(role) ? null : role);
        return authorized ? new OkResult() : new UnauthorizedResult();
    }

    private sealed class CpfRequest
    {
        public string? Cpf { get; set; }
    }
}
