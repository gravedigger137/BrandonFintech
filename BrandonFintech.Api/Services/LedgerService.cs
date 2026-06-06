using BrandonFintech.Infrastructure;
using BrandonFintech.Ledger;

namespace BrandonFintech.Api.Services;

public class LedgerService : ILedgerService
{
    private readonly ApplicationDbContext _db;

    public LedgerService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task AddEntryAsync(Guid accountId, decimal amount, string entryType, string description)
    {
        var ledgerEntry = new LedgerEntry
        {
            AccountId = accountId,
            Amount = amount,
            EntryType = entryType,
            Description = description
        };

        _db.LedgerEntries.Add(ledgerEntry);

        await _db.SaveChangesAsync();
    }
}
