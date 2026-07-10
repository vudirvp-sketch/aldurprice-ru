namespace AldurPrice.Configuration;

/// <summary>
/// Настройки окна захвата (секция "Window" в appsettings.json).
/// </summary>
public sealed class WindowOptions
{
    public bool InitialSetupComplete { get; init; } = false;
    public int? CustomOffsetX { get; init; }
    public int? CustomOffsetY { get; init; }
    public int? CustomWidth { get; init; }
    public int? CustomHeight { get; init; }
}
