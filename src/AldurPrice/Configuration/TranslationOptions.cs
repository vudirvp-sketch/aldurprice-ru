namespace AldurPrice.Configuration;

/// <summary>
/// Настройки перевода (секция "Translation" в appsettings.json).
/// </summary>
public sealed class TranslationOptions
{
    public string RuneshapeCombinationsPath { get; init; } = "ocr/runeshape-combinations-ru.json";
    public bool AutoUpdateFromExiledExchange { get; init; } = true;
}
