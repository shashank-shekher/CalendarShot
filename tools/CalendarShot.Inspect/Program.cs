using System.Globalization;
using System.Text.Json;
using CalendarShot.Core.Calendar;
using CalendarShot.Core.Parsing;
using CalendarShot.Core.Recognition;
using CalendarShot.Ocr;

namespace CalendarShot.Inspect;

/// <summary>
/// Usage:
///   CalendarShot.Inspect image.png [--today yyyy-MM-dd] [--lang tag] [--show-ocr] [--save-ocr page.json]
///   CalendarShot.Inspect --load-ocr page.json [--today yyyy-MM-dd]
///
/// Saved OCR output contains only text and positions, which makes it a safer
/// way to share a problem layout than the screenshot itself (remove any
/// private event titles before sharing).
/// </summary>
internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    [STAThread]
    private static async Task<int> Main(string[] args)
    {
        if (args.Length == 0 || args.Contains("--help") || args.Contains("-h"))
        {
            Console.WriteLine("CalendarShot.Inspect <image> [--today yyyy-MM-dd] [--lang tag] [--show-ocr] [--save-ocr file.json]");
            Console.WriteLine("CalendarShot.Inspect --load-ocr file.json [--today yyyy-MM-dd]");
            return args.Length == 0 ? 1 : 0;
        }

        DateOnly today = Option(args, "--today") is string t
            ? DateOnly.ParseExact(t, "yyyy-MM-dd", CultureInfo.InvariantCulture)
            : DateOnly.FromDateTime(DateTime.Now);

        OcrPage page;
        if (Option(args, "--load-ocr") is string ocrFile)
        {
            page = JsonSerializer.Deserialize<OcrPageDto>(await File.ReadAllTextAsync(ocrFile), JsonOptions)!.ToPage();
        }
        else
        {
            using var image = new Bitmap(args[0]);
            page = await WindowsOcrService.RecognizeAsync(image, Option(args, "--lang"), CancellationToken.None);
        }

        if (Option(args, "--save-ocr") is string saveFile)
        {
            await File.WriteAllTextAsync(saveFile, JsonSerializer.Serialize(OcrPageDto.From(page), JsonOptions));
            Console.WriteLine($"OCR output saved to {saveFile}");
        }

        if (args.Contains("--show-ocr"))
        {
            Console.WriteLine($"OCR: {page.Lines.Count} line(s), page {page.Width}x{page.Height}");
            foreach (OcrLine line in page.Lines)
            {
                Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"  [{line.Bounds.X,6:0},{line.Bounds.Y,6:0} {line.Bounds.Width,5:0}x{line.Bounds.Height,3:0}] {line.Text}"));
            }

            Console.WriteLine();
        }

        var parser = new CalendarScreenshotParser(DateVocabulary.Create(CultureInfo.CurrentCulture));
        ParseOutcome outcome = parser.Parse(page, today);
        if (!outcome.Success)
        {
            Console.WriteLine($"Not recognised: {outcome.Error}");
            return 2;
        }

        RecognitionResult result = outcome.Result!;
        Console.WriteLine($"Layout:  {result.Layout}");
        Console.WriteLine($"Covers:  {result.CoveredFrom:yyyy-MM-dd} to {result.CoveredTo:yyyy-MM-dd}");
        Console.WriteLine($"Events:  {result.Events.Count}");
        foreach (CalendarEvent e in result.Events)
        {
            string time = e.Start is null ? "all day    " : $"{e.Start:HH:mm}-{(e.End is null ? "     " : e.End.Value.ToString("HH:mm", CultureInfo.InvariantCulture))}";
            Console.WriteLine($"  {e.Date:yyyy-MM-dd} {time}  {e.Title}{(e.Notes is null ? string.Empty : $"  ({e.Notes})")}");
        }

        foreach (string warning in result.Warnings)
        {
            Console.WriteLine($"Warning: {warning}");
        }

        return 0;
    }

    private static string? Option(string[] args, string name)
    {
        int index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    private sealed record WordDto(string Text, double X, double Y, double Width, double Height);

    private sealed record OcrPageDto(double Width, double Height, List<List<WordDto>> Lines)
    {
        public static OcrPageDto From(OcrPage page) => new(
            page.Width,
            page.Height,
            page.Lines.Select(l => l.Words.Select(w => new WordDto(w.Text, w.Bounds.X, w.Bounds.Y, w.Bounds.Width, w.Bounds.Height)).ToList()).ToList());

        public OcrPage ToPage() => new(
            Width,
            Height,
            Lines.Select(l => new OcrLine(l.Select(w => new OcrWord(w.Text, new TextBounds(w.X, w.Y, w.Width, w.Height))).ToList())).ToList());
    }
}
