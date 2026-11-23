namespace NEREA.Core.Models;

/// <summary>A music track imported into the NEREA library.</summary>
public sealed class Track
{
    /// <summary>Stable id derived from the normalized file path.</summary>
    public string Id { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string Album { get; set; } = string.Empty;
    public string AlbumArtist { get; set; } = string.Empty;
    public string Genre { get; set; } = string.Empty;
    public int TrackNumber { get; set; }
    public int Year { get; set; }
    public TimeSpan Duration { get; set; }
    public long FileSize { get; set; }
    /// <summary>Cached artwork image path (may be empty).</summary>
    public string ArtworkPath { get; set; } = string.Empty;
    public bool Favorite { get; set; }
    public int PlayCount { get; set; }
    public DateTime? LastPlayedUtc { get; set; }
    public DateTime AddedUtc { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedUtc { get; set; }

    public string DisplayArtist =>
        string.IsNullOrWhiteSpace(Artist) ? "Unknown artist" : Artist;
    public string DisplayAlbum =>
        string.IsNullOrWhiteSpace(Album) ? "Unknown album" : Album;
}

public enum RepeatMode
{
    Off,
    All,
    One
}

/// <summary>An output audio endpoint as reported by Windows.</summary>
public sealed record AudioDeviceInfo(string Id, string Name, bool IsDefault);
