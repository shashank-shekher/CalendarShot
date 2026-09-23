using System.Text;

namespace CalendarShot.Core.Storage;

/// <summary>
/// File helpers that never leave a half-written file behind.
/// </summary>
public static class AtomicFile
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    /// Writes the content to a temporary file in the same directory and then
    /// moves it over the destination in a single rename.
    /// </summary>
    public static void WriteAllText(string path, string content)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(content);

        string fullPath = Path.GetFullPath(path);
        string directory = Path.GetDirectoryName(fullPath)
            ?? throw new ArgumentException("The path has no parent directory.", nameof(path));
        Directory.CreateDirectory(directory);

        string tempPath = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream, Utf8NoBom))
            {
                writer.Write(content);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }

            File.Move(tempPath, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    /// <summary>
    /// Reads a text file, refusing files larger than <paramref name="maxBytes"/>.
    /// </summary>
    public static string ReadAllText(string path, long maxBytes)
    {
        var info = new FileInfo(path);
        if (info.Length > maxBytes)
        {
            throw new InvalidDataException(
                $"'{info.Name}' is {info.Length} bytes, which exceeds the {maxBytes} byte limit.");
        }

        return File.ReadAllText(path, Encoding.UTF8);
    }
}
