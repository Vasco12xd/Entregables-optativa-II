namespace LogiTracker.Application.Security;

/// <summary>Puerto de autenticación de operadores por clave de API (ADR-003).</summary>
public interface IOperatorAuthenticator
{
    /// <summary>Devuelve el operador dueño de la clave, o null si la clave no existe o el operador está inactivo.</summary>
    Task<AuthenticatedOperator?> AuthenticateAsync(string apiKey, CancellationToken cancellationToken);
}

public sealed record AuthenticatedOperator(Guid OperatorId, string Alias);
