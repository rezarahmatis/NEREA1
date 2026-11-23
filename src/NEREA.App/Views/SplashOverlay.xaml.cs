using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using NEREA.App.ViewModels;

namespace NEREA.App.Views;

/// <summary>
/// Brief startup mark: symbol + NEREA, then it dissolves into the world.
/// Also collapses immediately when the engine degrades.
/// </summary>
public sealed partial class SplashOverlay : UserControl
{
    private MainViewModel? _vm;

    public SplashOverlay()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _vm = DataContext as MainViewModel ?? (Application.Current as App)?.Services
            .GetService(typeof(MainViewModel)) as MainViewModel;
        if (_vm is not null)
        {
            _vm.PropertyChanged += OnVmChanged;
            if (!_vm.SplashVisible) CollapseNow();
        }
    }

    private void OnVmChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainViewModel.SplashVisible)) return;
        DispatcherQueue.TryEnqueue(() =>
        {
            if (_vm!.SplashVisible) Root.Visibility = Visibility.Visible;
            else FadeOut();
        });
    }

    private void FadeOut()
    {
        var anim = new DoubleAnimation { From = 1, To = 0, Duration = new Duration(TimeSpan.FromMilliseconds(900)) };
        Storyboard.SetTarget(anim, Root);
        Storyboard.SetTargetProperty(anim, "Opacity");
        var sb = new Storyboard();
        sb.Children.Add(anim);
        sb.Completed += (_, _) => Root.Visibility = Visibility.Collapsed;
        sb.Begin();
    }

    private void CollapseNow() => Root.Visibility = Visibility.Collapsed;
}
