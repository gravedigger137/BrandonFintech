using BrandonFintech.Accounts;
using BrandonFintech.Api.Services;
using BrandonFintech.Audit;
using BrandonFintech.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BrandonFintech.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/v1/admin")]
public class AdminController : ControllerBase
{
    private const decimal PromotionalCreditAmount = 500.00m;
    private const string PromotionalCreditReleasedEntryType = "PromotionalCreditReleased";

    private readonly ApplicationDbContext _db;
    private readonly ILedgerService _ledgerService;

    public AdminController(ApplicationDbContext db, ILedgerService ledgerService)
    {
        _db = db;
        _ledgerService = ledgerService;
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers()
    {
        var users = await _db.Users
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new
            {
                x.Id,
                x.Email,
                x.FirstName,
                x.LastName,
                x.Role,
                x.IsActive,
                x.CreatedAt
            })
            .ToListAsync();

        return Ok(new
        {
            success = true,
            users
        });
    }

    [HttpGet("accounts")]
    public async Task<IActionResult> GetAccounts()
    {
        var accounts = await _db.Accounts
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new
            {
                x.Id,
                x.UserId,
                x.AccountNumber,
                x.AvailableBalance,
                x.PendingBalance,
                x.IsActive,
                x.CreatedAt
            })
            .ToListAsync();

        return Ok(new
        {
            success = true,
            accounts
        });
    }

    [HttpGet("payments")]
    public async Task<IActionResult> GetPayments()
    {
        var payments = await _db.Payments
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new
            {
                x.Id,
                x.UserId,
                x.Amount,
                x.Currency,
                x.StripePaymentIntentId,
                x.Status,
                x.CreatedAt
            })
            .ToListAsync();

        return Ok(new
        {
            success = true,
            payments
        });
    }

    [HttpGet("transfers")]
    public async Task<IActionResult> GetTransfers()
    {
        var transfers = await _db.Transfers
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new
            {
                x.Id,
                x.FromAccountId,
                x.ToAccountId,
                x.Amount,
                x.Status,
                x.CreatedAt
            })
            .ToListAsync();

        return Ok(new
        {
            success = true,
            transfers
        });
    }

    [HttpGet("audit-logs")]
    public async Task<IActionResult> GetAuditLogs()
    {
        var auditLogs = await _db.AuditLogs
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new
            {
                x.Id,
                x.Action,
                x.EntityType,
                x.EntityId,
                x.CreatedAt
            })
            .ToListAsync();

        return Ok(new
        {
            success = true,
            auditLogs
        });
    }

    [HttpPost("accounts/{accountId:guid}/release-promo-credit")]
    public async Task<IActionResult> ReleasePromotionalCredit(Guid accountId)
    {
        var account = await _db.Accounts
            .FirstOrDefaultAsync(x => x.Id == accountId);

        if (account == null)
        {
            return NotFound(new
            {
                success = false,
                message = "Account not found"
            });
        }

        if (account.PendingBalance < PromotionalCreditAmount)
        {
            return BadRequest(new
            {
                success = false,
                message = "Account does not have enough pending promotional credit to release"
            });
        }

        account.PendingBalance -= PromotionalCreditAmount;
        account.AvailableBalance += PromotionalCreditAmount;

        _db.AuditLogs.Add(new AuditLog
        {
            Action = "PromotionalCreditReleased",
            EntityType = nameof(Account),
            EntityId = account.Id.ToString()
        });

        await _db.SaveChangesAsync();

        await _ledgerService.AddEntryAsync(
            account.Id,
            PromotionalCreditAmount,
            PromotionalCreditReleasedEntryType,
            "$500 signup promotional credit released by admin");

        return Ok(new
        {
            success = true,
            account = new
            {
                account.Id,
                account.UserId,
                account.AccountNumber,
                account.AvailableBalance,
                account.PendingBalance,
                account.IsActive,
                account.CreatedAt
            }
        });
    }
}
