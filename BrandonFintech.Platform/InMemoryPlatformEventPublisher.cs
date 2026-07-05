using System.Collections.Concurrent;

namespace BrandonFintech.Platform;

public sealed class InMemoryPlatformEventPublisher : IPlatformEventPublisher
{
    private readonly ConcurrentQueue<PlatformDomainEvent> events = new();

    public IReadOnlyCollection<PlatformDomainEvent> PublishedEvents => events.ToArray();

    public Task PublishAsync(
        PlatformDomainEvent platformEvent,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        events.Enqueue(platformEvent);

        return Task.CompletedTask;
    }
}
