namespace BrandonFintech.Audit;

public class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Action { get; set; } = string.Empty;

    public string EntityType { get; set; } = string.Empty;

    public string EntityId { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}