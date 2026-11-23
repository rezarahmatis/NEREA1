using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace NEREA.App;

/// <summary>
/// Custom entry point. The XAML compiler emits a partial Program class in
/// App.g.i.cs; with DISABLE_XAML_GENERATED_MAIN defined (see csproj) its Main
/// is excluded and this partial part supplies the real entry point so that
/// every early failure - missing runtime file, bootstrap error, XAML resource
/// load error, DI construction failure - is written to a log and shown to the
/// user instead of closing NEREA silently.
/// </summary>
internal static partial class Program
{
    internal static readonly string LogDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "NEREA", "logs");

    [STAThread]
    private static void Main(string[] args)
    {
        try
        {
            WinRT.ComWrappersSupport.InitializeComWrappers();
            Microsoft.UI.Xaml.Application.Start(_ =>
            {
                var context = new Microsoft.UI.Dispatching.DispatcherQueueSynchronizationContext(
                    Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());
                SynchronizationContext.SetSynchronizationContext(context);
                _ = new App();
            });
        }
        catch (Exception ex)
        {
            WriteCrash("Main", ex);
            ShowFatal(ex);
        }
    }

    /// <summary>Runs before Main: installs process-wide crash reporting.</summary>
    [ModuleInitializer]
    internal static void InstallCrashReporting()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            WriteCrash("AppDomain.UnhandledException", e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) =>
            WriteCrash("TaskScheduler.UnobservedTaskException", e.Exception);
        WriteLog("NEREA process started.");
    }

    internal static void WriteLog(string message)
    {
        try
        {
            Directory.CreateDirectory(LogDirectory);
            File.AppendAllText(Path.Combine(LogDirectory, "nerea-crash.log"),
                $"{DateTime.Now:O}  {message}\n");
        }
        catch
        {
            // Logging must never throw.
        }
    }

    internal static void WriteCrash(string source, Exception? ex)
    {
        try
        {
            Directory.CreateDirectory(LogDirectory);
            var logPath = Path.Combine(LogDirectory, "nerea-crash.log");
            File.AppendAllText(logPath,
                $"{DateTime.Now:O}  [{source}]\n{(ex?.ToString() ?? "(no exception object)")}\n\n");
        }
        catch
        {
            // Logging must never throw.
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);

    /// <summary>Shows the last-resort error dialog when no window could be created.</summary>
    internal static void ShowFatal(Exception ex)
    {
        var logPath = Path.Combine(LogDirectory, "nerea-crash.log");
        var text =
            "NEREA could not start.\n\n" +
            $"{ex.GetType().FullName}: {ex.Message}\n\n" +
            $"Technical details were written to:\n{logPath}";
        try { MessageBoxW(IntPtr.Zero, text, "NEREA - startup error", 0x10); }
        catch
        {
            // No dialog is better than crashing again.
        }
    }
}
