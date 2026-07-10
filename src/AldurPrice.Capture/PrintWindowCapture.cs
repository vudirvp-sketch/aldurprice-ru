using Microsoft.Extensions.Logging;

namespace AldurPrice.Capture;

/// <summary>
/// Win32 <c>PrintWindow</c> — primary стратегия захвата.
/// Быстрее WGC для оконного режима, не требует Graphics Capture API.
///
/// <para><b>M0 — заглушка.</b> Полная реализация с P/Invoke <c>user32!PrintWindow</c> — в M1.4.</para>
/// </summary>
public sealed class PrintWindowCapture : ICaptureStrategy
{
    private readonly ILogger<PrintWindowCapture> _logger;

    public PrintWindowCapture(ILogger<PrintWindowCapture> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _logger.LogInformation("PrintWindowCapture initialised (M0 stub)");
    }

    /// <inheritdoc/>
    public string Name => "printwindow";

    /// <inheritdoc/>
    public Task<byte[]> CaptureAsync(CaptureRegion region, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(region);
        if (!region.IsValid)
            throw new ArgumentException("Capture region is not valid", nameof(region));
        // M1.4: [DllImport("user32")] PrintWindow → Bitmap → PNG bytes.
        throw new NotImplementedException("PrintWindowCapture.CaptureAsync — implemented in M1.4");
    }
}
