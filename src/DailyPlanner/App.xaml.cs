using DailyPlanner.Services;
using Microsoft.UI.Xaml;

namespace DailyPlanner;

public partial class App : Application
{
    public static MainWindow? Window { get; private set; }

    public App()
    {
        InitializeComponent();
        UnhandledException += (s, e) =>
        {
            // Keep the app open on unexpected errors and save what we can
            e.Handled = true;
            try { File.AppendAllText(Path.Combine(Store.Folder, "errors.log"), DateTime.Now + " " + e.Exception + Environment.NewLine); } catch { }
        };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Store.Load();
        Window = new MainWindow();
        Store.Ui = Window.DispatcherQueue;
        Window.Activate();
        Window.Start();
    }
}
