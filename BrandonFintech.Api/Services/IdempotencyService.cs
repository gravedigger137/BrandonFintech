using BrandonFintech.Infrastructure;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BrandonFintech.Api.Services;

public class IdempotencyService : IIdempotencyService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ApplicationDbContext _db;

    public IdempotencyService(ApplicationDbContext db)
    {
        _db = db;
    }

    public string HashRequest(object? request)
    {
        var json = JsonSerializer.Serialize(request ?? new { }, JsonOptions);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(json));

        return Convert.ToHexString(hash);
    }

    public async Task<IdempotencyResult> CheckAsync(
        Guid userId,
        string endpoint,
        string? key,
        string requestHash,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return new IdempotencyResult
            {
                Status = IdempotencyResultStatus.MissingKey
            };
        }

        key = key.Trim();

        var existing = await _db.IdempotencyKeys
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.UserId == userId &&
                     x.Endpoint == endpoint &&
                     x.Key == key,
                cancellationToken);

        if (existing == null)
        {
            return new IdempotencyResult
            {
                Status = IdempotencyResultStatus.New,
                Key = key
            };
        }

        if (!string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal))
        {
            return new IdempotencyResult
            {
                Status = IdempotencyResultStatus.Conflict,
                Key = key
            };
        }

        return new IdempotencyResult
        {
            Status = IdempotencyResultStatus.Replay,
            Key = key,
            ResponseJson = existing.ResponseJson,
            StatusCode = existing.StatusCode
        };
    }

    public async Task StoreAsync(
        Guid userId,
        string endpoint,
        string key,
        string requestHash,
        object response,
        int statusCode,
        CancellationToken cancellationToken = default)
    {
        var record = new IdempotencyKey
        {
            UserId = userId,
            Endpoint = endpoint,
            Key = key,
            RequestHash = requestHash,
            ResponseJson = JsonSerializer.Serialize(response, JsonOptions),
            StatusCode = statusCode
        };

        _db.IdempotencyKeys.Add(record);

        await _db.SaveChangesAsync(cancellationToken);
    }
}
