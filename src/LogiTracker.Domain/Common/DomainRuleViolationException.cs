namespace LogiTracker.Domain.Common;

/// <summary>
/// Se lanza cuando una operación rompería una invariante del dominio.
/// El código es estable y forma parte del catálogo de errores del contrato de aplicación.
/// </summary>
public sealed class DomainRuleViolationException : Exception
{
    public DomainRuleViolationException(string code)
        : base($"Domain rule violated: {code}")
    {
        Code = code;
    }

    public string Code { get; }
}
