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
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"));
        AppWindow.Resize(new SizeInt32(1180, 820));
        if (AppWindow.Presenter is OverlappedPresenter p) { p.PreferredMinimumWidth = 420; p.PreferredMinimumHeight = 560; }
        Theme.Apply(RootGrid);
    }

    // Decide where to start: the planner if signed in (or a guest), otherwise the login page
    public void Start()
    {
        if (Auth.Restore() || Store.IsGuest) ShowPlanner();
        else ShowLogin();
    }

    public void ShowLogin() =>
        RootFrame.Navigate(typeof(LoginPage), null, new DrillInNavigationTransitionInfo());

    public void ShowPlanner() =>
        RootFrame.Navigate(typeof(ShellPage), null, new DrillInNavigationTransitionInfo());

    public FrameworkElement Root => RootGrid;
}
