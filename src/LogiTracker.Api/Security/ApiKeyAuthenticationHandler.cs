using System.Security.Claims;
using System.Text.Encodings.Web;
using LogiTracker.Application.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace LogiTracker.Api.Security;

/// <summary>
/// Autentica al operador con la cabecera X-Api-Key. La identidad del operador sale de aquí,
/// no del cuerpo de la petición: nadie puede asignar "en nombre de" otro operador (riesgo RS-02).
/// </summary>
internal sealed class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "ApiKey";
    public const string HeaderName = "X-Api-Key";
    public const string OperatorRole = "Operator";

    private readonly IOperatorAuthenticator _authenticator;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IOperatorAuthenticator authenticator)
        : base(options, logger, encoder)
    {
        _authenticator = authenticator;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var values) || string.IsNullOrWhiteSpace(values.ToString()))
            return AuthenticateResult.NoResult();

        var authenticated = await _authenticator.AuthenticateAsync(values.ToString(), Context.RequestAborted);
        if (authenticated is null)
            return AuthenticateResult.Fail("Invalid API key.");   // El valor de la clave nunca se registra.

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, authenticated.OperatorId.ToString()),
            new Claim(ClaimTypes.Name, authenticated.Alias),
            new Claim(ClaimTypes.Role, OperatorRole),
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
        return AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName));
    }
}

internal static class OperatorClaims
{
    public static Guid GetOperatorId(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var operatorId)
            ? operatorId
            : throw new InvalidOperationException("Authenticated operator has no identifier.");
    }
}
