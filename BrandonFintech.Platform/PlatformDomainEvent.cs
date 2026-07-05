namespace BrandonFintech.Platform;

public sealed record PlatformDomainEvent(
    string Id,
    string Type,
    int Version,
    string Source,
    DateTimeOffset OccurredAt,
    IReadOnlyDictionary<string, object?> Payload,
    string? CorrelationId = null,
    string? TenantId = null);
