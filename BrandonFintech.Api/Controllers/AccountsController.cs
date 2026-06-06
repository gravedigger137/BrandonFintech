using BrandonFintech.Accounts;
using BrandonFintech.Api.Services;
using BrandonFintech.Audit;
using BrandonFintech.Contracts;
using BrandonFintech.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Security.Claims;
using System.Text;

namespace BrandonFintech.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/accounts")]
public class AccountsController : ControllerBase
{
    private const string CreateAccountEndpoint = "POST /api/v1/accounts";
    private const string DepositEntryType = "Deposit";

    private readonly ApplicationDbContext _db;
    private readonly IIdempotencyService _idempotencyService;
    private readonly ILedgerService _ledgerService;

    public AccountsController(
        ApplicationDbContext db,
        IIdempotencyService idempotencyService,
        ILedgerService ledgerService)
    {
        _db = db;
        _idempotencyService = idempotencyService;
        _ledgerService = ledgerService;
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] CreateAccountRequest? request)
    {
        if (!ModelState.IsValid)
        {
            return InvalidInput("Invalid account request");
        }

        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new
            {
                success = false,
                message = "Invalid token"
            });
        }

        var requestHash = _idempotencyService.HashRequest(request);
        var idempotency = await _idempotencyService.CheckAsync(
            userId,
            CreateAccountEndpoint,
            Request.Headers["Idempotency-Key"].ToString(),
            requestHash);

        var idempotencyResult = HandleIdempotencyResult(idempotency);
        if (idempotencyResult != null)
        {
            return idempotencyResult;
        }

        var account = new Account
        {
            UserId = userId,
            AccountNumber = await GenerateUniqueAccountNumberAsync(),
            AvailableBalance = 0,
            PendingBalance = 0,
            IsActive = true
        };

        var auditLog = new AuditLog
        {
            Action = "AccountCreated",
            EntityType = nameof(Account),
            EntityId = account.Id.ToString()
        };

        _db.Accounts.Add(account);
        _db.AuditLogs.Add(auditLog);

        await _db.SaveChangesAsync();

        var response = new
        {
            success = true,
            account = ToResponse(account)
        };

        await _idempotencyService.StoreAsync(
            userId,
            CreateAccountEndpoint,
            idempotency.Key,
            requestHash,
            response,
            StatusCodes.Status201Created);

        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpPost("{accountId:guid}/deposit")]
    public async Task<IActionResult> Deposit(Guid accountId, CreateDepositRequest request)
    {
        var validationError = ValidateCreateDepositRequest(request);
        if (validationError != null)
        {
            return InvalidInput(validationError);
        }

        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new
            {
                success = false,
                message = "Invalid token"
            });
        }

        var endpoint = $"POST /api/v1/accounts/{accountId}/deposit";
        var requestHash = _idempotencyService.HashRequest(request);
        var idempotency = await _idempotencyService.CheckAsync(
            userId,
            endpoint,
            Request.Headers["Idempotency-Key"].ToString(),
            requestHash);

        var idempotencyResult = HandleIdempotencyResult(idempotency);
        if (idempotencyResult != null)
        {
            return idempotencyResult;
        }

        var account = await _db.Accounts
            .FirstOrDefaultAsync(x => x.Id == accountId && x.UserId == userId);

        if (account == null)
        {
            return NotFound(new
            {
                success = false,
                message = "Account not found"
            });
        }

        account.AvailableBalance += request.Amount;

        _db.AuditLogs.Add(new AuditLog
        {
            Action = "AccountDepositCompleted",
            EntityType = nameof(Account),
            EntityId = account.Id.ToString()
        });

        await _db.SaveChangesAsync();

        var description = string.IsNullOrWhiteSpace(request.Description)
            ? "MVP account deposit"
            : request.Description.Trim();

        await _ledgerService.AddEntryAsync(
            account.Id,
            request.Amount,
            DepositEntryType,
            description);

        var response = new
        {
            success = true,
            account = ToResponse(account)
        };

        await _idempotencyService.StoreAsync(
            userId,
            endpoint,
            idempotency.Key,
            requestHash,
            response,
            StatusCodes.Status200OK);

        return Ok(response);
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

        var accounts = await _db.Accounts
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => ToResponse(x))
            .ToListAsync();

        return Ok(new
        {
            success = true,
            accounts
        });
    }

    [HttpGet("{accountId:guid}/statement.csv")]
    public async Task<IActionResult> ExportStatementCsv(Guid accountId)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new
            {
                success = false,
                message = "Invalid token"
            });
        }

        var accountExists = await _db.Accounts
            .AsNoTracking()
            .AnyAsync(x => x.Id == accountId && x.UserId == userId);

        if (!accountExists)
        {
            return NotFound(new
            {
                success = false,
                message = "Account not found"
            });
        }

        var ledgerEntries = await _db.LedgerEntries
            .AsNoTracking()
            .Where(x => x.AccountId == accountId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new
            {
                x.CreatedAt,
                x.EntryType,
                x.Description,
                x.Amount,
                x.AccountId
            })
            .ToListAsync();

        var csv = new StringBuilder();
        csv.AppendLine("Date,Type,Description,Amount,AccountId");

        foreach (var entry in ledgerEntries)
        {
            csv.Append(CsvEscape(entry.CreatedAt.ToString("O", CultureInfo.InvariantCulture)));
            csv.Append(',');
            csv.Append(CsvEscapeText(entry.EntryType));
            csv.Append(',');
            csv.Append(CsvEscapeText(entry.Description));
            csv.Append(',');
            csv.Append(CsvEscape(entry.Amount.ToString(CultureInfo.InvariantCulture)));
            csv.Append(',');
            csv.Append(CsvEscape(entry.AccountId.ToString()));
            csv.AppendLine();
        }

        Response.Headers.ContentDisposition = $"attachment; filename=\"statement-{accountId}.csv\"";

        return Content(csv.ToString(), "text/csv", Encoding.UTF8);
    }

    [HttpGet("{accountId:guid}")]
    public async Task<IActionResult> GetById(Guid accountId)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new
            {
                success = false,
                message = "Invalid token"
            });
        }

        var account = await _db.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == accountId && x.UserId == userId);

        if (account == null)
        {
            return NotFound(new
            {
                success = false,
                message = "Account not found"
            });
        }

        return Ok(new
        {
            success = true,
            account = ToResponse(account)
        });
    }

    private async Task<string> GenerateUniqueAccountNumberAsync()
    {
        string accountNumber;

        do
        {
            accountNumber = $"BF{DateTime.UtcNow:yyyyMMdd}{Random.Shared.Next(100000, 999999)}";
        }
        while (await _db.Accounts.AnyAsync(x => x.AccountNumber == accountNumber));

        return accountNumber;
    }

    private bool TryGetUserId(out Guid userId)
    {
        var userIdValue =
            User.FindFirstValue(ClaimTypes.NameIdentifier) ??
            User.FindFirstValue("sub");

        return Guid.TryParse(userIdValue, out userId);
    }

    private BadRequestObjectResult InvalidInput(string message)
        => BadRequest(new
        {
            success = false,
            message
        });

    private static string? ValidateCreateDepositRequest(CreateDepositRequest request)
    {
        if (request == null)
        {
            return "Request body is required";
        }

        if (request.Amount <= 0)
        {
            return "Amount must be greater than 0";
        }

        if (request.Description.Length > 500)
        {
            return "Description cannot exceed 500 characters";
        }

        return null;
    }

    private IActionResult? HandleIdempotencyResult(IdempotencyResult result)
        => result.Status switch
        {
            IdempotencyResultStatus.MissingKey => BadRequest(new
            {
                success = false,
                message = "Idempotency-Key header is required"
            }),
            IdempotencyResultStatus.Conflict => Conflict(new
            {
                success = false,
                message = "Idempotency-Key was reused with a different request"
            }),
            IdempotencyResultStatus.Replay => new ContentResult
            {
                Content = result.ResponseJson ?? "{}",
                ContentType = "application/json",
                StatusCode = result.StatusCode ?? StatusCodes.Status200OK
            },
            _ => null
        };

    private static AccountResponse ToResponse(Account account)
        => new()
        {
            Id = account.Id,
            UserId = account.UserId,
            AccountNumber = account.AccountNumber,
            AvailableBalance = account.AvailableBalance,
            PendingBalance = account.PendingBalance,
            IsActive = account.IsActive,
            CreatedAt = account.CreatedAt
        };

    private static string CsvEscape(string value)
    {
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }

    private static string CsvEscapeText(string value)
    {
        if (!string.IsNullOrEmpty(value) && "=+-@\t".Contains(value[0]))
        {
            value = "'" + value;
        }

        return CsvEscape(value);
    }
}
