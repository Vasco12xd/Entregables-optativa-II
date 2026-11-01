using System.Security.Cryptography;
using System.Text;
using LogiTracker.Application.Security;
using LogiTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LogiTracker.Infrastructure.Security;

/// <summary>Las claves de API nunca se guardan: solo su hash SHA-256 (riesgo de seguridad RS-01).</summary>
public static class ApiKeyHasher
{
    public static string Hash(string apiKey)
    {
        ArgumentNullException.ThrowIfNull(apiKey);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(apiKey));
        return Convert.ToHexStringLower(bytes);
    }
}

internal sealed class EfOperatorAuthenticator : IOperatorAuthenticator
{
    private const int MinimumKeyLength = 12;
    private const int MaximumKeyLength = 200;

    private readonly LogiTrackerDbContext _db;

    public EfOperatorAuthenticator(LogiTrackerDbContext db) => _db = db;

    public async Task<AuthenticatedOperator?> AuthenticateAsync(string apiKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(apiKey) || apiKey.Length < MinimumKeyLength || apiKey.Length > MaximumKeyLength)
            return null;

        var hash = ApiKeyHasher.Hash(apiKey);
        var record = await _db.Operators
            .AsNoTracking()
            .SingleOrDefaultAsync(o => o.ApiKeyHash == hash && o.IsActive, cancellationToken);

        return record is null ? null : new AuthenticatedOperator(record.Id, record.Alias);
    }
}
