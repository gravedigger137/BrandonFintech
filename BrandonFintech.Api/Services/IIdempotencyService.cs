namespace BrandonFintech.Api.Services;

public interface IIdempotencyService
{
    string HashRequest(object? request);

    Task<IdempotencyResult> CheckAsync(
        Guid userId,
        string endpoint,
        string? key,
        string requestHash,
        CancellationToken cancellationToken = default);

    Task StoreAsync(
        Guid userId,
        string endpoint,
        string key,
        string requestHash,
        object response,
        int statusCode,
        CancellationToken cancellationToken = default);
}

public enum IdempotencyResultStatus
{
    New,
    MissingKey,
    Replay,
    Conflict
}

public sealed class IdempotencyResult
{
    public IdempotencyResultStatus Status { get; init; }

    public string Key { get; init; } = string.Empty;

    public string? ResponseJson { get; init; }

    public int? StatusCode { get; init; }
}
