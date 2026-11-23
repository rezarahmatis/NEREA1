using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using NEREA.App.Services;
using NEREA.App.ViewModels;
using NEREA.Core.Data;
using NEREA.Core.Services;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace NEREA.App;

public sealed partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private readonly EngineHost _engine;
    private readonly WindowHookService _hook;
    private readonly SystemMediaControlsService _smtc;
    private readonly AquariumService _aquarium;
    private readonly SettingsStore _settings;

    private DispatcherQueueTimer? _toastTimer;
    private bool _fullscreen;
    private bool _closingToTray;

    public MainWindow(
        MainViewModel vm,
        EngineHost engine,
        WindowHookService hook,
        SystemMediaControlsService smtc,
        AquariumService aquarium,
        SettingsStore settings)
    {
        InitializeComponent();

        _vm = vm;
        _engine = engine;
        _hook = hook;
        _smtc = smtc;
        _aquarium = aquarium;
        _settings = settings;

        Title = "NEREA";
        Root.DataContext = _vm;
        PlayBar.DataContext = _vm;
        LibraryPanelView.DataContext = _vm.LibraryVm;
        AquariumPanelView.DataContext = _vm.AquariumVm;
        SettingsPanelView.DataContext = _vm.SettingsVm;
        // Panel visibility lives on the shell view model.
        LibraryPanelView.SetBinding(UIElement.VisibilityProperty, new Microsoft.UI.Xaml.Data.Binding
        {
            Path = new Microsoft.UI.Xaml.PropertyPath(nameof(MainViewModel.ShowLibrary)),
            Source = _vm
        });
        AquariumPanelView.SetBinding(UIElement.VisibilityProperty, new Microsoft.UI.Xaml.Data.Binding
        {
            Path = new Microsoft.UI.Xaml.PropertyPath(nameof(MainViewModel.ShowAquarium)),
            Source = _vm
        });
        SettingsPanelView.SetBinding(UIElement.VisibilityProperty, new Microsoft.UI.Xaml.Data.Binding
        {
            Path = new Microsoft.UI.Xaml.PropertyPath(nameof(MainViewModel.ShowSettings)),
            Source = _vm
        });

        ConfigureWindow();
        HookWindowEvents();
        WireEngine();
        WireKeyboard();

        Root.Loaded += OnRootLoaded;
        _vm.PropertyChanged += OnVmPropertyChanged;
    }

    private void ConfigureWindow()
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);

        appWindow.Resize(new SizeInt32(1600, 900));
        appWindow.Title = "NEREA";
        try { appWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "NEREA.ico")); } catch { }

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBarDrag);
        appWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Standard;
        appWindow.TitleBar.BackgroundColor = Colors.Transparent;
        appWindow.TitleBar.ForegroundColor = Colors.Transparent;
        appWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
        appWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;

        _hook.Attach(hwnd, Path.Combine(AppContext.BaseDirectory, "Assets", "NEREA.ico"));
        _hook.TrayShowRequested += ShowFromTray;
        _hook.TrayExitRequested += ExitFromTray;
        _smtc.Attach(hwnd);

        appWindow.Closing += (s, e) =>
        {
            if (_settings.Settings.MinimizeToTray && !_closingToTray)
            {
                e.Cancel = true;
                AppWindow.Hide();
            }
        };
        appWindow.Changed += (s, e) =>
        {
            if (e.DidVisibilityChange)
            {
                bool visible = AppWindow.IsVisible;
                if (visible) _engine.SetPaused(false);
                else if (_settings.Settings.PauseRenderWhenMinimized) _engine.SetPaused(true);
            }
        };
    }

    private void HookWindowEvents()
    {
        Root.PointerMoved += OnPointerActivity;
        Root.PointerPressed += OnPointerPressed;
        Root.PointerReleased += OnPointerReleased;
        Root.PointerWheelChanged += OnPointerWheel;
        Root.DragOver += OnDragOver;
        Root.Drop += OnDrop;
        Root.SizeChanged += OnRootSizeChanged;
        Root.DoubleTapped += OnDoubleTapped;
        Activated += (_, e) =>
        {
            if (e.WindowActivationState != WindowActivationState.Deactivated)
                Root.Focus(FocusState.Programmatic);
        };
        Closed += (_, _) => Teardown();
    }

    private void WireEngine()
    {
        _engine.DegradedChanged += (_, _) =>
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                FallbackBackground.Visibility = _engine.Degraded ? Visibility.Visible : Visibility.Collapsed;
            });
        };
    }

    private void WireKeyboard()
    {
        Root.KeyDown += OnKeyDown;
    }

    private void OnRootLoaded(object sender, RoutedEventArgs e)
    {
        _engine.Attach(RenderPanel);
        _engine.Resize(Root.ActualWidth, Root.ActualHeight);
        _vm.RestoreSession();
        Root.Focus(FocusState.Programmatic);
        AnimateWorldToast();
    }

    private void OnRootSizeChanged(object sender, SizeChangedEventArgs e) =>
        _engine.Resize(e.NewSize.Width, e.NewSize.Height);

    // ── Pointer → engine interaction ─────────────────────────────────────────

    private void OnPointerActivity(object sender, PointerRoutedEventArgs e)
    {
        var p = e.GetCurrentPoint(Root).Position;
        _vm.NotifyActivity(p.Y > Root.ActualHeight * 0.72);
        UpdateCursorIdle();

        if (_vm.AnyPanelOpen) return;
        // WinUI and the Windows SDK each project a PointerDeviceType with the same
        // name; compare the underlying value to stay independent of the projection.
        int pointerType = (int)e.Pointer.PointerDeviceType;
        if (pointerType != (int)Windows.Devices.Input.PointerDeviceType.Mouse &&
            pointerType != (int)Windows.Devices.Input.PointerDeviceType.Touch) return;
        _engine.Engine.OnPointerMove(
            (float)(p.X / Math.Max(1, Root.ActualWidth)),
            (float)(p.Y / Math.Max(1, Root.ActualHeight)),
            (float)DateTime.UtcNow.TimeOfDay.TotalSeconds);
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _vm.NotifyActivity(false);
        if (_vm.AnyPanelOpen) return;
        var p = e.GetCurrentPoint(Root).Position;
        if (e.GetCurrentPoint(Root).Properties.IsLeftButtonPressed)
        {
            _engine.Engine.OnPointerPress(
                (float)(p.X / Math.Max(1, Root.ActualWidth)),
                (float)(p.Y / Math.Max(1, Root.ActualHeight)));
            _dragging = true;
        }
    }

    private bool _dragging;

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_dragging)
        {
            _dragging = false;
            _engine.Engine.OnPointerRelease();
        }
        _vm.NotifyActivity(false);
    }

    private void OnPointerWheel(object sender, PointerRoutedEventArgs e)
    {
        var delta = e.GetCurrentPoint(Root).Properties.MouseWheelDelta;
        _vm.SetVolumeRelative(delta > 0 ? 0.04 : -0.04);
        _vm.NotifyActivity(false);
    }

    private void OnDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        // Double-click the world itself toggles cinematic mode.
        if (!_vm.AnyPanelOpen)
        {
            _vm.ToggleCinematic();
        }
    }

    private void UpdateCursorIdle()
    {
        // Cursor fades after inactivity in cinematic/ambient mode (WM_SETCURSOR hook).
        _hook.SetCursorHidden((_vm.Cinematic || _vm.Ambient) && _vm.IsIdle);
    }

    // ── Keyboard ─────────────────────────────────────────────────────────────

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        var focused = FocusManager.GetFocusedElement();
        if (focused is TextBox or ComboBox or Slider) return; // don't hijack typing

        bool ctrl = Microsoft.UI.Input.InputKeyboardSource
            .GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

        if (ctrl && e.Key == (Windows.System.VirtualKey)0x4F /* O */)
        {
            _ = PickFilesAsync();
            e.Handled = true;
            return;
        }
        if (ctrl && e.Key == (Windows.System.VirtualKey)0x46 /* F */)
        {
            _vm.OpenLibrary();
            e.Handled = true;
            return;
        }

        switch (e.Key)
        {
            case Windows.System.VirtualKey.Space:
                _vm.PlayPause();
                e.Handled = true;
                break;
            case Windows.System.VirtualKey.M:
                _vm.ToggleMute();
                e.Handled = true;
                break;
            case Windows.System.VirtualKey.N:
                _vm.Next();
                e.Handled = true;
                break;
            case Windows.System.VirtualKey.P:
                _vm.Previous();
                e.Handled = true;
                break;
            case Windows.System.VirtualKey.F11:
                ToggleFullscreen();
                e.Handled = true;
                break;
            case Windows.System.VirtualKey.Escape:
                if (_fullscreen) ToggleFullscreen();
                else _vm.ClosePanels();
                e.Handled = true;
                break;
            case (Windows.System.VirtualKey)0x43 /* C */:
                _vm.ToggleCinematic();
                e.Handled = true;
                break;
            case (Windows.System.VirtualKey)0x41 /* A */:
                if (_vm.Ambient) _vm.ExitAmbient(); else _vm.EnterAmbient();
                e.Handled = true;
                break;
            case Windows.System.VirtualKey.Left:
                _vm.SeekRelative(-5);
                e.Handled = true;
                break;
            case Windows.System.VirtualKey.Right:
                _vm.SeekRelative(5);
                e.Handled = true;
                break;
            case Windows.System.VirtualKey.Up:
                _vm.SetVolumeRelative(0.05);
                e.Handled = true;
                break;
            case Windows.System.VirtualKey.Down:
                _vm.SetVolumeRelative(-0.05);
                e.Handled = true;
                break;
        }
    }

    private void ToggleFullscreen()
    {
        _fullscreen = !_fullscreen;
        AppWindow.SetPresenter(_fullscreen
            ? AppWindowPresenterKind.FullScreen
            : AppWindowPresenterKind.Overlapped);
    }

    // ── Drag & drop import ───────────────────────────────────────────────

    private async void OnDragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
        }
        await Task.CompletedTask;
    }

    private async void OnDrop(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems)) return;
        try
        {
            var items = await e.DataView.GetStorageItemsAsync();
            var paths = items.Select(i => i.Path).ToList();
            if (paths.Count > 0)
                await _vm.LibraryVm.ImportAsync(paths);
        }
        catch
        {
            _vm.ShowInfo("Those items could not be imported.", true);
        }
    }

    // ── Pickers ──────────────────────────────────────────────────────────────

    public async Task PickFilesAsync()
    {
        var picker = new FileOpenPicker();
        foreach (var ext in new[] { ".mp3", ".wav", ".flac", ".ogg", ".oga", ".m4a", ".aac", ".wma" })
            picker.FileTypeFilter.Add(ext);
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
        var files = await picker.PickMultipleFilesAsync();
        if (files.Count > 0)
            await _vm.LibraryVm.ImportAsync(files.Select(f => f.Path));
    }

    public async Task PickFolderAsync()
    {
        var picker = new FolderPicker();
        picker.FileTypeFilter.Add("*");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
        var folder = await picker.PickSingleFolderAsync();
        if (folder is not null)
            await _vm.LibraryVm.ImportAsync(new[] { folder.Path });
    }

    // ── Pickers ──────────────────────────────────────────────────────────────

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainViewModel.OverlayOpacity):
            case nameof(MainViewModel.ControlsVisible):
            case nameof(MainViewModel.Cinematic):
            case nameof(MainViewModel.Ambient):
                DispatcherQueue.TryEnqueue(UpdateChrome);
                break;
            case nameof(MainViewModel.WorldToastVisible):
                DispatcherQueue.TryEnqueue(() =>
                {
                    if (_vm.WorldToastVisible) AnimateWorldToast();
                });
                break;
            case nameof(MainViewModel.InfoMessage):
                DispatcherQueue.TryEnqueue(() =>
                {
                    _vm.InfoSeverity = _vm.InfoError
                        ? Microsoft.UI.Xaml.Controls.InfoBarSeverity.Error
                        : Microsoft.UI.Xaml.Controls.InfoBarSeverity.Informational;
                });
                break;
        }
    }

    private void UpdateChrome()
    {
        bool cinematic = _vm.Cinematic || _vm.Ambient;
        bool visible = !cinematic && _vm.ControlsVisible;
        double target = cinematic ? 0.0 : _vm.OverlayOpacity;
        AnimateOpacity(Brand, cinematic ? 0.55 : target);
        AnimateOpacity(TopActions, target);
        AnimateOpacity(PlayBar, target);
        _hook.SetCursorHidden(cinematic && _vm.ControlsVisible == false);
    }

    private void AnimateOpacity(UIElement element, double to)
    {
        var anim = new DoubleAnimation { To = to, Duration = new Duration(TimeSpan.FromMilliseconds(450)) };
        Storyboard.SetTarget(anim, element);
        Storyboard.SetTargetProperty(anim, "Opacity");
        var sb = new Storyboard();
        sb.Children.Add(anim);
        sb.Begin();
    }

    private void AnimateWorldToast()
    {
        _toastTimer?.Stop();
        var fadeIn = new DoubleAnimation { From = 0, To = 0.9, Duration = new Duration(TimeSpan.FromMilliseconds(700)) };
        Storyboard.SetTarget(fadeIn, WorldToast);
        Storyboard.SetTargetProperty(fadeIn, "Opacity");
        var sb = new Storyboard();
        sb.Children.Add(fadeIn);
        sb.Completed += (_, _) =>
        {
            _toastTimer = DispatcherQueue.CreateTimer();
            _toastTimer.Interval = TimeSpan.FromSeconds(2.6);
            _toastTimer.Tick += (_, _) =>
            {
                _toastTimer.Stop();
                var fadeOut = new DoubleAnimation { From = 0.9, To = 0, Duration = new Duration(TimeSpan.FromMilliseconds(900)) };
                Storyboard.SetTarget(fadeOut, WorldToast);
                Storyboard.SetTargetProperty(fadeOut, "Opacity");
                var sb2 = new Storyboard();
                sb2.Children.Add(fadeOut);
                sb2.Completed += (_, _) => _vm.DismissWorldToast();
                sb2.Begin();
            };
            _toastTimer.Start();
        };
        sb.Begin();
    }

    // ── Tray ─────────────────────────────────────────────────────────────

    private void ShowFromTray()
    {
        AppWindow.Show();
        Activate();
        Root.Focus(FocusState.Programmatic);
    }

    private void ExitFromTray()
    {
        _closingToTray = true;
        Teardown();
        Application.Current.Exit();
    }

    private void Teardown()
    {
        try
        {
            _vm.PersistSession();
            _hook.RemoveTrayIcon();
            _engine.Dispose();
        }
        catch
        {
            // Shutdown must always complete.
        }
    }
}
