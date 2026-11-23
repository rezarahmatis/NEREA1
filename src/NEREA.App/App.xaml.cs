using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using NEREA.App.Services;
using NEREA.App.ViewModels;
using NEREA.Core.Data;
using NEREA.Core.Services;
using Windows.Storage;

namespace NEREA.App;

/// <summary>NEREA application entry point with dependency injection.</summary>
public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;
    public static string DataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NEREA");

    private Window? _window;

    public App()
    {
        InitializeComponent();
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(Path.Combine(DataDirectory, "logs"));
        UnhandledException += OnUnhandledException;
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Services = BuildServices();
        _window = Services.GetRequiredService<MainWindow>();
        _window.Activate();
    }

    private static IServiceProvider BuildServices()
    {
        var services = new ServiceCollection();

        services.AddSingleton(new LibraryDatabase(Path.Combine(DataDirectory, "library.db")));
        services.AddSingleton(new TagMetadataReader(Path.Combine(DataDirectory, "artwork")));
        services.AddSingleton<MusicLibraryService>();
        services.AddSingleton<PlaylistService>();
        services.AddSingleton(new SettingsStore(Path.Combine(DataDirectory, "settings.json")));
        services.AddSingleton(new AquariumStore(Path.Combine(DataDirectory, "aquariums.json")));
        services.AddSingleton<AquariumService>();

        services.AddSingleton<IMusicPlayer, AudioPlayerService>();
        services.AddSingleton<PlaybackController>();

        services.AddSingleton<EngineHost>();
        services.AddSingleton<SystemMediaControlsService>();
        services.AddSingleton<WindowHookService>();
        services.AddSingleton<AutostartService>();

        services.AddSingleton<MainViewModel>();
        services.AddSingleton<LibraryViewModel>();
        services.AddSingleton<AquariumViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<MainWindow>();

        return services.BuildServiceProvider();
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        try
        {
            File.AppendAllText(
                Path.Combine(DataDirectory, "logs", "nerea-crash.log"),
                $"{DateTime.Now:O}  {e.Message}\n{e.Exception}\n\n");
        }
        catch
        {
            // Never let logging fail the app.
        }
        e.Handled = true;
    }
}
