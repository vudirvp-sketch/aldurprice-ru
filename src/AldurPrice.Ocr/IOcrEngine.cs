namespace AldurPrice.Ocr;

/// <summary>
/// OCR-движок: распознаёт текст на битмапе. Реализации —
/// <see cref="WindowsOcrEngine"/> (Windows.Media.Ocr, primary) и
/// <see cref="TesseractEngine"/> (Tesseract 5.2, fallback).
///
/// <para>Возвращает массив строк (по строке на распознанную текстовую линию) +
/// Y-координаты линий — нужно слою pricing/overlay для раскладки цен.</para>
/// </summary>
public interface IOcrEngine
{
    /// <summary>Имя движка: "windows" / "tesseract".</summary>
    string Name { get; }

    /// <summary>Доступен ли движок на текущей системе (Windows 10 1809+ для Windows OCR).</summary>
    bool IsAvailable { get; }

    /// <summary>Распознать текст на битмапе. Возвращает массив строк и Y-координат.</summary>
    /// <param name="bitmap">Захваченный битмап региона экрана.</param>
    /// <param name="language">Код языка: "rus", "eng", и т.д.</param>
    /// <param name="cancellationToken">Токен отмены для cooperative cancellation.</param>
    Task<OcrResult> RecognizeAsync(byte[] bitmap, string language, CancellationToken cancellationToken = default);
}

/// <summary>Результат OCR: строки текста + Y-координаты в пикселях.</summary>
public sealed record OcrResult(IReadOnlyList<OcrLine> Lines);

/// <summary>Одна распознанная линия.</summary>
public sealed record OcrLine(string Text, int Y);
