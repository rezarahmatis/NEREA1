using NEREA.Core.Data;
using NEREA.Core.Models;

namespace NEREA.Core.Services;

public sealed record ImportProgress(int Processed, int Total, string CurrentFile);

/// <summary>
/// The music library: importing files/folders, scanning, searching and views.
/// Import runs asynchronously and never blocks the UI; an invalid file is
/// skipped with a warning rather than failing the batch.
/// </summary>
public sealed class MusicLibraryService
{
    public static readonly string[] SupportedExtensions =
        { ".mp3", ".wav", ".flac", ".ogg", ".oga", ".m4a", ".aac", ".wma" };

    private readonly LibraryDatabase _db;
    private readonly TagMetadataReader _metadata;
    private readonly object _sync = new();

    public MusicLibraryService(LibraryDatabase db, TagMetadataReader metadata)
    {
        _db = db;
        _metadata = metadata;
    }

    /// <summary>Raised whenever the library content changes (import, favorite, stats).</summary>
    public event EventHandler? LibraryChanged;

    public List<Track> GetAllTracks()
    {
        lock (_sync) return _db.GetAllTracks();
    }

    public Track? GetTrack(string id)
    {
        lock (_sync) return _db.GetTrack(id);
    }

    public bool IsSupported(string path) =>
        SupportedExtensions.Contains(Path.GetExtension(path).ToLowerInvariant());

    /// <summary>Imports individual files and/or whole folders, recursively.</summary>
    public Task ImportAsync(IEnumerable<string> paths, IProgress<ImportProgress>? progress, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var files = Expand(paths);
            var existing = new HashSet<string>(
                GetAllTracks().Select(t => Path.GetFullPath(t.Path).ToLowerInvariant()),
                StringComparer.OrdinalIgnoreCase);

            int total = files.Count, done = 0;
            foreach (var file in files)
            {
                ct.ThrowIfCancellationRequested();
                progress?.Report(new ImportProgress(done, total, Path.GetFileName(file)));
                try
                {
                    if (existing.Contains(Path.GetFullPath(file).ToLowerInvariant()))
                    {
                        done++;
                        continue;
                    }
                    var meta = _metadata.Read(file);
                    var fi = new FileInfo(file);
                    var track = new Track
                    {
                        Id = LibraryDatabase.TrackIdForPath(file),
                        Path = Path.GetFullPath(file),
                        Title = meta.Title,
                        Artist = meta.Artist,
                        Album = meta.Album,
                        AlbumArtist = meta.AlbumArtist,
                        Genre = meta.Genre,
                        TrackNumber = meta.TrackNumber,
                        Year = meta.Year,
                        Duration = meta.Duration,
                        FileSize = fi.Exists ? fi.Length : 0,
                        ArtworkPath = meta.ArtworkPath,
                        ModifiedUtc = fi.Exists ? fi.LastWriteTimeUtc : DateTime.UtcNow
                    };
                    _db.UpsertTrack(track);
                }
                catch
                {
                    // A single unreadable/corrupt file is skipped, never fatal.
                }
                done++;
            }
            LibraryChanged?.Invoke(this, EventArgs.Empty);
        }, ct);
    }

    private List<string> Expand(IEnumerable<string> paths)
    {
        var result = new List<string>();
        foreach (var p in paths)
        {
            if (Directory.Exists(p))
            {
                _db.AddFolderRoot(Path.GetFullPath(p));
                try
                {
                    var files = Directory.EnumerateFiles(p, "*", SearchOption.AllDirectories)
                        .Where(IsSupported)
                        .OrderBy(f => f, StringComparer.OrdinalIgnoreCase);
                    result.AddRange(files);
                }
                catch
                {
                    // Unreadable subtree: skip silently, keep importing the rest.
                }
            }
            else if (File.Exists(p) && IsSupported(p))
            {
                result.Add(p);
            }
        }
        return result;
    }

    public List<string> GetFolderRoots() => _db.GetFolderRoots();

    public void RescanFolders(IProgress<ImportProgress>? progress, CancellationToken ct = default)
    {
        var roots = GetFolderRoots();
        if (roots.Count > 0) ImportAsync(roots, progress, ct).GetAwaiter().GetResult();
    }

    public List<Track> Search(string query)
    {
        var all = GetAllTracks();
        if (string.IsNullOrWhiteSpace(query)) return all;
        var q = query.Trim();
        return all.Where(t =>
                t.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                t.Artist.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                t.Album.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                t.Genre.Contains(q, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public void ToggleFavorite(Track track)
    {
        track.Favorite = !track.Favorite;
        _db.SetFavorite(track.Id, track.Favorite);
        LibraryChanged?.Invoke(this, EventArgs.Empty);
    }

    public void RecordPlayback(Track track)
    {
        track.PlayCount++;
        track.LastPlayedUtc = DateTime.UtcNow;
        _db.RecordPlayback(track.Id);
    }

    public void RemoveTrack(Track track)
    {
        _db.DeleteTrack(track.Id);
        LibraryChanged?.Invoke(this, EventArgs.Empty);
    }

    // ── Views ────────────────────────────────────────────────────────────────

    public List<IGrouping<string, Track>> Albums(IEnumerable<Track>? source = null) =>
        (source ?? GetAllTracks())
            .GroupBy(t => string.IsNullOrWhiteSpace(t.Album) ? "Unknown album" : t.Album)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public List<IGrouping<string, Track>> Artists(IEnumerable<Track>? source = null) =>
        (source ?? GetAllTracks())
            .GroupBy(t => string.IsNullOrWhiteSpace(t.Artist) ? "Unknown artist" : t.Artist)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public List<IGrouping<string, Track>> Folders(IEnumerable<Track>? source = null) =>
        (source ?? GetAllTracks())
            .GroupBy(t => Path.GetDirectoryName(t.Path) ?? "")
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public List<Track> Favorites(IEnumerable<Track>? source = null) =>
        (source ?? GetAllTracks()).Where(t => t.Favorite).ToList();

    public List<Track> RecentlyPlayed(IEnumerable<Track>? source = null) =>
        (source ?? GetAllTracks())
            .Where(t => t.LastPlayedUtc is not null)
            .OrderByDescending(t => t.LastPlayedUtc)
            .ToList();
}
