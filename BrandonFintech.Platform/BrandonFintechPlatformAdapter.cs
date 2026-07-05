namespace BrandonFintech.Platform;

public sealed class BrandonFintechPlatformAdapter
{
    private readonly bool enabled;
    private readonly IPlatformEventPublisher? publisher;

    public BrandonFintechPlatformAdapter(bool enabled = false, IPlatformEventPublisher? publisher = null)
    {
        this.enabled = enabled;
        this.publisher = publisher;
    }

    public async Task<PlatformPublishResult> PublishAsync(
        PlatformDomainEvent platformEvent,
        CancellationToken cancellationToken = default)
    {
        if (!enabled)
        {
            return new PlatformPublishResult(PlatformPublishResult.Disabled, platformEvent.Type);
        }

        if (publisher is null)
        {
            return new PlatformPublishResult(PlatformPublishResult.NotConfigured, platformEvent.Type, platformEvent.Id);
        }

        await publisher.PublishAsync(platformEvent, cancellationToken);

        return new PlatformPublishResult(PlatformPublishResult.Published, platformEvent.Type, platformEvent.Id);
    }

    public async Task<PlatformPublishResult> TryPublishAsync(
        PlatformDomainEvent platformEvent,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await PublishAsync(platformEvent, cancellationToken);
        }
        catch
        {
            return new PlatformPublishResult(PlatformPublishResult.Failed, platformEvent.Type, platformEvent.Id);
        }
    }
}
