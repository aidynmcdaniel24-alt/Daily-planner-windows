using System.Text.Json.Nodes;
using DailyPlanner.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace DailyPlanner.Pages;

// ===== Coding: stats, checklist, daily drill, focus session, learning log, project idea, tips and sites =====
public sealed partial class CodingPage : Page
{
    readonly UI.Layout L = new();
    readonly TextBox entry = new() { PlaceholderText = "Example: finished CS50 week 1" };
    readonly StackPanel log = new();
    readonly TextBlock ideaTitle = UI.T("", 19, true);
    readonly TextBlock ideaDesc = UI.M("", 14);
    readonly Border ideaTag = UI.Pill("");
    Button? builtBtn;
    readonly ContentControl tiles = new() { HorizontalContentAlignment = HorizontalAlignment.Stretch };
    readonly StackPanel drillBox = new(), allDrills = new() { Spacing = 14 };
    readonly TextBlock tipText = UI.T("", 16);
    readonly StackPanel siteList = new(), moreSites = new();
    // focus session
    readonly ProgressRing ring = UI.Ring(170, UI.Lane("coding"));
    readonly TextBlock timeText = new() { FontSize = 36, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontFamily = UI.Mono, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    readonly StackPanel presets = new() { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Center };
    readonly StackPanel chart = new();
    readonly TextBlock todayMins = UI.M("");
    Button? startBtn, stopBtn;
    string drillName = "";

    public CodingPage()
    {
        InitializeComponent();
        Content = L.Root;
        L.Top.Children.Add(UI.PageTitle("Coding", "Code every day", "Small steps toward a career in tech."));
        L.Top.Children.Add(tiles);
        L.Main.Children.Add(new ChecklistCard("coding"));

        // ---- Today's coding drill ----
        var exp = new Expander { Header = "All coding drills", HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch, Content = allDrills };
        drillsExp = exp; exp.Expanding += (s, e) => BuildAllDrills();
        L.Main.Children.Add(UI.Card("\uE7C1", "Today's coding drill", "A small skill to practice today.", UI.Lane("coding"), null, drillBox,
            UI.Two(UI.Btn("I did this", DidDrill, true), UI.Btn("Another drill", () => Bump("cdo")), 8), exp));

        var add = UI.Btn("Add", AddEntry, true);
        var row = new Grid { ColumnSpacing = 8 };
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.Children.Add(entry); Grid.SetColumn(add, 1); row.Children.Add(add);
        entry.KeyDown += (s, e) => { if (e.Key == Windows.System.VirtualKey.Enter) AddEntry(); };
        L.Main.Children.Add(UI.Card("", "Learning log", "What did you learn or build today?", UI.Lane("coding"), null, row, log));

        // ---- Focus session ----
        var ringGrid = new Grid { Width = 170, Height = 170, HorizontalAlignment = HorizontalAlignment.Center };
        ring.Foreground = UI.Lane("coding");
        ringGrid.Children.Add(ring); ringGrid.Children.Add(timeText);
        foreach (var m in new[] { 25, 45, 60 }) { int mm = m; var b = UI.Chip(m + " min", false); b.HorizontalAlignment = HorizontalAlignment.Center; b.Padding = new Thickness(12, 4, 12, 5); b.Tag = m; b.Click += (s, e) => FocusTimer.SetMinutes(mm); presets.Children.Add(b); }
        startBtn = UI.Btn("Start", FocusTimer.StartPause, true); startBtn.HorizontalAlignment = HorizontalAlignment.Stretch;
        stopBtn = UI.Btn("Stop", StopSession); stopBtn.HorizontalAlignment = HorizontalAlignment.Stretch;
        todayMins.HorizontalAlignment = HorizontalAlignment.Center;
        L.Side.Children.Add(UI.Card("\uE916", "Focus session", "Code with no distractions. Minutes count toward your week.", UI.Lane("coding"), null,
            ringGrid, presets, UI.Two(startBtn, stopBtn, 8), chart, todayMins));

        builtBtn = UI.Btn("I built this", Built, true);
        L.Side.Children.Add(UI.Card("", "Today's project idea", null, UI.Lane("coding"), ideaTag, ideaTitle, ideaDesc,
            UI.Row(8, builtBtn, UI.Btn("Another idea", Another))));

        // ---- Tip of the day ----
        var next = UI.Btn("Next tip", () => Bump("cto")); next.HorizontalAlignment = HorizontalAlignment.Left;
        L.Side.Children.Add(UI.Card("\uE734", "Tip of the day", null, UI.Lane("coding"), null, tipText, next));

        // ---- Helpful sites ----
        var more = new Expander { Header = "More sites", HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch, Content = moreSites };
        L.Side.Children.Add(UI.Card("\uE71B", "Helpful sites", "Free places to learn, practice and get unstuck.", UI.Lane("coding"), null, siteList, more));
        DrawSites();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        FocusTimer.Init();
        Store.Changed += Draw; FocusTimer.Changed += DrawTimer;
        Draw(); DrawTimer();
    }
    protected override void OnNavigatedFrom(NavigationEventArgs e) { Store.Changed -= Draw; FocusTimer.Changed -= DrawTimer; }

    static string Path => (Store.St["ob"] as JsonObject)?["cp"]?.ToString() ?? "";
    static bool Fits(List<string> paths) => Path.Length == 0 || paths.Contains("all") || paths.Contains(Path);
    static int DayNum => (int)(DateTime.Today - new DateTime(1970, 1, 1)).TotalDays;
    static int Offset(string key) => Store.St[key] is JsonObject o && o["d"]?.ToString() == Store.Td() ? (int)Store.Num(o["n"]) : 0;
    static void Bump(string key) { Store.St[key] = new JsonObject { ["d"] = Store.Td(), ["n"] = Offset(key) + 1 }; Store.Save(); }

    // ---------- Stat tiles ----------
    void DrawTiles()
    {
        int week = Enumerable.Range(0, 7).Sum(i => FocusTimer.MinsOn(Store.Td(i)));
        var tc = Store.Arr("tc").OfType<JsonObject>().ToList();
        int logs = tc.Count(x => string.CompareOrdinal(x["d"]?.ToString() ?? "", Store.Td(6)) >= 0);
        int built = tc.Count(x => (x["t"]?.ToString() ?? "").StartsWith("Built: "));
        tiles.Content = UI.Wrap(new List<FrameworkElement>
        {
            UI.Tile("Coded this week", week > 0 ? FocusTimer.Hm(week) : "0 min", "From focus sessions"),
            UI.Tile("Coding streak", UI.Plural(Store.Streak("coding"), "day", "days"), "Checklist finished"),
            UI.Tile("Log entries", logs.ToString(), "In the last 7 days"),
            UI.Tile("Projects built", built.ToString(), "All time"),
        }, 160);
    }

    // ---------- Drill ----------
    void DrawDrill()
    {
        var all = GameData.CodeDrills();
        var list = all.Where(d => Fits(d.Paths)).Select(d => d.Drill).ToList();
        if (list.Count == 0) list = all.Select(d => d.Drill).ToList();
        drillBox.Children.Clear(); allDrills.Children.Clear();
        if (list.Count == 0) { drillBox.Children.Add(UI.M("No drills yet.")); return; }
        var today = list[(DayNum + Offset("cdo")) % list.Count];
        drillName = today.Name;
        drillBox.Children.Add(GamingPage.DrillView(today, "coding"));
        otherDrills = list.Where(d => d != today).ToList();
        if (drillsExp?.IsExpanded == true) BuildAllDrills();
    }

    List<Drill> otherDrills = new(); Expander? drillsExp;
    void BuildAllDrills()
    {
        if (allDrills.Children.Count > 0) return;
        foreach (var d in otherDrills) allDrills.Children.Add(GamingPage.DrillView(d, "coding"));
    }

    void DidDrill()
    {
        if (drillName.Length == 0) return;
        Store.Arr("tc").Add(new JsonObject { ["d"] = Store.Td(), ["t"] = "Drill: " + drillName });
        Store.Save();
        ShellPage.Current?.ShowToast("Added to your log", "Nice practice!");
    }

    void DrawTip()
    {
        var tips = GameData.CodeTips();
        tipText.Text = tips.Count == 0 ? "" : tips[(DayNum + Offset("cto")) % tips.Count];
    }

    // ---------- Sites ----------
    void DrawSites()
    {
        var mine = new List<GameData.Site>(); var rest = new List<GameData.Site>();
        foreach (var s in GameData.CodeSites())
        {
            bool fit = Path.Length > 0 ? s.Paths.Contains(Path) : s.Paths.Contains("all");
            (fit ? mine : rest).Add(s);
        }
        foreach (var s in rest.Where(x => x.Paths.Contains("all")).ToList()) { if (mine.Count >= 6) break; mine.Add(s); rest.Remove(s); }
        siteList.Children.Clear(); moreSites.Children.Clear();
        foreach (var s in mine) siteList.Children.Add(SiteRow(s));
        foreach (var s in rest) moreSites.Children.Add(SiteRow(s));
    }

    static UIElement SiteRow(GameData.Site s)
    {
        var g = new Grid { ColumnSpacing = 10 };
        g.ColumnDefinitions.Add(new ColumnDefinition());
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.Children.Add(UI.Stack(1, UI.T(s.Name, 14, true), UI.M(s.Desc, 12)));
        var pill = UI.Pill(s.Group, UI.Muted); pill.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(pill, 1); g.Children.Add(pill);
        var link = new HyperlinkButton { Content = g, NavigateUri = new Uri(s.Url), HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(10, 8, 10, 8), Margin = new Thickness(-10, 0, -10, 0), Foreground = UI.Res("TextFillColorPrimaryBrush") };
        ToolTipService.SetToolTip(link, s.Url);
        return link;
    }

    // ---------- Focus session ----------
    void DrawTimer()
    {
        timeText.Text = FocusTimer.Text;
        ring.Value = 100.0 * FocusTimer.Left / (FocusTimer.Minutes * 60);
        if (startBtn != null) startBtn.Content = FocusTimer.Running ? "Pause" : FocusTimer.Started && FocusTimer.Left > 0 ? "Resume" : "Start";
        if (stopBtn != null) stopBtn.IsEnabled = FocusTimer.Started;
        foreach (var b in presets.Children.OfType<Microsoft.UI.Xaml.Controls.Primitives.ToggleButton>()) b.IsChecked = (int)b.Tag == FocusTimer.Minutes;
    }

    void StopSession()
    {
        int n = FocusTimer.Stop();
        ShellPage.Current?.ShowToast(n > 0 ? "Session saved" : "Session stopped", n > 0 ? FocusTimer.Hm(n) + " added to your week." : "Under a minute isn't counted.");
    }

    void DrawChart()
    {
        var vals = new List<double?>(); var labels = new List<string>();
        for (int i = 6; i >= 0; i--) { vals.Add(FocusTimer.MinsOn(Store.Td(i))); labels.Add(DateTime.Now.AddDays(-i).ToString("dddd")); }
        double max = Math.Max(30, vals.Max(v => v ?? 0));
        chart.Children.Clear();
        var bars = UI.Bars(vals, labels, max, UI.Lane("coding"), v => FocusTimer.Hm((int)v));
        chart.Children.Add(bars);
        int t = FocusTimer.MinsOn(Store.Td());
        todayMins.Text = t > 0 ? $"Today: {FocusTimer.Hm(t)} of focused coding" : "No focus sessions yet today.";
    }

    void AddEntry()
    {
        var v = entry.Text.Trim(); if (v.Length == 0) return;
        Store.Arr("tc").Add(new JsonObject { ["d"] = Store.Td(), ["t"] = v });
        entry.Text = "";
        Store.Save();
    }

    // Each part only redraws when its own data changed
    string logSig = "", tileSig = "", drillSig = "", chartSig = "";
    void Draw()
    {
        var tcArr = Store.Arr("tc");
        string ls = tcArr.Count + "|" + (tcArr.LastOrDefault()?.ToJsonString() ?? "");
        if (ls != logSig) { logSig = ls; DrawLog(); }
        DrawIdea(); DrawTip();
        string ts = ls + "|" + (Store.St["cm"]?.ToJsonString() ?? "") + "|" + Store.Streak("coding") + "|" + Store.Td();
        if (ts != tileSig) { tileSig = ts; DrawTiles(); }
        string dsig = DayNum + "|" + Offset("cdo") + "|" + Path;
        if (dsig != drillSig) { drillSig = dsig; DrawDrill(); }
        string cs = (Store.St["cm"]?.ToJsonString() ?? "") + "|" + Store.Td();
        if (cs != chartSig) { chartSig = cs; DrawChart(); }
    }

    void DrawLog()
    {
        log.Children.Clear();
        var items = Store.Arr("tc").OfType<JsonObject>().TakeLast(8).Reverse().ToList();
        if (items.Count == 0) log.Children.Add(UI.M("Your entries will show here."));
        foreach (var x in items)
        {
            var g = new Grid { Padding = new Thickness(0, 9, 0, 9), ColumnSpacing = 10, BorderBrush = UI.Res("DividerStrokeColorDefaultBrush"), BorderThickness = new Thickness(0, 1, 0, 0) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(56) });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.Children.Add(UI.M(DateTime.TryParse(x["d"]?.ToString(), out var dt) ? dt.ToString("MMM d") : ""));
            var t = UI.T(x["t"]?.ToString() ?? "", 14); Grid.SetColumn(t, 1); g.Children.Add(t);
            log.Children.Add(g);
        }
    }

    // ---------- Project idea (AI ideas from the website if there are any today, else the built-in list) ----------
    (string Type, string Title, string Desc) Current()
    {
        if (Store.St["pj"] is JsonObject pj && pj["d"]?.ToString() == Store.Td() && pj["list"] is JsonArray l && l.Count > 0)
        {
            int i = (int)Store.Num(pj["i"]) % l.Count;
            if (l[i] is JsonArray p) return (p[0]?.ToString() ?? "", p[1]?.ToString() ?? "", p.Count > 2 ? p[2]?.ToString() ?? "" : "");
        }
        string cp = (Store.St["ob"] as JsonObject)?["cp"]?.ToString() ?? "";
        string want = cp == "py" ? "p" : cp == "web" ? "w" : "";
        var L2 = GameData.Projects().Where(p => want.Length == 0 || p.Type == want).ToList();
        if (L2.Count == 0) return ("p", "Build a to-do list app", "Add, check off, and delete tasks.");
        int day = (int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 86400000);
        var pio = Store.St["pio"] as JsonObject;
        int off = pio?["d"]?.ToString() == Store.Td() ? (int)Store.Num(pio["n"]) : 0;
        return L2[(day + off) % L2.Count];
    }

    void DrawIdea()
    {
        var p = Current();
        ideaTitle.Text = p.Title;
        ideaDesc.Text = p.Desc;
        ((TextBlock)ideaTag.Child).Text = p.Type == "p" ? "Python" : "HTML, CSS, JS";
    }

    void Another()
    {
        if (Store.St["pj"] is JsonObject pj && pj["d"]?.ToString() == Store.Td() && pj["list"] is JsonArray l && l.Count > 0)
            pj["i"] = ((int)Store.Num(pj["i"]) + 1) % l.Count;
        else
        {
            var pio = Store.St["pio"] as JsonObject;
            int n = pio?["d"]?.ToString() == Store.Td() ? (int)Store.Num(pio["n"]) : 0;
            Store.St["pio"] = new JsonObject { ["d"] = Store.Td(), ["n"] = n + 1 };
        }
        Store.Save();
    }

    void Built()
    {
        Store.Arr("tc").Add(new JsonObject { ["d"] = Store.Td(), ["t"] = "Built: " + Current().Title });
        Store.Save();
        ShellPage.Current?.ShowToast("Added to your log", "Nice build!");
    }
}
