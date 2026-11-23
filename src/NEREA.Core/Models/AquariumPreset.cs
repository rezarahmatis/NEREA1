namespace NEREA.Core.Models;

/// <summary>A user playlist: an ordered list of track ids.</summary>
public sealed class Playlist
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "New playlist";
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public List<string> TrackIds { get; set; } = new();
}

/// <summary>A saved custom aquarium: world + species population + optional music link + atmosphere.</summary>
public sealed class AquariumPreset
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "My aquarium";
    public string WorldId { get; set; } = "moonlit-pond";
    public List<SpeciesCount> Species { get; set; } = new();
    /// <summary>Optional playlist that this aquarium is associated with.</summary>
    public string? PlaylistId { get; set; }
    public Atmosphere Atmosphere { get; set; } = Atmosphere.Balanced;

    public AquariumPreset Clone()
    {
        var c = (AquariumPreset)MemberwiseClone();
        c.Id = Guid.NewGuid().ToString("N");
        c.Name = Name + " copy";
        c.Species = Species.Select(s => new SpeciesCount { SpeciesId = s.SpeciesId, Count = s.Count, Enabled = s.Enabled }).ToList();
        return c;
    }
}

public sealed class SpeciesCount
{
    public string SpeciesId { get; set; } = string.Empty;
    public int Count { get; set; } = 5;
    public bool Enabled { get; set; } = true;
}

public enum Atmosphere
{
    Calm,
    Balanced,
    Vivid
}
