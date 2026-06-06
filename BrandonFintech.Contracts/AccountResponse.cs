namespace BrandonFintech.Contracts;

public class AccountResponse
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string AccountNumber { get; set; } = string.Empty;

    public decimal AvailableBalance { get; set; }

    public decimal PendingBalance { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }
}
