using System.Text.Json;
using System.Text.Json.Serialization;
using NEREA.Core.Models;

namespace NEREA.Core.Data;

/// <summary>Persists <see cref="AppSettings"/> as JSON in the local app-data folder.</summary>
public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _path;
    private readonly object _sync = new();
    private AppSettings _cache;

    public SettingsStore(string settingsPath)
    {
        _path = settingsPath;
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        _cache = Load();
    }

    public AppSettings Settings => _cache;

    private AppSettings Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                var json = File.ReadAllText(_path);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json, Options);
                if (loaded is not null) return loaded;
            }
        }
        catch
        {
            // Corrupt settings fall back to defaults rather than blocking startup.
        }
        return new AppSettings();
    }

    /// <summary>Applies a mutation and persists asynchronously (fire and forget, thread safe).</summary>
    public void Update(Action<AppSettings> mutate)
    {
        AppSettings snapshot;
        lock (_sync)
        {
            mutate(_cache);
            snapshot = _cache;
        }
        Save(snapshot);
    }

    private void Save(AppSettings s)
    {
        try
        {
            var json = JsonSerializer.Serialize(s, Options);
            File.WriteAllText(_path, json);
        }
        catch
        {
            // Persistence failures must never take the app down.
        }
    }
}
