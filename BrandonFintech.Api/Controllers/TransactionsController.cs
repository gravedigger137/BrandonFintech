using BrandonFintech.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BrandonFintech.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/transactions")]
public class TransactionsController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public TransactionsController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new
            {
                success = false,
                message = "Invalid token"
            });
        }

        var accountIds = await _db.Accounts
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => x.Id)
            .ToListAsync();

        var ledgerEntries = await _db.LedgerEntries
            .AsNoTracking()
            .Where(x => accountIds.Contains(x.AccountId))
            .Select(x => new TransactionItem
            {
                Id = x.Id,
                Type = "LedgerEntry",
                Amount = x.Amount,
                Currency = null,
                Status = x.EntryType,
                Description = x.Description,
                AccountId = x.AccountId,
                FromAccountId = null,
                ToAccountId = null,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync();

        var payments = await _db.Payments
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => new TransactionItem
            {
                Id = x.Id,
                Type = "Payment",
                Amount = x.Amount,
                Currency = x.Currency,
                Status = x.Status,
                Description = "Stripe PaymentIntent",
                AccountId = null,
                FromAccountId = null,
                ToAccountId = null,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync();

        var transfers = await _db.Transfers
            .AsNoTracking()
            .Where(x => accountIds.Contains(x.FromAccountId) || accountIds.Contains(x.ToAccountId))
            .Select(x => new TransactionItem
            {
                Id = x.Id,
                Type = "Transfer",
                Amount = x.Amount,
                Currency = null,
                Status = x.Status,
                Description = "Internal transfer",
                AccountId = null,
                FromAccountId = x.FromAccountId,
                ToAccountId = x.ToAccountId,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync();

        var transactions = ledgerEntries
            .Concat(payments)
            .Concat(transfers)
            .OrderByDescending(x => x.CreatedAt)
            .ToList();

        return Ok(new
        {
            success = true,
            transactions
        });
    }

    private bool TryGetUserId(out Guid userId)
    {
        var userIdValue =
            User.FindFirstValue(ClaimTypes.NameIdentifier) ??
            User.FindFirstValue("sub");

        return Guid.TryParse(userIdValue, out userId);
    }

    private sealed class TransactionItem
    {
        public Guid Id { get; set; }

        public string Type { get; set; } = string.Empty;

        public decimal Amount { get; set; }

        public string? Currency { get; set; }

        public string Status { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public Guid? AccountId { get; set; }

        public Guid? FromAccountId { get; set; }

        public Guid? ToAccountId { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
