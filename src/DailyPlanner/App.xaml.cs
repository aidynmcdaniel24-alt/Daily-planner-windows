using DailyPlanner.Services;
using Microsoft.UI.Xaml;

namespace DailyPlanner;

public partial class App : Application
{
    public static MainWindow? Window { get; private set; }

    // Writes what happened at startup to %LocalAppData%\DailyPlanner\errors.log (helps find crashes)
    public static void Log(string text)
    {
        try
        {
            Directory.CreateDirectory(Store.Folder);
            File.AppendAllText(Path.Combine(Store.Folder, "errors.log"), DateTime.Now.ToString("s") + " " + text + Environment.NewLine);
        }
        catch { }
    }

    public App()
    {
        AppDomain.CurrentDomain.UnhandledException += (s, e) => Log("Crash: " + e.ExceptionObject);
        // Writes down every error, even ones that get handled (only in the first minute, to find startup problems)
        var started = DateTime.Now;
        AppDomain.CurrentDomain.FirstChanceException += (s, e) =>
        {
            if ((DateTime.Now - started).TotalSeconds < 20 && e.Exception is not (OperationCanceledException or HttpRequestException))
                Log("First-chance: " + e.Exception.GetType().Name + ": " + e.Exception.Message + " @ " + e.Exception.StackTrace?.Split('\n').FirstOrDefault()?.Trim());
        };
        try { InitializeComponent(); }
        catch (Exception ex) { Log("App.xaml failed: " + ex); throw; }
        try { var log = Path.Combine(Store.Folder, "errors.log"); if (File.Exists(log) && new FileInfo(log).Length > 200_000) File.Delete(log); } catch { }
        // Load the planner now so menus and pop-ups open in the right theme (light or dark)
        Store.Load();
        Theme.System = RequestedTheme;
        if (Theme.Mode == "dark") RequestedTheme = ApplicationTheme.Dark;
        else if (Theme.Mode == "light") RequestedTheme = ApplicationTheme.Light;
        UnhandledException += (s, e) =>
        {
            // Keep the app open on unexpected errors and write them down
            e.Handled = true;
            Log("Error: " + e.Exception);
        };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            Notify.Start();
            Window = new MainWindow();
            Window.Closed += (s, e) => { FocusTimer.Finish(); Store.Flush(); Notify.Stop(); };
            Store.Ui = Window.DispatcherQueue;
            Window.Activate();
            Window.Start();
        }
        catch (Exception ex) { Log("Startup failed: " + ex); throw; }
    }
}
