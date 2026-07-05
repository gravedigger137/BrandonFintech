namespace BrandonFintech.Platform;

public interface IPlatformEventPublisher
{
    Task PublishAsync(PlatformDomainEvent platformEvent, CancellationToken cancellationToken = default);
}
