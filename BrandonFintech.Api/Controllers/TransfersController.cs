using BrandonFintech.Accounts;
using BrandonFintech.Api.Services;
using BrandonFintech.Audit;
using BrandonFintech.Contracts;
using BrandonFintech.Infrastructure;
using BrandonFintech.Platform;
using BrandonFintech.Transfers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BrandonFintech.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/transfers")]
public class TransfersController : ControllerBase
{
    private const string CreateInternalTransferEndpoint = "POST /api/v1/transfers/internal";
    private const string CompletedStatus = "Completed";
    private const string DebitEntryType = "Debit";
    private const string CreditEntryType = "Credit";

    private readonly ApplicationDbContext _db;
    private readonly ILedgerService _ledgerService;
    private readonly IIdempotencyService _idempotencyService;
    private readonly BrandonFintechPlatformAdapter _platformAdapter;

    public TransfersController(
        ApplicationDbContext db,
        ILedgerService ledgerService,
        IIdempotencyService idempotencyService,
        BrandonFintechPlatformAdapter platformAdapter)
    {
        _db = db;
        _ledgerService = ledgerService;
        _idempotencyService = idempotencyService;
        _platformAdapter = platformAdapter;
    }

    [HttpPost("internal")]
    public async Task<IActionResult> CreateInternal(CreateTransferRequest request)
    {
        var validationError = ValidateCreateTransferRequest(request);
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

        var requestHash = _idempotencyService.HashRequest(request);
        var idempotency = await _idempotencyService.CheckAsync(
            userId,
            CreateInternalTransferEndpoint,
            Request.Headers["Idempotency-Key"].ToString(),
            requestHash);

        var idempotencyResult = HandleIdempotencyResult(idempotency);
        if (idempotencyResult != null)
        {
            return idempotencyResult;
        }

        var fromAccount = await _db.Accounts
            .FirstOrDefaultAsync(x => x.Id == request.FromAccountId);

        if (fromAccount == null || fromAccount.UserId != userId)
        {
            return Forbid();
        }

        var toAccount = await _db.Accounts
            .FirstOrDefaultAsync(x => x.Id == request.ToAccountId);

        if (toAccount == null)
        {
            return BadRequest(new
            {
                success = false,
                message = "Destination account does not exist"
            });
        }

        if (fromAccount.AvailableBalance < request.Amount)
        {
            return BadRequest(new
            {
                success = false,
                message = "Insufficient available balance"
            });
        }

        fromAccount.AvailableBalance -= request.Amount;
        toAccount.AvailableBalance += request.Amount;

        var transfer = new Transfer
        {
            FromAccountId = fromAccount.Id,
            ToAccountId = toAccount.Id,
            Amount = request.Amount,
            Status = CompletedStatus
        };

        var auditLog = new AuditLog
        {
            Action = "InternalTransferCompleted",
            EntityType = nameof(Transfer),
            EntityId = transfer.Id.ToString()
        };

        _db.Transfers.Add(transfer);
        _db.AuditLogs.Add(auditLog);

        await _db.SaveChangesAsync();
        await _platformAdapter.TryPublishAsync(BrandonFintechPlatformMapper.MapTransferCreated(transfer));
        await _platformAdapter.TryPublishAsync(BrandonFintechPlatformMapper.MapAuditLogged(auditLog));

        var description = string.IsNullOrWhiteSpace(request.Description)
            ? "Internal transfer"
            : request.Description;

        await _ledgerService.AddEntryAsync(
            fromAccount.Id,
            request.Amount,
            DebitEntryType,
            description);

        await _ledgerService.AddEntryAsync(
            toAccount.Id,
            request.Amount,
            CreditEntryType,
            description);

        var response = new
        {
            success = true,
            transfer = ToResponse(transfer)
        };

        await _idempotencyService.StoreAsync(
            userId,
            CreateInternalTransferEndpoint,
            idempotency.Key,
            requestHash,
            response,
            StatusCodes.Status201Created);

        return StatusCode(StatusCodes.Status201Created, response);
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

        var transfers = await _db.Transfers
            .AsNoTracking()
            .Where(x => accountIds.Contains(x.FromAccountId) || accountIds.Contains(x.ToAccountId))
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => ToResponse(x))
            .ToListAsync();

        return Ok(new
        {
            success = true,
            transfers
        });
    }

    [HttpGet("{transferId:guid}")]
    public async Task<IActionResult> GetById(Guid transferId)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new
            {
                success = false,
                message = "Invalid token"
            });
        }

        var transfer = await _db.Transfers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == transferId);

        if (transfer == null)
        {
            return NotFound(new
            {
                success = false,
                message = "Transfer not found"
            });
        }

        var ownsRelatedAccount = await _db.Accounts
            .AsNoTracking()
            .AnyAsync(x =>
                x.UserId == userId &&
                (x.Id == transfer.FromAccountId || x.Id == transfer.ToAccountId));

        if (!ownsRelatedAccount)
        {
            return NotFound(new
            {
                success = false,
                message = "Transfer not found"
            });
        }

        return Ok(new
        {
            success = true,
            transfer = ToResponse(transfer)
        });
    }

    private bool TryGetUserId(out Guid userId)
    {
        var userIdValue =
            User.FindFirstValue(ClaimTypes.NameIdentifier) ??
            User.FindFirstValue("sub");

        return Guid.TryParse(userIdValue, out userId);
    }

    private static string? ValidateCreateTransferRequest(CreateTransferRequest request)
    {
        if (request == null)
        {
            return "Request body is required";
        }

        if (request.FromAccountId == Guid.Empty)
        {
            return "FromAccountId is required";
        }

        if (request.ToAccountId == Guid.Empty)
        {
            return "ToAccountId is required";
        }

        if (request.FromAccountId == request.ToAccountId)
        {
            return "FromAccount and ToAccount must be different";
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

    private BadRequestObjectResult InvalidInput(string message)
        => BadRequest(new
        {
            success = false,
            message
        });

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

    private static TransferResponse ToResponse(Transfer transfer)
        => new()
        {
            Id = transfer.Id,
            FromAccountId = transfer.FromAccountId,
            ToAccountId = transfer.ToAccountId,
            Amount = transfer.Amount,
            Status = transfer.Status,
            CreatedAt = transfer.CreatedAt
        };
}
