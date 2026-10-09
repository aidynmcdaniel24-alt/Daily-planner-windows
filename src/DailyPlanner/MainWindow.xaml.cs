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
        // Open at a size that fits this screen (or where it was last time), and remember it
        WindowPlace.Restore(AppWindow);
        Closed += (s, e) => WindowPlace.Save(AppWindow);
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
