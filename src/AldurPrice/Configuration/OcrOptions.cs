namespace AldurPrice.Configuration;

/// <summary>
/// Настройки OCR (секция "OCR" в appsettings.json).
/// </summary>
public sealed class OcrOptions
{
    public string Language { get; init; } = "rus";            // eng/rus/deu/fra/spa/por/kor/chi_tra
    public string OcrBackend { get; init; } = "windows";      // windows / tesseract
    public string CaptureMode { get; init; } = "printwindow"; // printwindow / desktop (WGC)
    public bool SaveDebugImages { get; init; } = false;
    public int DebugImageIntervalSeconds { get; init; } = 15;
    public bool DebugOverlay { get; init; } = false;
    public bool HideDebugOverlayWhenInterfaceNotDetected { get; init; } = false;
    public int ScanIntervalMs { get; init; } = 100;           // 50-500, с adaptive
    public double? OverlayScale { get; init; }
    public bool EnableImagePreprocessing { get; init; } = true;
    public int BinarizationThreshold { get; init; } = 145;
    public bool EnableTextColorFiltering { get; init; } = true;
    public int TextColorTargetR { get; init; } = 50;
    public int TextColorTargetG { get; init; } = 42;
    public int TextColorTargetB { get; init; } = 34;
    public int TextColorTolerance { get; init; } = 47;
    public int TextColorMaxLuminance { get; init; } = 145;
    public int TextColorMaxChannelSpread { get; init; } = 29;
    public int OcrEngineMode { get; init; } = 2;              // 0=legacy, 1=LSTM+legacy, 2=LSTM only
    public bool BypassOcrCache { get; init; } = false;
    public string TesseractDataPath { get; init; } = "";
    public bool FrameDiffing { get; init; } = true;
}
