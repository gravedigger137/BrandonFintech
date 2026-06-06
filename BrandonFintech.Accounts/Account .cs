namespace BrandonFintech.Accounts;

public class Account
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    public string AccountNumber { get; set; } = string.Empty;

    public decimal AvailableBalance { get; set; }

    public decimal PendingBalance { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}