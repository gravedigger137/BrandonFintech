using BrandonFintech.Accounts;
using BrandonFintech.Audit;
using BrandonFintech.Identity;
using BrandonFintech.Payments;
using BrandonFintech.Transfers;

namespace BrandonFintech.Platform;

public static class BrandonFintechPlatformMapper
{
    private const string Source = "brandon-fintech";

    public static PlatformDomainEvent MapUserCreated(User user, string? correlationId = null)
    {
        return CreateEvent(
            PlatformEventType.UserCreated,
            user.CreatedAt,
            new Dictionary<string, object?>
            {
                ["userId"] = user.Id,
                ["email"] = user.Email,
                ["firstName"] = user.FirstName,
                ["lastName"] = user.LastName,
                ["role"] = user.Role,
                ["isActive"] = user.IsActive,
                ["createdAt"] = user.CreatedAt
            },
            correlationId);
    }

    public static PlatformDomainEvent MapAccountCreated(Account account, string? correlationId = null)
    {
        return CreateEvent(
            PlatformEventType.AccountCreated,
            account.CreatedAt,
            new Dictionary<string, object?>
            {
                ["accountId"] = account.Id,
                ["userId"] = account.UserId,
                ["accountNumberLast4"] = Last4(account.AccountNumber),
                ["isActive"] = account.IsActive,
                ["availableBalance"] = account.AvailableBalance,
                ["pendingBalance"] = account.PendingBalance,
                ["createdAt"] = account.CreatedAt
            },
            correlationId);
    }

    public static PlatformDomainEvent MapPaymentCompleted(Payment payment, string? correlationId = null)
    {
        return CreateEvent(
            PlatformEventType.PaymentCompleted,
            payment.CreatedAt,
            new Dictionary<string, object?>
            {
                ["paymentId"] = payment.Id,
                ["userId"] = payment.UserId,
                ["amount"] = payment.Amount,
                ["currency"] = payment.Currency,
                ["status"] = payment.Status,
                ["createdAt"] = payment.CreatedAt
            },
            correlationId);
    }

    public static PlatformDomainEvent MapTransferCreated(Transfer transfer, string? correlationId = null)
    {
        return CreateEvent(
            PlatformEventType.TransferCreated,
            transfer.CreatedAt,
            new Dictionary<string, object?>
            {
                ["transferId"] = transfer.Id,
                ["fromAccountId"] = transfer.FromAccountId,
                ["toAccountId"] = transfer.ToAccountId,
                ["amount"] = transfer.Amount,
                ["status"] = transfer.Status,
                ["createdAt"] = transfer.CreatedAt
            },
            correlationId);
    }

    public static PlatformDomainEvent MapAuditLogged(AuditLog auditLog, string? correlationId = null)
    {
        return CreateEvent(
            PlatformEventType.AuditLogged,
            auditLog.CreatedAt,
            new Dictionary<string, object?>
            {
                ["auditId"] = auditLog.Id,
                ["action"] = auditLog.Action,
                ["entityType"] = auditLog.EntityType,
                ["entityId"] = auditLog.EntityId,
                ["createdAt"] = auditLog.CreatedAt
            },
            correlationId);
    }

    private static PlatformDomainEvent CreateEvent(
        string type,
        DateTime occurredAt,
        IReadOnlyDictionary<string, object?> payload,
        string? correlationId)
    {
        return new PlatformDomainEvent(
            $"brandon_fintech_{type}_{Guid.NewGuid():N}",
            type,
            1,
            Source,
            DateTime.SpecifyKind(occurredAt, DateTimeKind.Utc),
            payload,
            correlationId);
    }

    private static string Last4(string value)
    {
        return value.Length <= 4 ? value : value[^4..];
    }
}
