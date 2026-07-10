namespace AldurPrice.Core.Contracts;

/// <summary>
/// Снимок состояния окна лиги: захваченный битмап + распознанные строки с Y-координатами.
/// Передаётся от слоя OCR слою pricing/overlay.
/// </summary>
public sealed record LeagueWindowSnapshot(
    string[] ItemNames,
    int[] YPositions,
    bool IsPanelOpen,
    DateTimeOffset CapturedAt)
{
    public bool IsValid => ItemNames.Length == YPositions.Length;
}
