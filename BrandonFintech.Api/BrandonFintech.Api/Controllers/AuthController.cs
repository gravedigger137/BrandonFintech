using BrandonFintech.Accounts;
using BrandonFintech.Audit;
using BrandonFintech.Api.Services;
using BrandonFintech.Contracts;
using BrandonFintech.Identity;
using BrandonFintech.Infrastructure;
using BrandonFintech.Ledger;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace BrandonFintech.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private const decimal SignupPromotionalCreditAmount = 500.00m;
    private const string PromotionalCreditPendingEntryType = "PromotionalCreditPending";

    private static readonly PasswordHasher<User> PasswordHasher = new();

    private readonly ApplicationDbContext _db;
    private readonly IJwtService _jwtService;

    public AuthController(ApplicationDbContext db, IJwtService jwtService)
    {
        _db = db;
        _jwtService = jwtService;
    }

    [EnableRateLimiting("AuthSensitive")]
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        var validationError = ValidateRegisterRequest(request);
        if (validationError != null)
        {
            return InvalidInput(validationError);
        }

        var email = request.Email.Trim().ToLowerInvariant();

        var exists = await _db.Users
            .AnyAsync(x => x.Email == email);

        if (exists)
        {
            return BadRequest(new
            {
                success = false,
                message = "Email already exists"
            });
        }

        var user = new User
        {
            Email = email,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Role = "User"
        };

        user.PasswordHash = PasswordHasher.HashPassword(user, request.Password);

        var defaultAccount = new Account
        {
            UserId = user.Id,
            AccountNumber = await GenerateUniqueAccountNumberAsync(),
            AvailableBalance = 0,
            PendingBalance = SignupPromotionalCreditAmount,
            IsActive = true
        };

        var promotionalLedgerEntry = new LedgerEntry
        {
            AccountId = defaultAccount.Id,
            Amount = SignupPromotionalCreditAmount,
            EntryType = PromotionalCreditPendingEntryType,
            Description = "$500 signup promotional credit pending review"
        };

        var promotionalAuditLog = new AuditLog
        {
            Action = "PromotionalCreditPendingCreated",
            EntityType = nameof(Account),
            EntityId = defaultAccount.Id.ToString()
        };

        _db.Users.Add(user);
        _db.Accounts.Add(defaultAccount);
        _db.LedgerEntries.Add(promotionalLedgerEntry);
        _db.AuditLogs.Add(promotionalAuditLog);

        await _db.SaveChangesAsync();

        return Ok(new
        {
            success = true,
            user = new
            {
                user.Id,
                user.Email,
                user.FirstName,
                user.LastName,
                user.Role
            },
            defaultAccount = new
            {
                defaultAccount.Id,
                defaultAccount.UserId,
                defaultAccount.AccountNumber,
                defaultAccount.AvailableBalance,
                defaultAccount.PendingBalance,
                defaultAccount.IsActive,
                defaultAccount.CreatedAt
            }
        });
    }

    [EnableRateLimiting("AuthSensitive")]
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var validationError = ValidateLoginRequest(request);
        if (validationError != null)
        {
            return InvalidInput(validationError);
        }

        var email = request.Email.Trim().ToLowerInvariant();

        var user = await _db.Users.FirstOrDefaultAsync(x => x.Email == email);

        if (user == null || !await IsPasswordValidAsync(user, request.Password))
        {
            return Unauthorized(new
            {
                success = false,
                message = "Invalid credentials"
            });
        }

        var accessToken = _jwtService.GenerateToken(user);

        return Ok(new
        {
            success = true,
            tokenType = "Bearer",
            accessToken,
            expiresIn = 3600,
            user = new
            {
                user.Id,
                user.Email,
                user.FirstName,
                user.LastName
            }
        });
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var userIdValue =
            User.FindFirstValue(ClaimTypes.NameIdentifier) ??
            User.FindFirstValue("sub");

        if (!Guid.TryParse(userIdValue, out var userId))
        {
            return Unauthorized(new
            {
                success = false,
                message = "Invalid token"
            });
        }

        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == userId);

        if (user == null)
        {
            return Unauthorized(new
            {
                success = false,
                message = "User not found"
            });
        }

        return Ok(new
        {
            success = true,
            user = new
            {
                user.Id,
                user.Email,
                user.FirstName,
                user.LastName
            }
        });
    }

    private async Task<bool> IsPasswordValidAsync(User user, string password)
    {
        var result = PasswordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = PasswordHasher.HashPassword(user, password);
            await _db.SaveChangesAsync();

            return true;
        }

        if (result == PasswordVerificationResult.Success)
        {
            return true;
        }

        var legacyHash = HashLegacyPassword(password);
        if (!string.Equals(user.PasswordHash, legacyHash, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        user.PasswordHash = PasswordHasher.HashPassword(user, password);
        await _db.SaveChangesAsync();

        return true;
    }

    private static string? ValidateRegisterRequest(RegisterRequest request)
    {
        if (request == null)
        {
            return "Request body is required";
        }

        if (string.IsNullOrWhiteSpace(request.Email) ||
            !new EmailAddressAttribute().IsValid(request.Email))
        {
            return "A valid email is required";
        }

        if (string.IsNullOrWhiteSpace(request.FirstName))
        {
            return "First name is required";
        }

        if (string.IsNullOrWhiteSpace(request.LastName))
        {
            return "Last name is required";
        }

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
        {
            return "Password must be at least 8 characters";
        }

        return null;
    }

    private static string? ValidateLoginRequest(LoginRequest request)
    {
        if (request == null)
        {
            return "Request body is required";
        }

        if (string.IsNullOrWhiteSpace(request.Email) ||
            !new EmailAddressAttribute().IsValid(request.Email))
        {
            return "A valid email is required";
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            return "Password is required";
        }

        return null;
    }

    private BadRequestObjectResult InvalidInput(string message)
        => BadRequest(new
        {
            success = false,
            message
        });

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

    private static string HashLegacyPassword(string password)
    {
        using var sha = SHA256.Create();

        var bytes = sha.ComputeHash(
            Encoding.UTF8.GetBytes(password));

        return Convert.ToHexString(bytes);
    }
}
