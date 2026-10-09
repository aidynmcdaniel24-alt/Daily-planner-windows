using DailyPlanner.Pages;
using DailyPlanner.Services;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.Graphics;

namespace DailyPlanner;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        try { InitializeComponent(); }
        catch (Exception ex) { App.Log("MainWindow.xaml failed: " + ex); throw; }
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"));
        // Open at a comfortable size that fits the screen, centered
        var area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
        int w = Math.Min(1180, (int)(area.Width * 0.9)), h = Math.Min(820, (int)(area.Height * 0.9));
        AppWindow.MoveAndResize(new RectInt32(area.X + (area.Width - w) / 2, area.Y + (area.Height - h) / 2, w, h));
        if (AppWindow.Presenter is OverlappedPresenter p) { p.PreferredMinimumWidth = 420; p.PreferredMinimumHeight = 560; }
        Theme.Apply(RootGrid);
    }

    // Decide where to start: the planner if signed in (or a guest), otherwise the login page
    public void Start()
    {
        if (Auth.Restore() || Store.IsGuest) ShowPlanner();
        else ShowLogin();
    }

    public void ShowLogin() => Go(typeof(LoginPage));
    public void ShowPlanner() => Go(typeof(ShellPage));

    void Go(Type page)
    {
        try { RootFrame.Navigate(page, null, new DrillInNavigationTransitionInfo()); }
        catch (Exception ex) { App.Log(page.Name + " failed: " + ex); throw; }
    }

    public FrameworkElement Root => RootGrid;
}
