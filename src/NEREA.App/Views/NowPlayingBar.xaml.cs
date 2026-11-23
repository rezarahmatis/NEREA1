using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using NEREA.App.ViewModels;

namespace NEREA.App.Views;

/// <summary>
/// The minimal playback layer: metadata, a thin seek line and quiet transport
/// controls belonging to a digital artwork rather than a media player.
/// </summary>
public sealed partial class NowPlayingBar : UserControl
{
    private MainViewModel? _vm;
    private bool _seeking;

    public NowPlayingBar()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _vm = DataContext as MainViewModel;
        if (_vm is not null)
        {
            _vm.PropertyChanged += OnVmChanged;
        }
        SeekArea.SizeChanged += (_, _) => UpdateSeekVisual();
        UpdateSeekVisual();
        UpdateModeMarks();
    }

    private void OnVmChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.PositionFraction))
        {
            DispatcherQueue.TryEnqueue(UpdateSeekVisual);
        }
        else if (e.PropertyName == nameof(MainViewModel.IsShuffle) ||
                 e.PropertyName == nameof(MainViewModel.RepeatIndex))
        {
            DispatcherQueue.TryEnqueue(UpdateModeMarks);
        }
    }

    private void UpdateSeekVisual()
    {
        double w = Math.Max(1, SeekArea.ActualWidth);
        double frac = _vm?.PositionFraction ?? 0;
        SeekFill.Width = w * frac;
        SeekThumb.Margin = new Thickness(w * frac - 3.5, 0, 0, 0);
    }

    private void UpdateModeMarks()
    {
        if (_vm is null) return;
        ShuffleButton.IsChecked = _vm.IsShuffle;
        RepeatButton.IsChecked = _vm.RepeatIndex > 0;
        RepeatOneMark.Opacity = _vm.RepeatIndex == 2 ? 1 : 0;
    }

    // ── Seek interactions ────────────────────────────────────────────────────

    private double FractionFromPoint(PointerRoutedEventArgs e)
    {
        var p = e.GetCurrentPoint(SeekArea).Position;
        return Math.Clamp(p.X / Math.Max(1, SeekArea.ActualWidth), 0, 1);
    }

    private void SeekArea_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _seeking = true;
        SeekArea.CapturePointer(e.Pointer);
        _vm?.SeekFraction(FractionFromPoint(e));
        e.Handled = true;
    }

    private void SeekArea_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_seeking) return;
        _vm?.SeekFraction(FractionFromPoint(e));
        e.Handled = true;
    }

    private void SeekArea_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_seeking) return;
        _seeking = false;
        SeekArea.ReleasePointerCapture(e.Pointer);
        _vm?.NotifyActivity(false);
        e.Handled = true;
    }

    private void Shuffle_Click(object sender, RoutedEventArgs e) => _vm?.ToggleShuffle();

    private void Repeat_Click(object sender, RoutedEventArgs e) => _vm?.CycleRepeat();
}
