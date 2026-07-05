namespace BrandonFintech.Platform;

public static class PlatformIntegrationFeature
{
    public const string EnvironmentVariableName = "PLATFORM_INTEGRATION_ENABLED";

    public static bool IsEnabled(IReadOnlyDictionary<string, string?> values)
    {
        return values.TryGetValue(EnvironmentVariableName, out var value) && IsEnabled(value);
    }

    public static bool IsEnabled(string? value)
    {
        return value is not null &&
            (value.Equals("1", StringComparison.OrdinalIgnoreCase) ||
             value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
             value.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
             value.Equals("on", StringComparison.OrdinalIgnoreCase));
    }
}
