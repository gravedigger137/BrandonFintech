namespace BrandonFintech.Contracts;

public class CreateDepositRequest
{
    public decimal Amount { get; set; }

    public string Description { get; set; } = string.Empty;
}
