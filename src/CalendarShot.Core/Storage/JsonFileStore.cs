using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using CalendarShot.Core.Diagnostics;

namespace CalendarShot.Core.Storage;

/// <summary>
/// Loads and saves a single JSON document with size limits, atomic writes and
/// quarantine of unreadable files.
/// </summary>
public sealed class JsonFileStore<T>
    where T : class
{
    public const long DefaultMaxBytes = 1024 * 1024;

    private readonly string _path;
    private readonly IAppLog _log;
    private readonly long _maxBytes;

    public JsonFileStore(string path, IAppLog log, long maxBytes = DefaultMaxBytes)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(log);

        _path = path;
        _log = log;
        _maxBytes = maxBytes;
    }

    public string FilePath => _path;

    public bool Exists => File.Exists(_path);

    /// <summary>
    /// Returns the stored value, or <see langword="null"/> when the file does not
    /// exist or could not be read. Unreadable files are renamed so they are not
    /// silently overwritten and can be inspected later.
    /// </summary>
    public T? Load()
    {
        if (!File.Exists(_path))
        {
            return null;
        }

        try
        {
            string json = AtomicFile.ReadAllText(_path, _maxBytes);
            return JsonSerializer.Deserialize<T>(json, StorageJson.Options);
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException or NotSupportedException)
        {
            Quarantine(ex);
            return null;
        }
    }

    public void Save(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        AtomicFile.WriteAllText(_path, JsonSerializer.Serialize(value, StorageJson.Options));
    }

    private void Quarantine(Exception reason)
    {
        string suffix = DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        string target = $"{_path}.corrupt-{suffix}";
        try
        {
            File.Move(_path, target, overwrite: true);
            _log.Warn($"Unreadable file '{Path.GetFileName(_path)}' was moved to '{Path.GetFileName(target)}'.");
        }
        catch (IOException ex)
        {
            _log.Error($"Could not quarantine unreadable file '{Path.GetFileName(_path)}'.", ex);
        }

        _log.Error($"Failed to read '{Path.GetFileName(_path)}'.", reason);
    }
}

/// <summary>
/// Shared serializer settings. No polymorphic type handling is enabled, so
/// files cannot instruct the deserializer to create arbitrary types.
/// </summary>
public static class StorageJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        MaxDepth = 16,
        Converters = { new JsonStringEnumConverter() },
    };
}
