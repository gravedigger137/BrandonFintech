namespace BrandonFintech.Contracts;

public class CreatePaymentIntentRequest
{
    public decimal Amount { get; set; }

    public string Currency { get; set; } = "USD";
}
