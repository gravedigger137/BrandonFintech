using BrandonFintech.Accounts;
using BrandonFintech.Audit;
using BrandonFintech.Identity;
using BrandonFintech.Payments;
using BrandonFintech.Platform;
using BrandonFintech.Transfers;

namespace BrandonFintech.Platform.Tests;

public sealed class BrandonFintechPlatformAdapterTests
{
    [Fact]
    public void Maps_BrandonFintech_Concepts_To_Platform_Contracts()
    {
        var userId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var accountId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var paymentId = Guid.Parse("00000000-0000-0000-0000-000000000003");
        var transferId = Guid.Parse("00000000-0000-0000-0000-000000000004");
        var toAccountId = Guid.Parse("00000000-0000-0000-0000-000000000005");
        var auditId = Guid.Parse("00000000-0000-0000-0000-000000000006");
        var createdAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var events = new[]
        {
            BrandonFintechPlatformMapper.MapUserCreated(new User
            {
                Id = userId,
                Email = "user@example.test",
                FirstName = "Example",
                LastName = "User",
                Role = "User",
                CreatedAt = createdAt
            }),
            BrandonFintechPlatformMapper.MapAccountCreated(new Account
            {
                Id = accountId,
                UserId = userId,
                AccountNumber = "123456789",
                CreatedAt = createdAt
            }),
            BrandonFintechPlatformMapper.MapPaymentCompleted(new Payment
            {
                Id = paymentId,
                UserId = userId,
                Amount = 100,
                Currency = "USD",
                Status = "completed",
                CreatedAt = createdAt
            }),
            BrandonFintechPlatformMapper.MapTransferCreated(new Transfer
            {
                Id = transferId,
                FromAccountId = accountId,
                ToAccountId = toAccountId,
                Amount = 25,
                Status = "pending",
                CreatedAt = createdAt
            }),
            BrandonFintechPlatformMapper.MapAuditLogged(new AuditLog
            {
                Id = auditId,
                Action = "account.created",
                EntityType = "account",
                EntityId = accountId.ToString(),
                CreatedAt = createdAt
            })
        };

        Assert.Equal(
            new[]
            {
                PlatformEventType.UserCreated,
                PlatformEventType.AccountCreated,
                PlatformEventType.PaymentCompleted,
                PlatformEventType.TransferCreated,
                PlatformEventType.AuditLogged
            },
            events.Select(platformEvent => platformEvent.Type));

        Assert.All(events, platformEvent =>
        {
            Assert.Equal("brandon-fintech", platformEvent.Source);
            Assert.Equal(1, platformEvent.Version);
            Assert.Equal(createdAt, platformEvent.OccurredAt.UtcDateTime);
        });

        Assert.Equal("6789", events[1].Payload["accountNumberLast4"]);
        Assert.False(events[1].Payload.ContainsKey("accountNumber"));
    }

    [Fact]
    public async Task Adapter_Is_Disabled_By_Default()
    {
        Assert.False(PlatformIntegrationFeature.IsEnabled(new Dictionary<string, string?>()));

        var adapter = new BrandonFintechPlatformAdapter();
        var platformEvent = BrandonFintechPlatformMapper.MapAuditLogged(new AuditLog
        {
            Action = "test",
            EntityType = "unit",
            EntityId = "unit_123"
        });

        var result = await adapter.PublishAsync(platformEvent);

        Assert.Equal(PlatformPublishResult.Disabled, result.Status);
        Assert.Equal(PlatformEventType.AuditLogged, result.EventType);
    }

    [Fact]
    public async Task Adapter_Publishes_When_Enabled_And_Configured()
    {
        var publisher = new RecordingPublisher();
        var adapter = new BrandonFintechPlatformAdapter(enabled: true, publisher);
        var platformEvent = BrandonFintechPlatformMapper.MapTransferCreated(new Transfer
        {
            FromAccountId = Guid.NewGuid(),
            ToAccountId = Guid.NewGuid(),
            Amount = 25,
            Status = "pending"
        });

        var result = await adapter.PublishAsync(platformEvent);

        Assert.Equal(PlatformPublishResult.Published, result.Status);
        Assert.Single(publisher.Events);
        Assert.Equal(PlatformEventType.TransferCreated, publisher.Events[0].Type);
    }

    private sealed class RecordingPublisher : IPlatformEventPublisher
    {
        public List<PlatformDomainEvent> Events { get; } = [];

        public Task PublishAsync(PlatformDomainEvent platformEvent, CancellationToken cancellationToken = default)
        {
            Events.Add(platformEvent);
            return Task.CompletedTask;
        }
    }
}
