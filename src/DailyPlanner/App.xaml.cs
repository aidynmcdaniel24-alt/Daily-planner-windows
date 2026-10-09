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
        try { InitializeComponent(); }
        catch (Exception ex) { Log("App.xaml failed: " + ex); throw; }
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
            Store.Load();
            Notify.Start();
            Window = new MainWindow();
            Window.Closed += (s, e) => Notify.Stop();
            Store.Ui = Window.DispatcherQueue;
            Window.Activate();
            Window.Start();
        }
        catch (Exception ex) { Log("Startup failed: " + ex); throw; }
    }
}
