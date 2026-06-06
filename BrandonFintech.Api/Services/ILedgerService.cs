namespace BrandonFintech.Api.Services;

public interface ILedgerService
{
    Task AddEntryAsync(Guid accountId, decimal amount, string entryType, string description);
}
