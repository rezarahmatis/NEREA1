using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using NEREA.App.ViewModels;
using NEREA.Core.Models;

namespace NEREA.App.Views;

/// <summary>
/// The premium music library: songs, albums, artists, playlists, favorites,
/// recent, folders and the live queue with search, sorting and context menus.
/// </summary>
public sealed partial class LibraryPanel : UserControl
{
    private LibraryViewModel? _vm;
    private MainWindow? _host;

    public LibraryPanel()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _vm = DataContext as LibraryViewModel;
        _host = (Application.Current as App)?.Services.GetService(typeof(MainWindow)) as MainWindow;
    }

    // ── Navigation ───────────────────────────────────────────────────────────

    private void Tabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_vm is not null) _vm.SelectedView = Tabs.SelectedIndex;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => _vm?.ClosePanelsPublic();

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (_vm is not null) _vm.ShowGroupTracks = false;
    }

    // ── Play helpers ─────────────────────────────────────────────────────────

    private static Track? TrackFrom(object sender)
    {
        if ((sender as FrameworkElement)?.DataContext is Track t) return t;
        return ((sender as FrameworkElement)?.DataContext as FrameworkElement)?.DataContext as Track;
    }

    private static GroupRow? GroupFrom(object sender) =>
        ((sender as FrameworkElement)?.DataContext as FrameworkElement)?.DataContext as GroupRow
        ?? (sender as FrameworkElement)?.DataContext as GroupRow;

    private void SongsList_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        var track = TrackFrom(e.OriginalSource);
        if (_vm is not null && track is not null) _vm.PlayTrack(track);
    }

    private void GroupsList_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        var group = GroupFrom(e.OriginalSource);
        if (_vm is not null && group is not null) _vm.OpenGroup(group);
    }

    private void PlaylistsList_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (_vm is null) return;
        if ((e.OriginalSource as FrameworkElement)?.DataContext is Playlist p)
            _vm.OpenPlaylist(p);
    }

    private void QueueList_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        var track = TrackFrom(e.OriginalSource);
        if (_vm is not null && track is not null) _vm.PlayTrack(track);
    }

    private void GroupTracksList_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        var track = TrackFrom(e.OriginalSource);
        if (_vm is not null && track is not null) _vm.PlayTrack(track);
    }

    private void PlayGroupTracks_Click(object sender, RoutedEventArgs e)
    {
        if (_vm is not null && _vm.PlaylistSongs.Count > 0)
            _vm.PlayList(_vm.PlaylistSongs.ToList(), _vm.PlaylistSongs[0]);
    }

    private void RemoveFromQueue_Click(object sender, RoutedEventArgs e)
    {
        var track = TrackFrom(sender);
        if (_vm is not null && track is not null) _vm.RemoveFromQueue(track);
    }

    // ── Context menus (right tap) ────────────────────────────────────────────

    private void List_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if (_vm is null) return;
        var src = e.OriginalSource as FrameworkElement;
        var track = src?.DataContext as Track
                    ?? (src?.DataContext as FrameworkElement)?.DataContext as Track;
        var group = src?.DataContext as GroupRow;
        var playlist = src?.DataContext as Playlist;

        if (track is not null && !(_vm.ShowGroupTracks && track == null))
        {
            ShowTrackMenu(track, e.GetPosition(this));
        }
        else if (group is not null)
        {
            ShowGroupMenu(group, e.GetPosition(this));
        }
        else if (playlist is not null)
        {
            ShowPlaylistMenu(playlist, e.GetPosition(this));
        }
        e.Handled = true;
    }

    private void ShowTrackMenu(Track track, Windows.Foundation.Point position)
    {
        if (_vm is null) return;
        var menu = new MenuFlyout();

        menu.Items.Add(MakeItem("Play", _ => _vm.PlayTrack(track)));
        menu.Items.Add(MakeItem("Play next", _ => _vm.QueueNext(track)));
        menu.Items.Add(MakeItem("Add to queue", _ => _vm.QueueTrack(track)));
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(MakeItem(track.Favorite ? "Remove from favorites" : "Add to favorites",
            _ => _vm.ToggleFavorite(track)));

        var toPlaylist = new MenuFlyoutSubItem { Text = "Add to playlist" };
        foreach (var pl in _vm.PlaylistsForMenu)
        {
            var plId = pl.Id;
            toPlaylist.Items.Add(MakeItem(pl.Name, _ => _vm.AddTrackToPlaylist(plId, track)));
        }
        if (toPlaylist.Items.Count > 0) menu.Items.Add(toPlaylist);
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(MakeItem("Remove from library", _ => _vm.RemoveFromLibrary(track)));

        ShowFlyout(menu, position);
    }

    private void ShowGroupMenu(GroupRow group, Windows.Foundation.Point position)
    {
        if (_vm is null) return;
        var menu = new MenuFlyout();
        menu.Items.Add(MakeItem("Play", _ => _vm.PlayGroup(group)));
        menu.Items.Add(MakeItem("Open", _ => _vm.OpenGroup(group)));
        menu.Items.Add(new MenuFlyoutSeparator());
        var toPlaylist = new MenuFlyoutSubItem { Text = "Add all to playlist" };
        foreach (var pl in _vm.PlaylistsForMenu)
        {
            var plId = pl.Id;
            toPlaylist.Items.Add(MakeItem(pl.Name, _ =>
            {
                foreach (var t in group.Tracks) _vm.AddTrackToPlaylist(plId, t);
            }));
        }
        if (toPlaylist.Items.Count > 0) menu.Items.Add(toPlaylist);
        ShowFlyout(menu, position);
    }

    private void ShowPlaylistMenu(Playlist playlist, Windows.Foundation.Point position)
    {
        if (_vm is null) return;
        var menu = new MenuFlyout();
        menu.Items.Add(MakeItem("Open", _ => _vm.OpenPlaylist(playlist)));
        menu.Items.Add(MakeItem("Duplicate", _ => _vm.DuplicatePlaylist(playlist)));
        menu.Items.Add(MakeItem("Rename…", _ => RenamePlaylistDialog(playlist)));
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(MakeItem("Delete", _ => _vm.DeletePlaylist(playlist)));
        ShowFlyout(menu, position);
    }

    private void ShowFlyout(MenuFlyout menu, Windows.Foundation.Point position)
    {
        menu.ShowAt(PanelRoot, position);
    }

    private static MenuFlyoutItem MakeItem(string text, Action<RoutedEventArgs> action)
    {
        var item = new MenuFlyoutItem { Text = text };
        item.Click += (_, e) => action(e);
        return item;
    }

    // ── Dialogs ──────────────────────────────────────────────────────────────

    private async void NewPlaylist_Click(object sender, RoutedEventArgs e)
    {
        var name = await PromptAsync("New playlist", "Playlist name");
        if (!string.IsNullOrWhiteSpace(name)) _vm?.CreatePlaylist(name);
    }

    private async void RenamePlaylistDialog(Playlist playlist)
    {
        var name = await PromptAsync("Rename playlist", "New name", playlist.Name);
        if (!string.IsNullOrWhiteSpace(name) && _vm is not null)
            _vm.RenamePlaylistTo(playlist.Id, name);
    }

    private async Task<string?> PromptAsync(string title, string placeholder, string initial = "")
    {
        var dialog = new ContentDialog
        {
            Title = title,
            XamlRoot = XamlRoot,
            PrimaryButtonText = "OK",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary
        };
        var box = new TextBox { PlaceholderText = placeholder, Text = initial };
        dialog.Content = box;
        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary ? box.Text.Trim() : null;
    }

    // ── Import ───────────────────────────────────────────────────────────────

    private void AddFiles_Click(object sender, RoutedEventArgs e) => _ = (_host?.PickFilesAsync());

    private void AddFolder_Click(object sender, RoutedEventArgs e) => _ = (_host?.PickFolderAsync());
}
