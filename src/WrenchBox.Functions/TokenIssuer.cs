using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace WrenchBox.Functions;

public sealed class TokenIssuer
{
    private readonly string _secret;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly int _expiryMinutes;

    public TokenIssuer(IConfiguration configuration)
    {
        _secret = configuration["Jwt:Secret"] ?? string.Empty;
        _issuer = configuration["Jwt:Issuer"] ?? "WrenchBox";
        _audience = configuration["Jwt:Audience"] ?? "WrenchBox.Admin";
        _expiryMinutes = int.TryParse(configuration["Jwt:CustomerExpiryMinutes"], out var minutes) ? minutes : 30;
    }

    public (string Token, DateTime ExpiresAt) Issue(Guid customerId)
    {
        if (_secret.Length < 32)
            throw new InvalidOperationException("Jwt:Secret must contain at least 32 characters.");

        var expires = DateTime.UtcNow.AddMinutes(_expiryMinutes);
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, customerId.ToString()),
                new Claim("customer_id", customerId.ToString()),
                new Claim(ClaimTypes.Role, "Customer"),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            ],
            expires: expires,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }

    public bool TryValidate(string? bearer, string? requiredRole)
    {
        if (string.IsNullOrWhiteSpace(bearer) || _secret.Length < 32)
            return false;

        var token = bearer.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? bearer["Bearer ".Length..].Trim()
            : bearer.Trim();

        try
        {
            var principal = new JwtSecurityTokenHandler().ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = _issuer,
                ValidAudience = _audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret)),
                RoleClaimType = ClaimTypes.Role
            }, out _);

            if (string.IsNullOrWhiteSpace(requiredRole))
                return true;

            return principal.IsInRole(requiredRole)
                || principal.Claims.Any(c =>
                    (c.Type == ClaimTypes.Role || c.Type == "role") &&
                    string.Equals(c.Value, requiredRole, StringComparison.Ordinal));
        }
        catch (Exception)
        {
            return false;
        }
    }
}
