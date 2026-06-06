using BrandonFintech.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BrandonFintech.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/dashboard")]
public class DashboardController : ControllerBase
{
    private const int RecentItemCount = 5;

    private readonly ApplicationDbContext _db;

    public DashboardController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet("summary")]
    public async Task<IActionResult> Summary()
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new
            {
                success = false,
                message = "Invalid token"
            });
        }

        var accounts = await _db.Accounts
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => new
            {
                x.Id,
                x.AvailableBalance
            })
            .ToListAsync();

        var accountIds = accounts
            .Select(x => x.Id)
            .ToList();

        var totalPayments = await _db.Payments
            .AsNoTracking()
            .CountAsync(x => x.UserId == userId);

        var totalTransfers = await _db.Transfers
            .AsNoTracking()
            .CountAsync(x => accountIds.Contains(x.FromAccountId) || accountIds.Contains(x.ToAccountId));

        var recentPayments = await _db.Payments
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(RecentItemCount)
            .Select(x => new
            {
                x.Id,
                x.Amount,
                x.Currency,
                x.Status,
                x.CreatedAt
            })
            .ToListAsync();

        var recentTransfers = await _db.Transfers
            .AsNoTracking()
            .Where(x => accountIds.Contains(x.FromAccountId) || accountIds.Contains(x.ToAccountId))
            .OrderByDescending(x => x.CreatedAt)
            .Take(RecentItemCount)
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
            summary = new
            {
                totalAccounts = accounts.Count,
                totalAvailableBalance = accounts.Sum(x => x.AvailableBalance),
                totalPayments,
                totalTransfers,
                recentPayments,
                recentTransfers
            }
        });
    }

    private bool TryGetUserId(out Guid userId)
    {
        var userIdValue =
            User.FindFirstValue(ClaimTypes.NameIdentifier) ??
            User.FindFirstValue("sub");

        return Guid.TryParse(userIdValue, out userId);
    }
}
