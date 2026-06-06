namespace BrandonFintech.Infrastructure;

public class IdempotencyKey
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Key { get; set; } = string.Empty;

    public Guid UserId { get; set; }

    public string Endpoint { get; set; } = string.Empty;

    public string RequestHash { get; set; } = string.Empty;

    public string ResponseJson { get; set; } = string.Empty;

    public int StatusCode { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
