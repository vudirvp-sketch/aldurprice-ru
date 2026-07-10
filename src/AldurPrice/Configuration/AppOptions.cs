namespace AldurPrice.Configuration;

/// <summary>
/// Глобальные настройки приложения (секция "App" в appsettings.json).
/// </summary>
public sealed class AppOptions
{
    public string Language { get; init; } = "auto";        // auto / ru / en
    public string LogLevel { get; init; } = "Information"; // Trace/Debug/Information/Warning/Error
    public bool BringToForeground { get; init; } = true;
    public bool AlwaysOnTop { get; init; } = false;
    public bool RememberDebugPanel { get; init; } = false;
    public bool CloseWithPoE2 { get; init; } = false;
    public bool OpenWithPoE2 { get; init; } = false;
    public bool AllOverlaysDisabled { get; init; } = false;
    public bool PricingOverlay { get; init; } = true;
    public bool Banner { get; init; } = true;
    public bool AdaptiveScanInterval { get; init; } = true;
    public bool PauseWhenUnfocused { get; init; } = true;
}
