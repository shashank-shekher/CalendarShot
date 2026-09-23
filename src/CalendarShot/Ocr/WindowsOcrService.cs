using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices.WindowsRuntime;
using CalendarShot.Core.Recognition;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using CoreOcrLine = CalendarShot.Core.Recognition.OcrLine;
using CoreOcrWord = CalendarShot.Core.Recognition.OcrWord;
using WinOcrResult = Windows.Media.Ocr.OcrResult;

namespace CalendarShot.Ocr;

/// <summary>
/// Text recognition using the OCR engine built into Windows. Recognition runs
/// entirely on the local machine; no image data leaves the computer.
/// </summary>
/// <remarks>
/// The image is read twice at different scales and the results are merged,
/// because each scale occasionally drops or garbles words the other reads
/// correctly (see <see cref="OcrPassMerger"/>).
/// </remarks>
internal static class WindowsOcrService
{
    /// <summary>Screens up to this width are upscaled, which noticeably improves accuracy on small text.</summary>
    private const int UpscaleWidthLimit = 2560;

    /// <summary>
    /// Median word height, in screen pixels, below which text counts as very small.
    /// Typical 9-point UI text measures about 10 pixels; some applications use less.
    /// </summary>
    private const double SmallTextHeight = 8.5;

    /// <summary>Text height, in pixels of the scaled image, at which Windows OCR reads most reliably.</summary>
    private const double TargetTextHeight = 32;

    private const double MaxScale = 4.5;

    public static IReadOnlyList<(string Tag, string DisplayName)> AvailableLanguages() =>
        OcrEngine.AvailableRecognizerLanguages
            .Select(l => (l.LanguageTag, l.DisplayName))
            .ToList();

    public static async Task<OcrPage> RecognizeAsync(Bitmap image, string? languageTag, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(image);

        OcrEngine engine = CreateEngine(languageTag);
        double limit = (double)OcrEngine.MaxImageDimension / Math.Max(image.Width, image.Height);

        (double primaryScale, double secondaryScale) = image.Width <= UpscaleWidthLimit ? (2.0, 1.0) : (1.0, 1.5);
        primaryScale = Math.Min(primaryScale, limit);
        secondaryScale = Math.Min(secondaryScale, limit);

        OcrPage primary = await RecognizePassAsync(engine, image, primaryScale, cancellationToken).ConfigureAwait(false);

        // Some applications draw their calendar in very small type. When the
        // first pass shows that, read again at a scale that brings the text to a
        // comfortable height and treat that pass as the more reliable one.
        double textHeight = MedianWordHeight(primary);
        if (textHeight > 0 && textHeight < SmallTextHeight)
        {
            double wanted = Math.Min(Math.Min(TargetTextHeight / textHeight, MaxScale), limit);
            if (wanted > primaryScale * 1.2)
            {
                // Recognition of very small text is unstable: each scale misses
                // different lines, so an intermediate scale is read as well.
                OcrPage large = await RecognizePassAsync(engine, image, wanted, cancellationToken).ConfigureAwait(false);
                OcrPage middle = await RecognizePassAsync(engine, image, (wanted + primaryScale) / 2, cancellationToken).ConfigureAwait(false);
                return OcrPassMerger.Merge(OcrPassMerger.Merge(large, middle), primary);
            }
        }

        if (Math.Abs(primaryScale - secondaryScale) < 0.1)
        {
            return primary;
        }

        OcrPage secondary = await RecognizePassAsync(engine, image, secondaryScale, cancellationToken).ConfigureAwait(false);
        return OcrPassMerger.Merge(primary, secondary);
    }

    private static double MedianWordHeight(OcrPage page)
    {
        double[] heights = page.Lines.SelectMany(l => l.Words).Select(w => w.Bounds.Height).Order().ToArray();
        return heights.Length == 0 ? 0 : heights[heights.Length / 2];
    }

    private static async Task<OcrPage> RecognizePassAsync(OcrEngine engine, Bitmap image, double scale, CancellationToken cancellationToken)
    {
        using Bitmap prepared = Resize(image, scale);
        using SoftwareBitmap softwareBitmap = ToSoftwareBitmap(prepared);

        WinOcrResult result = await engine.RecognizeAsync(softwareBitmap).AsTask(cancellationToken).ConfigureAwait(false);

        var lines = new List<CoreOcrLine>(result.Lines.Count);
        foreach (Windows.Media.Ocr.OcrLine line in result.Lines)
        {
            var words = line.Words
                .Select(w => new CoreOcrWord(
                    w.Text,
                    new TextBounds(w.BoundingRect.X / scale, w.BoundingRect.Y / scale, w.BoundingRect.Width / scale, w.BoundingRect.Height / scale)))
                .ToList();

            if (words.Count > 0)
            {
                lines.Add(new CoreOcrLine(words));
            }
        }

        return new OcrPage(image.Width, image.Height, lines);
    }

    private static OcrEngine CreateEngine(string? languageTag)
    {
        OcrEngine? engine = null;
        if (!string.IsNullOrWhiteSpace(languageTag))
        {
            var language = new Language(languageTag);
            if (OcrEngine.IsLanguageSupported(language))
            {
                engine = OcrEngine.TryCreateFromLanguage(language);
            }
        }

        engine ??= OcrEngine.TryCreateFromUserProfileLanguages();

        return engine ?? throw new InvalidOperationException(
            "Windows text recognition is not available. Install an OCR-capable language in " +
            "Settings > Time & language > Language & region, then try again.");
    }

    private static Bitmap Resize(Bitmap source, double scale)
    {
        int width = Math.Max(1, (int)Math.Round(source.Width * scale));
        int height = Math.Max(1, (int)Math.Round(source.Height * scale));

        var target = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using Graphics graphics = Graphics.FromImage(target);
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.SmoothingMode = SmoothingMode.HighQuality;
        graphics.DrawImage(source, 0, 0, width, height);
        return target;
    }

    private static SoftwareBitmap ToSoftwareBitmap(Bitmap bitmap)
    {
        var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        BitmapData data = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            int rowBytes = bitmap.Width * 4;
            byte[] pixels = new byte[rowBytes * bitmap.Height];
            for (int y = 0; y < bitmap.Height; y++)
            {
                System.Runtime.InteropServices.Marshal.Copy(data.Scan0 + (y * data.Stride), pixels, y * rowBytes, rowBytes);
            }

            return SoftwareBitmap.CreateCopyFromBuffer(
                pixels.AsBuffer(), BitmapPixelFormat.Bgra8, bitmap.Width, bitmap.Height, BitmapAlphaMode.Premultiplied);
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }
}
