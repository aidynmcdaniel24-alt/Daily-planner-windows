using System.Diagnostics;
using DailyPlanner.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using Windows.System;

namespace DailyPlanner.Pages;

public sealed partial class ShellPage : Page
{
    public static ShellPage? Current { get; private set; }
    DispatcherTimer? poll, toastTimer, minute;
    Action? undo;
    readonly HashSet<string> reminded = new();

    public ShellPage()
    {
        InitializeComponent();
        Current = this;
        BreakTimer.Init();
        // "--page gaming" opens a page directly (used by the automatic screenshot test)
        var args = Environment.GetCommandLineArgs();
        int pi = Array.IndexOf(args, "--page");
        string start = pi > 0 && pi + 1 < args.Length ? args[pi + 1] : "home";
        if (start == "settings") Nav.SelectedItem = Nav.SettingsItem;
        else Nav.SelectedItem = Nav.MenuItems.OfType<NavigationViewItem>().FirstOrDefault(i => i.Tag?.ToString() == start) ?? Nav.MenuItems[0];

        // Keyboard shortcuts: 1-5 open tabs, F focus mode, N next task
        void Key(VirtualKey k, Action a, VirtualKeyModifiers m = VirtualKeyModifiers.None)
        {
            var acc = new KeyboardAccelerator { Key = k, Modifiers = m };
            acc.Invoked += (s, e) => { if (FocusManager.GetFocusedElement(XamlRoot) is TextBox or PasswordBox or AutoSuggestBox) return; e.Handled = true; a(); };
            KeyboardAccelerators.Add(acc);
        }
        Key(VirtualKey.Number1, () => Select("gaming")); Key(VirtualKey.Number2, () => Select("sleep"));
        Key(VirtualKey.Number3, () => Select("coding")); Key(VirtualKey.Number4, () => Select("summary"));
        Key(VirtualKey.Number5, () => Select("board")); Key(VirtualKey.H, () => Select("home"));
        Key(VirtualKey.F, () => SetFocus(!focusOn));
        Key(VirtualKey.Escape, () => { if (focusOn) SetFocus(false); });
        KeyboardAcceleratorPlacementMode = KeyboardAcceleratorPlacementMode.Hidden;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        Store.Finished += OnFinished;
        Store.SyncState += OnSync;
        Store.Kicked += OnKicked;
        Store.Saved += OnSaved;
        if (App.Window != null) App.Window.Activated += OnActivated;

        // Check the account for changes from the website every minute
        poll = new DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
        poll.Tick += async (s, a) => await Pull();
        poll.Start();
        minute = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        minute.Tick += (s, a) => Reminders();
        minute.Start();
        _ = Pull();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        Store.Finished -= OnFinished;
        Store.SyncState -= OnSync;
        Store.Kicked -= OnKicked;
        Store.Saved -= OnSaved;
        if (App.Window != null) App.Window.Activated -= OnActivated;
        poll?.Stop(); minute?.Stop();
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

    // Keep my leaderboard streak up to date after saving
    static DateTime lastBoard = DateTime.MinValue;
    async void OnSaved()
    {
        if (Board.Lb == null || Auth.Current == null || (DateTime.Now - lastBoard).TotalSeconds < 20) return;
        lastBoard = DateTime.Now;
        try { await Board.Update(); } catch { }
    }

    // ---------- Reminders (bedtime and study) ----------
    void Reminders()
    {
        var rm = Store.St["rm"] as System.Text.Json.Nodes.JsonObject; if (rm == null) return;
        var now = DateTime.Now; int m = now.Hour * 60 + now.Minute;
        void Check(string key, string title, string text, bool skip = false)
        {
            if (!TimeSpan.TryParse(rm[key]?.ToString(), out var t)) return;
            int at = (int)t.TotalMinutes; string id = key + Store.Td();
            if (m >= at && m < at + 5 && !reminded.Contains(id) && !skip)
            {
                reminded.Add(id);
                Notify.Show(title, text);
                ShowToast(title, text);
            }
        }
        Check("b", "Bedtime reminder", "Start winding down. Screens off soon.");
        Check("s", "Study reminder", "Time for your coding session.", Store.Done("coding").Count > 0);
    }

    // ---------- Navigation ----------
    void Nav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected) { Go(typeof(SettingsPage)); return; }
        switch ((args.SelectedItem as NavigationViewItem)?.Tag?.ToString())
        {
            case "home": Go(typeof(HomePage)); break;
            case "gaming": Go(typeof(GamingPage)); break;
            case "sleep": Go(typeof(SleepPage)); break;
            case "coding": Go(typeof(CodingPage)); break;
            case "summary": Go(typeof(SummaryPage)); break;
            case "board": Go(typeof(LeaderboardPage)); break;
        }
    }

    void Nav_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        switch ((args.InvokedItemContainer as NavigationViewItem)?.Tag?.ToString())
        {
            case "web": Process.Start(new ProcessStartInfo(Config.Website + "home/") { UseShellExecute = true }); break;
            case "focus": SetFocus(true); break;
        }
    }

    void Go(Type page)
    {
        if (focusOn) SetFocus(false, false);
        PageFrame.Navigate(page, null, new EntranceNavigationTransitionInfo());
    }

    void Select(string tag)
    {
        if (focusOn) SetFocus(false);
        var item = Nav.MenuItems.OfType<NavigationViewItem>().FirstOrDefault(i => i.Tag?.ToString() == tag);
        if (item != null) Nav.SelectedItem = item;
    }

    public void OpenLane(string lane) => Select(lane);

    // ---------- Focus mode: just today's three checklists ----------
    bool focusOn;
    void SetFocus(bool on, bool restore = true)
    {
        focusOn = on;
        Nav.IsPaneVisible = !on;
        ExitFocus.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        if (on) PageFrame.Navigate(typeof(FocusPage), null, new DrillInNavigationTransitionInfo());
        else if (restore)
        {
            var sel = Nav.SelectedItem; Nav.SelectedItem = null; Nav.SelectedItem = sel ?? Nav.MenuItems[0];
        }
    }
    void ExitFocus_Click(object sender, RoutedEventArgs e) => SetFocus(false);

    // ---------- Pop-ups ----------
    void OnFinished(string lane)
    {
        int s = Store.Streak(lane);
        Sound.Chime();
        ShowToast($"{Store.LaneNames[lane]} checklist done!", $"Streak: {UI.Plural(s, "day", "days")}. Nice work.");
    }

    public void ShowToast(string title, string text)
    {
        undo = null;
        Toast.ActionButton = null;
        Toast.Severity = InfoBarSeverity.Success;
        Toast.Title = title;
        Toast.Message = text;
        Toast.IsOpen = true;
        AutoHide(4);
    }

    // A pop-up with an Undo button (used when deleting a task)
    public void ShowUndo(string text, Action onUndo)
    {
        undo = onUndo;
        var b = new Button { Content = "Undo" };
        b.Click += (s, e) => { undo?.Invoke(); undo = null; Toast.IsOpen = false; };
        Toast.ActionButton = b;
        Toast.Severity = InfoBarSeverity.Informational;
        Toast.Title = text;
        Toast.Message = "";
        Toast.IsOpen = true;
        AutoHide(5);
    }

    void AutoHide(int seconds)
    {
        toastTimer?.Stop();
        toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(seconds) };
        toastTimer.Tick += (s, a) => { Toast.IsOpen = false; toastTimer?.Stop(); };
        toastTimer.Start();
    }

    void OnSync(bool on) => SyncBadge.Opacity = on ? 1 : 0;

    async void OnKicked()
    {
        Auth.SignOut();
        Store.ClearLocal();
        await new ContentDialog
        {
            Title = "You were logged out",
            Content = "Someone used \"Log out of all devices\" on your account. Log in again to keep going.",
            CloseButtonText = "OK",
            XamlRoot = XamlRoot,
        }.ShowAsync();
        App.Window?.ShowLogin();
    }
}
