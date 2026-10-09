using System.Diagnostics;
using DailyPlanner.Services;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using ContentData = DailyPlanner.Services.Content;

namespace DailyPlanner.Pages;

// ===== Home: greeting, next task, today's progress, quote =====
public sealed partial class HomePage : Page
{
    readonly StackPanel root = new() { Spacing = 18, MaxWidth = 1180 };
    readonly TextBlock date = UI.Eyebrow(""), hello = UI.T("", 38, true), sub = UI.M("", 15);
    readonly StackPanel goals = new() { Orientation = Orientation.Horizontal, Spacing = 6 };
    readonly InfoBar setupBar = Bar("Make the plan yours", "Answer a few setup questions to get drills for your game and your own bedtime routine.", InfoBarSeverity.Informational);
    readonly InfoBar verifyBar = Bar("Please verify your email", "Check your inbox for the link.", InfoBarSeverity.Warning);
    readonly InfoBar nudgeBar = Bar("", "", InfoBarSeverity.Success);
    readonly InfoBar bedBar = Bar("", "", InfoBarSeverity.Informational);
    readonly Grid top = new() { ColumnSpacing = 18, RowSpacing = 18 };
    readonly Border nextCard = new() { CornerRadius = new CornerRadius(18), Padding = new Thickness(22), BorderThickness = new Thickness(1) };
    readonly TextBlock nextLabel = UI.Eyebrow("Next up"), nextTitle = UI.T("", 22, true), nextNote = UI.M("");
    readonly Button nextDone;
    readonly Border todayCard;
    readonly ProgressRing allRing = UI.Ring(116, UI.Brand);
    readonly TextBlock allPct = new() { FontSize = 28, FontWeight = FontWeights.Bold, CharacterSpacing = -20, HorizontalAlignment = HorizontalAlignment.Center };
    readonly StackPanel lanes = new() { Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
    readonly TextBlock quote = UI.T("", 18), quoteBy = UI.M("");
    readonly StackPanel typeRow = new() { Orientation = Orientation.Horizontal, Spacing = 4 }, moodRow = new() { Orientation = Orientation.Horizontal, Spacing = 6 };
    (string lane, int index)? next;
    int shift;
    static bool newsShown;

    static InfoBar Bar(string title, string msg, InfoBarSeverity sev) => new() { Title = title, Message = msg, Severity = sev, IsOpen = false, IsClosable = true, Visibility = Visibility.Collapsed };
    static void SetBar(InfoBar b, bool open) { b.IsOpen = open; b.Visibility = open ? Visibility.Visible : Visibility.Collapsed; }

    public HomePage()
    {
        InitializeComponent();
        hello.FontWeight = FontWeights.Bold; hello.CharacterSpacing = -25;
        root.ChildrenTransitions = new Microsoft.UI.Xaml.Media.Animation.TransitionCollection { new Microsoft.UI.Xaml.Media.Animation.EntranceThemeTransition { IsStaggeringEnabled = true } };
        foreach (var b in new[] { setupBar, verifyBar, nudgeBar, bedBar }) b.Closed += (s, e) => { s.Visibility = Visibility.Collapsed; s.Tag = "closed"; };
        setupBar.ActionButton = UI.Btn("Open setup", () => { setupBar.Tag = "seen"; Process.Start(new ProcessStartInfo(Config.Website + "setup/") { UseShellExecute = true }); });
        verifyBar.ActionButton = UI.Btn("Resend email", async () =>
        {
            try { await Auth.SendVerifyEmail(); verifyBar.Message = "Sent! Check your inbox (and spam)."; }
            catch (Exception ex) { verifyBar.Message = Auth.Friendly(ex); }
        });

        root.Children.Add(UI.Stack(4, date, hello, sub, goals));
        root.Children.Add(nudgeBar); root.Children.Add(verifyBar); root.Children.Add(setupBar); root.Children.Add(bedBar);

        // Next up card
        nextCard.BorderBrush = UI.Res("AccentFillColorDefaultBrush");
        nextCard.Background = new LinearGradientBrush
        {
            StartPoint = new Windows.Foundation.Point(0, 0), EndPoint = new Windows.Foundation.Point(1, 1), Opacity = 0.22,
            GradientStops = { new GradientStop { Color = Windows.UI.Color.FromArgb(255, 0x6D, 0x5C, 0xFF), Offset = 0 }, new GradientStop { Color = Microsoft.UI.Colors.Transparent, Offset = 0.8 } },
        };
        nextDone = UI.Btn("Done", () => { if (next is { } n) Store.SetDone(n.lane, n.index, true); }, true, "");
        nextDone.VerticalAlignment = VerticalAlignment.Center;
        var ng = new Grid { ColumnSpacing = 16 };
        ng.ColumnDefinitions.Add(new ColumnDefinition()); ng.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var nt = UI.Stack(4, nextLabel, nextTitle, nextNote); nt.VerticalAlignment = VerticalAlignment.Center;
        ng.Children.Add(nt); Grid.SetColumn(nextDone, 1); ng.Children.Add(nextDone);
        nextCard.Child = ng;

        // Today card
        var ringGrid = new Grid { Width = 116, Height = 116, VerticalAlignment = VerticalAlignment.Center };
        ringGrid.Children.Add(allRing);
        var pctStack = UI.Stack(0, allPct, UI.M("today", 12)); pctStack.HorizontalAlignment = HorizontalAlignment.Center; pctStack.VerticalAlignment = VerticalAlignment.Center;
        ((TextBlock)pctStack.Children[1]).HorizontalAlignment = HorizontalAlignment.Center;
        ringGrid.Children.Add(pctStack);
        var tg = new Grid { ColumnSpacing = 20 };
        tg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); tg.ColumnDefinitions.Add(new ColumnDefinition());
        tg.Children.Add(ringGrid); Grid.SetColumn(lanes, 1); tg.Children.Add(lanes);
        todayCard = new Border { Background = UI.Res("CardBackgroundFillColorDefaultBrush"), BorderBrush = UI.Res("CardStrokeColorDefaultBrush"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(18), Padding = new Thickness(20), Child = tg };

        top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) });
        top.ColumnDefinitions.Add(new ColumnDefinition());
        top.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); top.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        top.Children.Add(nextCard); top.Children.Add(todayCard);
        root.Children.Add(top);

        // Quote card
        foreach (var (k, label) in new[] { ("mix", "Mix"), ("moti", "Motivation"), ("faith", "Faith") })
        {
            var b = UI.Chip(label, false); b.Tag = k; b.HorizontalAlignment = HorizontalAlignment.Left; b.Padding = new Thickness(12, 4, 12, 5);
            b.Click += (s, e) => { Store.St["qm"] = k; Store.Save(); };
            typeRow.Children.Add(b);
        }
        foreach (var (k, label) in new[] { ("any", "Any"), ("focus", "Focus"), ("loss", "After a loss"), ("tired", "Tired") })
        {
            var b = UI.Chip(label, false); b.Tag = k; b.HorizontalAlignment = HorizontalAlignment.Left; b.CornerRadius = new CornerRadius(99); b.Padding = new Thickness(12, 3, 12, 4);
            b.Click += (s, e) => { Store.St["qmo"] = k; shift = 0; Store.Save(); };
            moodRow.Children.Add(b);
        }
        var refresh = UI.IconBtn("", "New quote", () => { shift++; DrawQuote(); });
        var qhead = new Grid(); qhead.Children.Add(typeRow); refresh.HorizontalAlignment = HorizontalAlignment.Right; qhead.Children.Add(refresh);
        var moodLine = UI.Row(10, UI.M("Mood"), moodRow); ((TextBlock)moodLine.Children[0]).VerticalAlignment = VerticalAlignment.Center;
        root.Children.Add(new Border
        {
            Background = UI.Res("CardBackgroundFillColorDefaultBrush"), BorderBrush = UI.Res("CardStrokeColorDefaultBrush"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(18), Padding = new Thickness(20),
            Child = UI.Stack(10, qhead, quote, quoteBy, new ScrollViewer { Content = moodLine, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, HorizontalScrollMode = ScrollMode.Auto, VerticalScrollMode = ScrollMode.Disabled }),
        });

        Content = new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        SizeChanged += (s, e) => Fit(e.NewSize.Width);
    }

    void Fit(double w)
    {
        bool narrow = w < 860;
        Grid.SetColumn(todayCard, narrow ? 0 : 1); Grid.SetRow(todayCard, narrow ? 1 : 0);
        Grid.SetColumnSpan(nextCard, narrow ? 2 : 1); Grid.SetColumnSpan(todayCard, narrow ? 2 : 1);
        root.Padding = narrow ? new Thickness(18, 20, 18, 28) : new Thickness(36, 28, 36, 36);
        hello.FontSize = narrow ? 30 : 38;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        Store.Changed += Draw;
        Draw();
        _ = Extras();
    }
    protected override void OnNavigatedFrom(NavigationEventArgs e) => Store.Changed -= Draw;

    static DateTime lastExtras = DateTime.MinValue;
    async Task Extras()
    {
        // these need the internet, so only check every few minutes (not every time Home opens)
        if ((DateTime.Now - lastExtras).TotalMinutes < 5) { DrawQuote(); return; }
        lastExtras = DateTime.Now;
        if (Auth.Current != null)
        {
            await Auth.RefreshProfile();
            SetBar(verifyBar, Auth.Current is { UsesPassword: true, Verified: false });
            try
            {
                var names = await Board.TakeNudges();
                if (names.Count > 0)
                {
                    string who = names.Count == 1 ? names[0] : names.Count == 2 ? names[0] + " and " + names[1] : names[0] + " and " + (names.Count - 1) + " others";
                    nudgeBar.Title = who + " nudged you"; nudgeBar.Message = "Keep your streak alive today!";
                    SetBar(nudgeBar, true);
                }
            }
            catch { }
        }
        await ContentData.Refresh();
        DrawQuote();
        WhatsNew();
    }

    // "What's new" pop-up, once per version (shared with the website)
    async void WhatsNew()
    {
        if (newsShown || ContentData.NewsItems.Count == 0) return;
        string seen = Store.Str("seenv");
        if (seen.Length == 0) { Store.St["seenv"] = ContentData.NewsVersion; Store.Save(); return; }
        if (seen == ContentData.NewsVersion) return;
        newsShown = true;
        Store.St["seenv"] = ContentData.NewsVersion; Store.Save();
        var list = UI.Stack(8);
        foreach (var n in ContentData.NewsItems) list.Children.Add(UI.Row(8, UI.T("•", 14, true, UI.Brand), UI.T(n, 14)));
        await new ContentDialog
        {
            Title = "What's new", PrimaryButtonText = "Got it", DefaultButton = ContentDialogButton.Primary, XamlRoot = XamlRoot,
            Content = new ScrollViewer { Content = UI.Stack(10, UI.M("Version " + ContentData.NewsVersion), list), MaxHeight = 420 },
        }.Themed().ShowAsync();
    }

    void Draw()
    {
        int h = DateTime.Now.Hour;
        string part = h < 5 ? "Up late" : h < 12 ? "Good morning" : h < 17 ? "Good afternoon" : "Good evening";
        hello.Text = part + (Store.Name.Length > 0 ? ", " + Store.Name : "");
        date.Text = DateTime.Now.ToString("dddd, MMMM d").ToUpperInvariant();
        sub.Text = h < 5 ? "Get some sleep. Your plan will be here tomorrow." : "Here's your plan for today.";
        goals.Children.Clear();
        foreach (var g in Store.Goals) goals.Children.Add(UI.Pill(g));
        SetBar(setupBar, !Store.SetUp && setupBar.Tag == null);

        // Rings
        int all = 0, done = 0;
        lanes.Children.Clear();
        foreach (var lane in Store.Lanes)
        {
            var (k, n) = Store.Count(lane); all += n; done += k;
            lanes.Children.Add(LaneRow(lane, k, n));
        }
        int pct = all == 0 ? 0 : (int)Math.Round(100.0 * done / all);
        allRing.Value = pct; allPct.Text = pct + "%";

        // Next up: gaming, then coding, then sleep
        next = null;
        foreach (var lane in new[] { "gaming", "coding", "sleep" })
        {
            var item = Store.Items(lane).FirstOrDefault(t => !t.Done);
            if (item != null) { next = (lane, item.Index); nextTitle.Text = item.Title; nextNote.Text = Store.LaneNames[lane] + (item.Note.Length > 0 ? " · " + item.Note : ""); break; }
        }
        nextLabel.Text = next == null ? "ALL DONE" : "NEXT UP";
        if (next == null) { nextTitle.Text = "Every checklist is finished for today."; nextNote.Text = "Nice work. Rest up and come back tomorrow."; }
        nextDone.Visibility = next == null ? Visibility.Collapsed : Visibility.Visible;

        // Bedtime countdown
        var bt = (Store.St["ob"] as System.Text.Json.Nodes.JsonObject)?["bt"]?.ToString() ?? (Store.St["rm"] as System.Text.Json.Nodes.JsonObject)?["b"]?.ToString();
        bool show = false;
        if (TimeSpan.TryParse(bt, out var t))
        {
            int diff = (int)Math.Round((DateTime.Today.Add(t) - DateTime.Now).TotalMinutes); if (diff < -360) diff += 1440;
            if (diff is > 0 and <= 240) { bedBar.Title = diff > 60 ? $"Bedtime in {diff / 60}h {diff % 60}m" : $"Start winding down. Bedtime in {diff} min"; show = true; }
            else if (diff is <= 0 and > -180) { bedBar.Title = "It's past your bedtime"; show = true; }
        }
        SetBar(bedBar, show && bedBar.Tag == null);

        DrawQuote();
    }

    UIElement LaneRow(string lane, int k, int n)
    {
        var color = UI.Lane(lane); int streak = Store.Streak(lane);
        string[] icons = { "", "", "" };
        var ring = new Grid { Width = 38, Height = 38 };
        ring.Children.Add(new ProgressRing { IsIndeterminate = false, Width = 38, Height = 38, Maximum = 100, Value = n == 0 ? 0 : 100.0 * k / n, Foreground = color, Background = UI.Res("Line2Brush") });
        ring.Children.Add(new FontIcon { Glyph = icons[Array.IndexOf(Store.Lanes, lane)], FontSize = 15, Foreground = color, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
        var text = UI.Stack(0, UI.T(Store.LaneNames[lane], 14, true), UI.T(n > 0 && k == n ? "All done" : $"{k}/{n} done", 12, false, n > 0 && k == n ? UI.Res("CodingBrush") : UI.Muted));
        text.VerticalAlignment = VerticalAlignment.Center;
        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(ring); Grid.SetColumn(text, 1); grid.Children.Add(text);
        if (streak > 0) { var sk = UI.T("\U0001F525 " + streak, 13, true, UI.Res("StreakBrush")); sk.VerticalAlignment = VerticalAlignment.Center; ToolTipService.SetToolTip(sk, UI.Plural(streak, "day", "days") + " streak"); Grid.SetColumn(sk, 2); grid.Children.Add(sk); }
        var b = new Button { Content = grid, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch, Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent), BorderThickness = new Thickness(0), Padding = new Thickness(6) };
        b.Click += (s, e) => ShellPage.Current?.OpenLane(lane);
        return b;
    }

    async void DrawQuote()
    {
        string type = Store.Str("qm"); if (type.Length == 0) type = "mix";
        string mood = Store.Str("qmo"); if (mood.Length == 0) mood = "any";
        foreach (var b in typeRow.Children.OfType<ToggleButton>()) b.IsChecked = (string)b.Tag == type;
        foreach (var b in moodRow.Children.OfType<ToggleButton>()) b.IsChecked = (string)b.Tag == mood;
        int s = shift + (int)Store.Num(Store.St["qsh"]);
        var q = ContentData.Pick(type, mood, s);
        quoteBy.Text = "— " + q.By;
        if (q.VerseRef != null && q.Text.Length == 0)
        {
            quote.Text = "Loading verse…";
            if (await ContentData.LoadVerse(q.VerseRef)) quote.Text = "“" + ContentData.VerseText(q.VerseRef) + "”";
            else quote.Text = q.VerseRef;
        }
        else quote.Text = "“" + q.Text + "”";
    }
}
