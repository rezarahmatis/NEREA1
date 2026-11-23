using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NEREA.App.ViewModels;

namespace NEREA.App.Views;

/// <summary>
/// Application settings. Every control maps to a real setting applied
/// immediately and persisted to the local settings file.
/// </summary>
public sealed partial class SettingsPanel : UserControl
{
    private SettingsViewModel? _vm;
    private MainViewModel? _main;

    public SettingsPanel()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _vm = DataContext as SettingsViewModel;
        _main = _vm?.AppMain;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => _main?.ClosePanelsPublic();

    private void Rescan_Click(object sender, RoutedEventArgs e) => _vm?.RescanLibrary();
}
