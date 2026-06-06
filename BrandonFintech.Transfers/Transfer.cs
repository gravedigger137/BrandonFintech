namespace BrandonFintech.Transfers;

public class Transfer
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid FromAccountId { get; set; }

    public Guid ToAccountId { get; set; }

    public decimal Amount { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}