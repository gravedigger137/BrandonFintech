namespace BrandonFintech.Platform;

public sealed record PlatformPublishResult(string Status, string EventType, string? EventId = null)
{
    public const string Disabled = "disabled";
    public const string Published = "published";
    public const string NotConfigured = "not_configured";
    public const string Failed = "failed";
}
