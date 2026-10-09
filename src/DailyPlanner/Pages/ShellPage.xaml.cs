using System.Diagnostics;
using DailyPlanner.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
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
        BuildSidebar();
        SizeChanged += (s, e) => SetCompact(e.NewSize.Width < 900);
        // "--page gaming" opens a page directly (used by the automatic screenshot test)
        var args = Environment.GetCommandLineArgs();
        int pi = Array.IndexOf(args, "--page");
        Navigate(pi > 0 && pi + 1 < args.Length ? args[pi + 1] : "home");

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
        Store.Changed += DrawNav;
        Theme.Changed += OnTheme;
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
        Store.Changed -= DrawNav;
        Theme.Changed -= OnTheme;
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

    // ---------- Sidebar ----------
    record NavItem(string Tag, string Glyph, string Label, string? Lane);
    static readonly NavItem[] MainItems =
    {
        new("home", "\uE80F", "Home", null),
        new("gaming", "\uE7FC", "Gaming", "gaming"),
        new("sleep", "\uE708", "Sleep", "sleep"),
        new("coding", "\uE943", "Coding", "coding"),
        new("summary", "\uE9D2", "Summary", null),
        new("board", "\uE734", "Leaderboard", null),
    };
    static readonly NavItem[] FootItems =
    {
        new("focus", "\uF272", "Focus mode", null),
        new("web", "\uE774", "Open the website", null),
        new("settings", "\uE713", "Settings", null),
    };

    sealed class NavView
    {
        public required NavItem Item;
        public required Button Btn;
        public required Border Bar, IconBox;
        public required FontIcon Icon;
        public required TextBlock Label;
        public TextBlock? Badge;
    }
    readonly List<NavView> navViews = new();
    readonly List<FrameworkElement> wideOnly = new();
    TextBlock? todayPct; Border? todayFill; Grid? todayTrack; TextBlock? profName, profSub, avatarText;
    string current = "";
    bool compact;

    Brush Accent(NavItem i) => i.Lane != null ? UI.Lane(i.Lane) : UI.Brand;

    void BuildSidebar()
    {
        navViews.Clear(); wideOnly.Clear();
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Profile
        avatarText = new TextBlock { FontSize = 15, FontWeight = Microsoft.UI.Text.FontWeights.Bold, Foreground = new SolidColorBrush(Microsoft.UI.Colors.White), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var grad = new LinearGradientBrush { StartPoint = new Windows.Foundation.Point(0, 0), EndPoint = new Windows.Foundation.Point(1, 1) };
        grad.GradientStops.Add(new GradientStop { Color = Windows.UI.Color.FromArgb(255, 0x8B, 0x7D, 0xFF), Offset = 0 });
        grad.GradientStops.Add(new GradientStop { Color = Windows.UI.Color.FromArgb(255, 0x5B, 0x4A, 0xE6), Offset = 1 });
        var avatar = new Border { Width = 38, Height = 38, CornerRadius = new CornerRadius(12), Background = grad, Child = avatarText };
        profName = UI.T("", 14, true, null, false); profName.TextTrimming = TextTrimming.CharacterEllipsis;
        profSub = UI.M("", 12); profSub.TextWrapping = TextWrapping.NoWrap; profSub.TextTrimming = TextTrimming.CharacterEllipsis;
        var names = UI.Stack(0, profName, profSub); names.VerticalAlignment = VerticalAlignment.Center;
        wideOnly.Add(names);
        var prof = new Grid { ColumnSpacing = 10 };
        prof.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        prof.ColumnDefinitions.Add(new ColumnDefinition());
        prof.Children.Add(avatar); Grid.SetColumn(names, 1); prof.Children.Add(names);
        var profBtn = Plain(prof, () => Navigate("settings"));
        profBtn.Padding = new Thickness(6);
        ToolTipService.SetToolTip(profBtn, "Your account and settings");
        var menuLabel = new TextBlock { Text = "MENU", FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, CharacterSpacing = 120, Foreground = UI.Res("TextFillColorTertiaryBrush"), FontFamily = (FontFamily)Application.Current.Resources["GeistMono"], Margin = new Thickness(12, 18, 0, 6) };
        wideOnly.Add(menuLabel);
        var top = UI.Stack(2, profBtn, menuLabel);
        foreach (var it in MainItems) top.Children.Add(Item(it, true));
        root.Children.Add(top);

        // Bottom: today's progress, then focus / website / settings
        todayPct = UI.T("", 13, true);
        todayTrack = new Grid { Height = 6, CornerRadius = new CornerRadius(3), Background = UI.Res("Line2Brush") };
        todayFill = new Border { HorizontalAlignment = HorizontalAlignment.Left, CornerRadius = new CornerRadius(3), Background = UI.Brand, Width = 0 };
        todayTrack.Children.Add(todayFill);
        todayTrack.SizeChanged += (s, e) => DrawNav();
        var todayHead = new Grid();
        todayHead.Children.Add(UI.M("Today", 12));
        todayPct.HorizontalAlignment = HorizontalAlignment.Right; todayHead.Children.Add(todayPct);
        var today = new Border { Padding = new Thickness(12, 10, 12, 12), CornerRadius = new CornerRadius(12), Background = UI.Res("PanelBrush"), BorderBrush = UI.Res("LineBrush"), BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 0, 8), Child = UI.Stack(8, todayHead, todayTrack) };
        wideOnly.Add(today);
        var bottom = UI.Stack(2, today);
        foreach (var it in FootItems) bottom.Children.Add(Item(it, false));
        Grid.SetRow(bottom, 2); root.Children.Add(bottom);

        Sidebar.Child = root;
        SetCompact(compact, true);
        DrawNav();
    }

    static Button Plain(UIElement content, Action click)
    {
        var b = new Button
        {
            Content = content, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent), BorderThickness = new Thickness(0), CornerRadius = new CornerRadius(10), Padding = new Thickness(8, 7, 10, 7),
        };
        b.Resources["ButtonBackgroundPointerOver"] = UI.Res("HoverBrush");
        b.Resources["ButtonBackgroundPressed"] = UI.Res("SunkBrush");
        b.Resources["ButtonBorderBrushPointerOver"] = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        b.Resources["ButtonBorderBrushPressed"] = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        b.Click += (s, e) => click();
        return b;
    }

    UIElement Item(NavItem it, bool main)
    {
        var icon = new FontIcon { Glyph = it.Glyph, FontSize = 15 };
        var iconBox = new Border { Width = 30, Height = 30, CornerRadius = new CornerRadius(9), Child = icon };
        var label = UI.T(it.Label, 14, false, null, false); label.VerticalAlignment = VerticalAlignment.Center;
        var g = new Grid { ColumnSpacing = 11 };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.ColumnDefinitions.Add(new ColumnDefinition());
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.Children.Add(iconBox); Grid.SetColumn(label, 1); g.Children.Add(label);
        wideOnly.Add(label);
        TextBlock? badge = null;
        if (it.Lane != null)
        {
            badge = new TextBlock { FontSize = 11, FontFamily = (FontFamily)Application.Current.Resources["GeistMono"], VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(badge, 2); g.Children.Add(badge);
            wideOnly.Add(badge);
        }
        var btn = Plain(g, () => Navigate(it.Tag));
        if (!main) btn.Padding = new Thickness(8, 4, 10, 4);
        ToolTipService.SetToolTip(btn, it.Label);
        var bar = new Border { Width = 3, Height = 18, CornerRadius = new CornerRadius(2), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(-10, 0, 0, 0), Background = Accent(it), Opacity = 0 };
        var holder = new Grid(); holder.Children.Add(btn); holder.Children.Add(bar);
        navViews.Add(new NavView { Item = it, Btn = btn, Bar = bar, IconBox = iconBox, Icon = icon, Label = label, Badge = badge });
        return holder;
    }

    void DrawNav()
    {
        if (profName == null) return;
        string nm = Store.Name;
        profName.Text = nm.Length > 0 ? nm : "Welcome";
        profSub!.Text = Auth.Current != null ? "Synced with your account" : "Guest · saved on this PC";
        avatarText!.Text = nm.Length > 0 ? nm[..1].ToUpperInvariant() : "\u263A";

        foreach (var v in navViews)
        {
            bool on = v.Item.Tag == current;
            var acc = Accent(v.Item);
            v.Btn.Background = on ? UI.Res("NavActiveBrush") : new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            v.Bar.Opacity = on ? 1 : 0;
            v.IconBox.Background = on ? UI.Tint(acc, 0.18) : new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            v.Icon.Foreground = on ? acc : UI.Muted;
            v.Label.Foreground = on ? UI.Res("TextFillColorPrimaryBrush") : UI.Muted;
            v.Label.FontWeight = on ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;
            if (v.Badge != null && v.Item.Lane != null)
            {
                var (k, n) = Store.Count(v.Item.Lane);
                bool done = n > 0 && k >= n;
                v.Badge.Text = done ? "\u2713" : $"{k}/{n}";
                v.Badge.Foreground = done ? acc : UI.Res("TextFillColorTertiaryBrush");
                v.Badge.FontWeight = done ? Microsoft.UI.Text.FontWeights.Bold : Microsoft.UI.Text.FontWeights.Normal;
            }
        }
        int tot = 0, dn = 0;
        foreach (var l in Store.Lanes) { var (k, n) = Store.Count(l); tot += n; dn += Math.Min(k, n); }
        double pct = tot == 0 ? 0 : (double)dn / tot;
        if (todayPct != null) todayPct.Text = Math.Round(pct * 100) + "%";
        if (todayFill != null && todayTrack != null) todayFill.Width = Math.Max(0, todayTrack.ActualWidth * pct);
    }

    void SetCompact(bool on, bool force = false)
    {
        if (on == compact && !force) return;
        compact = on;
        Sidebar.Width = on ? 74 : 236;
        foreach (var e in wideOnly) e.Visibility = on ? Visibility.Collapsed : Visibility.Visible;
        foreach (var v in navViews) v.Btn.HorizontalContentAlignment = on ? HorizontalAlignment.Center : HorizontalAlignment.Stretch;
    }

    void OnTheme()
    {
        BuildSidebar();
        Navigate(current, true);
    }

    // ---------- Navigation ----------
    void Navigate(string tag, bool force = false)
    {
        switch (tag)
        {
            case "web": Process.Start(new ProcessStartInfo(Config.Website + "home/") { UseShellExecute = true }); return;
            case "focus": SetFocus(true); return;
        }
        if (focusOn) SetFocus(false, false);
        if (tag == current && !force) return;
        Type? page = tag switch
        {
            "home" => typeof(HomePage), "gaming" => typeof(GamingPage), "sleep" => typeof(SleepPage), "coding" => typeof(CodingPage),
            "summary" => typeof(SummaryPage), "board" => typeof(LeaderboardPage), "settings" => typeof(SettingsPage), _ => null,
        };
        if (page == null) { tag = "home"; page = typeof(HomePage); }
        current = tag;
        PageFrame.Navigate(page, null, new EntranceNavigationTransitionInfo());
        DrawNav();
    }

    void Select(string tag) => Navigate(tag);

    public void OpenLane(string lane) => Select(lane);

    // ---------- Focus mode: just today's three checklists ----------
    bool focusOn;
    void SetFocus(bool on, bool restore = true)
    {
        focusOn = on;
        Sidebar.Visibility = on ? Visibility.Collapsed : Visibility.Visible;
        ExitFocus.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        if (on) PageFrame.Navigate(typeof(FocusPage), null, new DrillInNavigationTransitionInfo());
        else if (restore) Navigate(current.Length > 0 ? current : "home", true);
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

    void OnSync(bool on) { SyncBadge.Opacity = on ? 1 : 0; DrawNav(); }

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
