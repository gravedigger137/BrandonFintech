using BrandonFintech.Audit;
using BrandonFintech.Api.Services;
using BrandonFintech.Contracts;
using BrandonFintech.Infrastructure;
using BrandonFintech.Payments;
using BrandonFintech.Platform;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stripe;
using System.Security.Claims;
using System.Text.Json;

namespace BrandonFintech.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/payments")]
public class PaymentsController : ControllerBase
{
    private const string CreatePaymentIntentEndpoint = "POST /api/v1/payments/intents";

    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly IIdempotencyService _idempotencyService;
    private readonly IMemoryCache _webhookReplayCache;
    private readonly BrandonFintechPlatformAdapter _platformAdapter;

    public PaymentsController(
        ApplicationDbContext db,
        IConfiguration configuration,
        IIdempotencyService idempotencyService,
        IMemoryCache webhookReplayCache,
        BrandonFintechPlatformAdapter platformAdapter)
    {
        _db = db;
        _configuration = configuration;
        _idempotencyService = idempotencyService;
        _webhookReplayCache = webhookReplayCache;
        _platformAdapter = platformAdapter;
    }

    [HttpPost("intents")]
    public async Task<IActionResult> CreateIntent(CreatePaymentIntentRequest request)
    {
        var validationError = ValidateCreatePaymentIntentRequest(request);
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
            CreatePaymentIntentEndpoint,
            Request.Headers["Idempotency-Key"].ToString(),
            requestHash);

        var idempotencyResult = HandleIdempotencyResult(idempotency);
        if (idempotencyResult != null)
        {
            return idempotencyResult;
        }

        var currency = NormalizeCurrency(request.Currency);

        var stripeSecretKey = _configuration["Stripe:SecretKey"];
        if (string.IsNullOrWhiteSpace(stripeSecretKey))
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                success = false,
                message = "Stripe is not configured"
            });
        }

        var paymentIntentService = new PaymentIntentService();
        var paymentIntent = await paymentIntentService.CreateAsync(
            new PaymentIntentCreateOptions
            {
                Amount = ToMinorUnits(request.Amount),
                Currency = currency!.ToLowerInvariant(),
                AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions
                {
                    Enabled = true
                },
                Metadata = new Dictionary<string, string>
                {
                    ["userId"] = userId.ToString()
                }
            },
            new RequestOptions
            {
                ApiKey = stripeSecretKey
            });

        var payment = new Payment
        {
            UserId = userId,
            Amount = request.Amount,
            Currency = currency!,
            StripePaymentIntentId = paymentIntent.Id,
            Status = paymentIntent.Status
        };

        _db.Payments.Add(payment);

        await _db.SaveChangesAsync();

        var response = new
        {
            success = true,
            payment = ToResponse(payment),
            clientSecret = paymentIntent.ClientSecret
        };

        await _idempotencyService.StoreAsync(
            userId,
            CreatePaymentIntentEndpoint,
            idempotency.Key,
            requestHash,
            response,
            StatusCodes.Status201Created);

        return StatusCode(StatusCodes.Status201Created, response);
    }

    [AllowAnonymous]
    [HttpPost("stripe/webhook")]
    public async Task<IActionResult> StripeWebhook()
    {
        var webhookSecret = _configuration["Stripe:WebhookSecret"];
        if (string.IsNullOrWhiteSpace(webhookSecret))
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                success = false,
                message = "Stripe webhook is not configured"
            });
        }

        var signatureHeader = Request.Headers["Stripe-Signature"].ToString();
        if (string.IsNullOrWhiteSpace(signatureHeader))
        {
            return BadRequest(new
            {
                success = false,
                message = "Missing Stripe signature"
            });
        }

        string payload;
        using (var reader = new StreamReader(Request.Body))
        {
            payload = await reader.ReadToEndAsync();
        }

        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(payload, signatureHeader, webhookSecret);
        }
        catch (StripeException)
        {
            return BadRequest(new
            {
                success = false,
                message = "Invalid Stripe signature"
            });
        }

        if (!string.IsNullOrWhiteSpace(stripeEvent.Id))
        {
            var replayCacheKey = $"stripe-event:{stripeEvent.Id}";
            if (_webhookReplayCache.TryGetValue(replayCacheKey, out _))
            {
                return Ok(new
                {
                    success = true,
                    received = true,
                    replayed = true,
                    handled = false
                });
            }

            _webhookReplayCache.Set(replayCacheKey, true, TimeSpan.FromHours(24));
        }

        var webhookReceivedAuditLog = new AuditLog
        {
            Action = "StripeWebhookReceived",
            EntityType = "StripeEvent",
            EntityId = stripeEvent.Id ?? string.Empty
        };

        _db.AuditLogs.Add(webhookReceivedAuditLog);

        if (!IsSupportedPaymentIntentEvent(stripeEvent.Type))
        {
            await _db.SaveChangesAsync();
            await _platformAdapter.TryPublishAsync(BrandonFintechPlatformMapper.MapAuditLogged(webhookReceivedAuditLog));

            return Ok(new
            {
                success = true,
                received = true,
                handled = false
            });
        }

        var paymentIntent = GetPaymentIntent(stripeEvent);
        if (paymentIntent == null)
        {
            await _db.SaveChangesAsync();
            await _platformAdapter.TryPublishAsync(BrandonFintechPlatformMapper.MapAuditLogged(webhookReceivedAuditLog));

            return Ok(new
            {
                success = true,
                received = true,
                handled = false
            });
        }

        var payment = await _db.Payments
            .FirstOrDefaultAsync(x => x.StripePaymentIntentId == paymentIntent.Id);

        if (payment == null)
        {
            var paymentNotFoundAuditLog = new AuditLog
            {
                Action = "StripePaymentNotFound",
                EntityType = nameof(PaymentIntent),
                EntityId = paymentIntent.Id
            };

            _db.AuditLogs.Add(paymentNotFoundAuditLog);

            await _db.SaveChangesAsync();
            await _platformAdapter.TryPublishAsync(BrandonFintechPlatformMapper.MapAuditLogged(webhookReceivedAuditLog));
            await _platformAdapter.TryPublishAsync(BrandonFintechPlatformMapper.MapAuditLogged(paymentNotFoundAuditLog));

            return Ok(new
            {
                success = true,
                received = true,
                handled = false
            });
        }

        payment.Status = paymentIntent.Status;

        var paymentStatusAuditLog = new AuditLog
        {
            Action = "PaymentStatusUpdatedFromStripeWebhook",
            EntityType = nameof(Payment),
            EntityId = payment.Id.ToString()
        };

        _db.AuditLogs.Add(paymentStatusAuditLog);

        await _db.SaveChangesAsync();
        await _platformAdapter.TryPublishAsync(BrandonFintechPlatformMapper.MapAuditLogged(webhookReceivedAuditLog));
        await _platformAdapter.TryPublishAsync(BrandonFintechPlatformMapper.MapAuditLogged(paymentStatusAuditLog));

        if (IsCompletedPaymentStatus(payment.Status))
        {
            await _platformAdapter.TryPublishAsync(BrandonFintechPlatformMapper.MapPaymentCompleted(payment));
        }

        return Ok(new
        {
            success = true,
            received = true,
            handled = true
        });
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

        var payments = await _db.Payments
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => ToResponse(x))
            .ToListAsync();

        return Ok(new
        {
            success = true,
            payments
        });
    }

    [HttpGet("{paymentId:guid}")]
    public async Task<IActionResult> GetById(Guid paymentId)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new
            {
                success = false,
                message = "Invalid token"
            });
        }

        var payment = await _db.Payments
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == paymentId && x.UserId == userId);

        if (payment == null)
        {
            return NotFound(new
            {
                success = false,
                message = "Payment not found"
            });
        }

        return Ok(new
        {
            success = true,
            payment = ToResponse(payment)
        });
    }

    private static long ToMinorUnits(decimal amount)
        => decimal.ToInt64(decimal.Round(amount * 100, 0, MidpointRounding.AwayFromZero));

    private static string? NormalizeCurrency(string currency)
    {
        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
        {
            return null;
        }

        return currency.ToUpperInvariant();
    }

    private static string? ValidateCreatePaymentIntentRequest(CreatePaymentIntentRequest request)
    {
        if (request == null)
        {
            return "Request body is required";
        }

        if (request.Amount <= 0)
        {
            return "Amount must be greater than 0";
        }

        if (request.Amount > 1_000_000)
        {
            return "Amount exceeds the MVP payment limit";
        }

        if (NormalizeCurrency(request.Currency) == null)
        {
            return "Currency must be a three-letter ISO currency code";
        }

        return null;
    }

    private static bool IsSupportedPaymentIntentEvent(string eventType)
        => eventType is
            "payment_intent.succeeded" or
            "payment_intent.payment_failed" or
            "payment_intent.canceled";

    private static bool IsCompletedPaymentStatus(string status)
        => status.Equals("succeeded", StringComparison.OrdinalIgnoreCase);

    private static PaymentIntent? GetPaymentIntent(Event stripeEvent)
    {
        if (stripeEvent.Data.Object is PaymentIntent paymentIntent)
        {
            return paymentIntent;
        }

        return JsonSerializer.Deserialize<PaymentIntent>(
            stripeEvent.Data.RawObject.GetRawText());
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

    private static PaymentResponse ToResponse(Payment payment)
        => new()
        {
            Id = payment.Id,
            UserId = payment.UserId,
            Amount = payment.Amount,
            Currency = payment.Currency,
            StripePaymentIntentId = payment.StripePaymentIntentId,
            Status = payment.Status,
            CreatedAt = payment.CreatedAt
        };
}
