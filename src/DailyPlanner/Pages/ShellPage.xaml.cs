using System.Diagnostics;
using DailyPlanner.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;

namespace DailyPlanner.Pages;

public sealed partial class ShellPage : Page
{
    public static ShellPage? Current { get; private set; }
    DispatcherTimer? poll, toastTimer;

    public ShellPage()
    {
        InitializeComponent();
        Current = this;
        Nav.SelectedItem = Nav.MenuItems[0];
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        Store.Finished += OnFinished;
        Store.SyncState += OnSync;
        Store.Kicked += OnKicked;
        if (App.Window != null) App.Window.Activated += OnActivated;

        // Check the account for changes from the website every minute
        poll = new DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
        poll.Tick += async (s, a) => await Pull();
        poll.Start();
        _ = Pull();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        Store.Finished -= OnFinished;
        Store.SyncState -= OnSync;
        Store.Kicked -= OnKicked;
        if (App.Window != null) App.Window.Activated -= OnActivated;
        poll?.Stop();
    }

    async void OnActivated(object sender, WindowActivatedEventArgs e)
    {
        if (e.WindowActivationState != WindowActivationState.Deactivated) await Pull();
    }

    static bool pulling;
    static async Task Pull()
    {
        if (pulling || Auth.Current == null || Store.Syncing) return;
        pulling = true;
        try { await Store.SyncDown(); } catch { }
        finally { pulling = false; }
    }

    // ---------- Navigation ----------
    void Nav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected) { Go(typeof(SettingsPage), null); return; }
        var tag = (args.SelectedItem as NavigationViewItem)?.Tag?.ToString();
        if (tag == "home") Go(typeof(HomePage), null);
        else if (tag is "gaming" or "sleep" or "coding") Go(typeof(ChecklistPage), tag);
    }

    void Nav_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if ((args.InvokedItemContainer as NavigationViewItem)?.Tag?.ToString() == "web")
            Process.Start(new ProcessStartInfo(Config.Website + "home/") { UseShellExecute = true });
    }

    void Go(Type page, object? param) =>
        PageFrame.Navigate(page, param, new EntranceNavigationTransitionInfo());

    // Used by the Home page's lane buttons
    public void OpenLane(string lane)
    {
        Nav.SelectedItem = lane switch { "gaming" => NavGaming, "sleep" => NavSleep, _ => NavCoding };
    }

    // ---------- Pop-ups ----------
    void OnFinished(string lane)
    {
        int s = Store.Streak(lane);
        ShowToast($"{Store.LaneNames[lane]} checklist done!", $"Streak: {s} {(s == 1 ? "day" : "days")}. Nice work.");
    }

    public void ShowToast(string title, string text)
    {
        Toast.Title = title;
        Toast.Message = text;
        Toast.IsOpen = true;
        toastTimer?.Stop();
        toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        toastTimer.Tick += (s, a) => { Toast.IsOpen = false; toastTimer?.Stop(); };
        toastTimer.Start();
    }

    void OnSync(bool on) => SyncBadge.Opacity = on ? 1 : 0;

    async void OnKicked()
    {
        Auth.SignOut();
        Store.ClearLocal();
        var d = new ContentDialog
        {
            Title = "You were logged out",
            Content = "Someone used \"Log out of all devices\" on your account. Log in again to keep going.",
            CloseButtonText = "OK",
            XamlRoot = XamlRoot,
        };
        await d.ShowAsync();
        App.Window?.ShowLogin();
    }
}
