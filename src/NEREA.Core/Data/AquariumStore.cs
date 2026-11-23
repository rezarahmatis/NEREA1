using System.Text.Json;
using System.Text.Json.Serialization;
using NEREA.Core.Models;

namespace NEREA.Core.Data;

/// <summary>Persists user-created aquarium presets as JSON.</summary>
public sealed class AquariumStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _path;
    private readonly object _sync = new();
    private List<AquariumPreset> _presets;

    public AquariumStore(string path)
    {
        _path = path;
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        _presets = Load();
    }

    public IReadOnlyList<AquariumPreset> Presets
    {
        get { lock (_sync) return _presets.ToList(); }
    }

    private List<AquariumPreset> Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                var loaded = JsonSerializer.Deserialize<List<AquariumPreset>>(File.ReadAllText(_path), Options);
                if (loaded is not null) return loaded;
            }
        }
        catch
        {
            // Corrupt file: start fresh, never crash the app.
        }
        return new List<AquariumPreset>();
    }

    private void Save()
    {
        try
        {
            File.WriteAllText(_path, JsonSerializer.Serialize(_presets, Options));
        }
        catch
        {
            // Ignore persistence failures.
        }
    }

    public void Save(AquariumPreset preset)
    {
        lock (_sync)
        {
            var existing = _presets.FindIndex(p => p.Id == preset.Id);
            if (existing >= 0) _presets[existing] = preset; else _presets.Add(preset);
            Save();
        }
    }

    public void Delete(string id)
    {
        lock (_sync)
        {
            _presets.RemoveAll(p => p.Id == id);
            Save();
        }
    }

    public AquariumPreset? Find(string id)
    {
        lock (_sync)
        {
            return _presets.Find(p => p.Id == id);
        }
    }
}
