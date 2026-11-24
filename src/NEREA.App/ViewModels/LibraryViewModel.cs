using CommunityToolkit.Mvvm.ComponentModel;
using NEREA.Audio;
using CommunityToolkit.Mvvm.Input;
using NEREA.Core.Data;
using NEREA.Core.Models;
using NEREA.Core.Services;
using System.Collections.ObjectModel;

namespace NEREA.App.ViewModels;

/// <summary>A grouping row for the Albums / Artists / Folders views.</summary>
public sealed class GroupRow
{
    public string Name { get; init; } = "";
    public string Subtitle { get; init; } = "";
    public string ArtworkPath { get; init; } = "";
    public IReadOnlyList<Track> Tracks { get; init; } = Array.Empty<Track>();
}

/// <summary>
/// The music library view model: songs / albums / artists / playlists /
/// favorites / recent / folders views with search and sorting.
/// </summary>
public partial class LibraryViewModel : ObservableObject
{
    private readonly MusicLibraryService _library;
    private readonly PlaylistService _playlists;
    private readonly PlaybackController _playback;
    private MainViewModel? _main;

    private List<Track> _allTracks = new();
    private List<Playlist> _allPlaylists = new();

    public const int ViewSongs = 0, ViewAlbums = 1, ViewArtists = 2, ViewPlaylists = 3,
                     ViewFavorites = 4, ViewRecent = 5, ViewFolders = 6, ViewQueue = 7;

    [ObservableProperty] private ObservableCollection<Track> _songs = new();
    [ObservableProperty] private ObservableCollection<GroupRow> _groups = new();
    [ObservableProperty] private ObservableCollection<Track> _playlistSongs = new();
    [ObservableProperty] private ObservableCollection<Track> _queue = new();
    [ObservableProperty] private ObservableCollection<Playlist> _playlistList = new();
    [ObservableProperty] private int _selectedView;
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private int _sortIndex;
    [ObservableProperty] private bool _showGroupTracks;   // album/artist/folder/playlist drill-in
    [ObservableProperty] private string _groupTracksTitle = "";
    [ObservableProperty] private bool _hasLibrary;

    // View visibility state (drives the panel's stacked lists).
    [ObservableProperty] private bool _songsVisible = true;
    [ObservableProperty] private bool _groupsVisible;
    [ObservableProperty] private bool _playlistsVisible;
    [ObservableProperty] private bool _queueVisible;

    private List<Track> _groupTracks = new();
    private Playlist? _openPlaylist;

    public LibraryViewModel(MusicLibraryService library, PlaylistService playlists,
        PlaybackController playback)
    {
        _library = library;
        _playlists = playlists;
        _playback = playback;

        _playback.QueueChanged += (_, _) => RefreshQueue();
        _sync = System.Threading.SynchronizationContext.Current;
        _library.LibraryChanged += (_, _) => _sync?.Post(_ => RefreshAll(), null);
    }

    private readonly System.Threading.SynchronizationContext? _sync;

    /// <summary>
    /// Attached by MainViewModel after construction. MainViewModel depends on
    /// this view model, so taking it in the constructor created a circular
    /// dependency that threw while building services and crashed the app at
    /// startup.
    /// </summary>
    public void AttachMain(MainViewModel main) => _main = main;

    public void RefreshAll()
    {
        _allTracks = _library.GetAllTracks();
        _allPlaylists = _playlists.GetPlaylists();
        HasLibrary = _allTracks.Count > 0;
        RebuildView();
        RefreshQueue();
    }

    public void RefreshQueue()
    {
        Queue = new ObservableCollection<Track>(_playback.Queue);
    }

    public Track? FindTrackByPath(string path) =>
        _allTracks.FirstOrDefault(t => string.Equals(t.Path, path, StringComparison.OrdinalIgnoreCase))
        ?? _library.GetAllTracks().FirstOrDefault(t => string.Equals(t.Path, path, StringComparison.OrdinalIgnoreCase));

    partial void OnSelectedViewChanged(int value) => RebuildView();
    partial void OnSearchTextChanged(string value) => RebuildView();
    partial void OnSortIndexChanged(int value) => RebuildView();

    private IEnumerable<Track> QueryTracks()
    {
        IEnumerable<Track> source = _allTracks;
        if (SelectedView == ViewFavorites) source = _library.Favorites(source);
        if (SelectedView == ViewRecent) source = _library.RecentlyPlayed(source);
        var q = SearchText;
        if (!string.IsNullOrWhiteSpace(q))
            source = source.Where(t =>
                t.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                t.Artist.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                t.Album.Contains(q, StringComparison.OrdinalIgnoreCase));
        return SortTracks(source);
    }

    private IEnumerable<Track> SortTracks(IEnumerable<Track> source) => SortIndex switch
    {
        1 => source.OrderBy(t => t.AddedUtc),                        // Recently added
        2 => source.OrderByDescending(t => t.Duration),             // Duration
        3 => source.OrderBy(t => t.DisplayArtist, StringComparer.OrdinalIgnoreCase)
                  .ThenBy(t => t.Title, StringComparer.OrdinalIgnoreCase), // Artist
        _ => source.OrderBy(t => t.Title, StringComparer.OrdinalIgnoreCase)
    };

    private void RebuildView()
    {
        ShowGroupTracks = false;
        bool songs = SelectedView is ViewSongs or ViewFavorites or ViewRecent;
        bool groups = SelectedView is ViewAlbums or ViewArtists or ViewFolders;
        SongsVisible = songs;
        GroupsVisible = groups;
        PlaylistsVisible = SelectedView == ViewPlaylists;
        QueueVisible = SelectedView == ViewQueue;
        switch (SelectedView)
        {
            case ViewSongs:
            case ViewFavorites:
            case ViewRecent:
                Songs = new ObservableCollection<Track>(QueryTracks());
                Groups = new ObservableCollection<GroupRow>();
                PlaylistSongs = new ObservableCollection<Track>();
                break;

            case ViewAlbums:
                Groups = new ObservableCollection<GroupRow>(_library.Albums(QueryTracks()).Select(g => new GroupRow
                {
                    Name = g.Key,
                    Subtitle = $"{g.First().DisplayArtist} · {g.Count()} songs",
                    ArtworkPath = g.FirstOrDefault(t => t.ArtworkPath.Length > 0)?.ArtworkPath ?? "",
                    Tracks = g.OrderBy(t => t.TrackNumber).ToList()
                }));
                Songs = new ObservableCollection<Track>();
                break;

            case ViewArtists:
                Groups = new ObservableCollection<GroupRow>(_library.Artists(QueryTracks()).Select(g => new GroupRow
                {
                    Name = g.Key,
                    Subtitle = $"{g.Count()} songs · {g.Select(x => x.Album).Distinct().Count()} albums",
                    ArtworkPath = g.FirstOrDefault(t => t.ArtworkPath.Length > 0)?.ArtworkPath ?? "",
                    Tracks = g.OrderBy(t => t.Album).ThenBy(t => t.TrackNumber).ToList()
                }));
                Songs = new ObservableCollection<Track>();
                break;

            case ViewFolders:
                Groups = new ObservableCollection<GroupRow>(_library.Folders(QueryTracks()).Select(g => new GroupRow
                {
                    Name = string.IsNullOrEmpty(g.Key) ? "(root)" : g.Key,
                    Subtitle = $"{g.Count()} songs",
                    ArtworkPath = "",
                    Tracks = g.OrderBy(t => t.Title).ToList()
                }));
                Songs = new ObservableCollection<Track>();
                break;

            case ViewPlaylists:
                PlaylistList = new ObservableCollection<Playlist>(_allPlaylists);
                Songs = new ObservableCollection<Track>();
                Groups = new ObservableCollection<GroupRow>();
                break;

            case ViewQueue:
                RefreshQueue();
                Songs = new ObservableCollection<Track>();
                break;
        }
    }

    // ── Track commands ───────────────────────────────────────────────────────

    [RelayCommand]
    public void PlayTrack(Track? track)
    {
        if (track is null) return;
        if (SelectedView == ViewQueue)
        {
            int idx = _playback.Queue.ToList().FindIndex(t => t.Id == track.Id);
            if (idx >= 0) _playback.JumpTo(idx);
            return;
        }
        PlayList(QueryTracks().ToList(), track);
    }

    [RelayCommand]
    public void PlayGroup(GroupRow? group)
    {
        if (group is null) return;
        _groupTracks = group.Tracks.ToList();
        _playback.PlayTracks(_groupTracks, 0);
    }

    [RelayCommand]
    public void OpenGroup(GroupRow? group)
    {
        if (group is null) return;
        _groupTracks = group.Tracks.ToList();
        GroupTracksTitle = group.Name;
        PlaylistSongs = new ObservableCollection<Track>(_groupTracks);
        ShowGroupTracks = true;
    }

    [RelayCommand]
    public void OpenPlaylist(Playlist? playlist)
    {
        if (playlist is null) return;
        _openPlaylist = playlist;
        var tracks = playlist.TrackIds
            .Select(id => _allTracks.FirstOrDefault(t => t.Id == id))
            .Where(t => t is not null).Select(t => t!).ToList();
        _groupTracks = tracks;
        GroupTracksTitle = playlist.Name;
        PlaylistSongs = new ObservableCollection<Track>(tracks);
        ShowGroupTracks = true;
    }

    public void PlayList(List<Track> tracks, Track? first)
    {
        if (tracks.Count == 0) return;
        int index = first is not null ? Math.Max(0, tracks.FindIndex(t => t.Id == first.Id)) : 0;
        _playback.PlayTracks(tracks, index);
    }

    [RelayCommand]
    public void QueueTrack(Track? track)
    {
        if (track is not null) _playback.Enqueue(track);
    }

    [RelayCommand]
    public void QueueNext(Track? track)
    {
        if (track is not null) _playback.EnqueueNext(track);
    }

    [RelayCommand]
    public void RemoveFromQueue(Track? track)
    {
        if (track is null) return;
        int idx = _playback.Queue.ToList().FindIndex(t => t.Id == track.Id);
        if (idx >= 0) _playback.RemoveFromQueue(idx);
    }

    [RelayCommand]
    public void ToggleFavorite(Track? track)
    {
        if (track is not null) _library.ToggleFavorite(track);
    }

    [RelayCommand]
    public void RemoveFromLibrary(Track? track)
    {
        if (track is not null) _library.RemoveTrack(track);
    }

    public void AddTrackToPlaylist(string playlistId, Track track) =>
        _playlists.AddTrack(playlistId, track.Id);

    public IReadOnlyList<Playlist> PlaylistsForMenu => _allPlaylists;

    // ── Playlist commands ────────────────────────────────────────────────────

    [RelayCommand]
    public void CreatePlaylist(string? name)
    {
        var finalName = string.IsNullOrWhiteSpace(name) ? $"Playlist {_allPlaylists.Count + 1}" : name!.Trim();
        _playlists.Create(finalName);
        _allPlaylists = _playlists.GetPlaylists();
        PlaylistList = new ObservableCollection<Playlist>(_allPlaylists);
    }

    public void RenamePlaylistTo(string id, string newName)
    {
        _playlists.Rename(id, newName);
        RefreshAll();
    }

    [RelayCommand]
    public void DeletePlaylist(Playlist? playlist)
    {
        if (playlist is null) return;
        _playlists.Delete(playlist.Id);
        RefreshAll();
    }

    [RelayCommand]
    public void DuplicatePlaylist(Playlist? playlist)
    {
        if (playlist is not null) _playlists.Duplicate(playlist.Id);
        RefreshAll();
    }

    [RelayCommand]
    private void RemoveTrackFromOpenPlaylist(Track? track)
    {
        if (_openPlaylist is null || track is null) return;
        _playlists.RemoveTrack(_openPlaylist.Id, track.Id);
        OpenPlaylist(_openPlaylist);
    }

    /// <summary>Closes every overlay panel; safe entry point for shell views.</summary>
    public void ClosePanelsPublic() => _main?.ClosePanelsPublic();

    // ── Import ───────────────────────────────────────────────────────────────

    public async Task ImportAsync(IEnumerable<string> paths)
    {
        if (_main is not null) _main.BusyImporting = true;
        var progress = new Progress<ImportProgress>(p =>
        {
            if (_main is null) return;
            _main.InfoMessage = $"Importing music… {p.Processed}/{p.Total}";
            _main.InfoOpen = true;
        });
        try
        {
            await _library.ImportAsync(paths, progress);
            RefreshAll();
            _main?.ShowInfo("Import complete. The library is ready.");
        }
        catch (Exception ex)
        {
            _main?.ShowInfo($"Import failed: {ex.Message}", true);
        }
        finally
        {
            if (_main is not null) _main.BusyImporting = false;
        }
    }
}
