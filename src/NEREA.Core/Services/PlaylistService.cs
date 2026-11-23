using NEREA.Core.Data;
using NEREA.Core.Models;

namespace NEREA.Core.Services;

/// <summary>Playlist CRUD on top of the library database.</summary>
public sealed class PlaylistService
{
    private readonly LibraryDatabase _db;

    public PlaylistService(LibraryDatabase db) => _db = db;

    public event EventHandler? PlaylistsChanged;

    public List<Playlist> GetPlaylists() => _db.GetPlaylists();

    public Playlist Create(string name)
    {
        var p = new Playlist { Name = name };
        _db.SavePlaylist(p);
        PlaylistsChanged?.Invoke(this, EventArgs.Empty);
        return p;
    }

    public void Rename(string id, string newName)
    {
        var p = _db.GetPlaylists().Find(x => x.Id == id);
        if (p is null) return;
        p.Name = newName;
        _db.SavePlaylist(p);
        PlaylistsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Delete(string id)
    {
        _db.DeletePlaylist(id);
        PlaylistsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Duplicate(string id)
    {
        var p = _db.GetPlaylists().Find(x => x.Id == id);
        if (p is null) return;
        var copy = new Playlist { Name = p.Name + " copy", TrackIds = new List<string>(p.TrackIds) };
        _db.SavePlaylist(copy);
        PlaylistsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void AddTrack(string playlistId, string trackId)
    {
        var p = _db.GetPlaylists().Find(x => x.Id == playlistId);
        if (p is null) return;
        if (!p.TrackIds.Contains(trackId)) p.TrackIds.Add(trackId);
        _db.SavePlaylist(p);
        PlaylistsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void RemoveTrack(string playlistId, string trackId)
    {
        var p = _db.GetPlaylists().Find(x => x.Id == playlistId);
        if (p is null) return;
        p.TrackIds.Remove(trackId);
        _db.SavePlaylist(p);
        PlaylistsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetTracks(string playlistId, IEnumerable<string> trackIds)
    {
        var p = _db.GetPlaylists().Find(x => x.Id == playlistId);
        if (p is null) return;
        p.TrackIds = trackIds.ToList();
        _db.SavePlaylist(p);
        PlaylistsChanged?.Invoke(this, EventArgs.Empty);
    }
}
