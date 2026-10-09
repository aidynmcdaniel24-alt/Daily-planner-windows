using System.Text.Json.Nodes;
using DailyPlanner.Services;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;

namespace DailyPlanner.Pages;

// ===== Gaming: checklist, drill guide, break timer, tilt, scores, rank, weekly review =====
public sealed partial class GamingPage : Page
{
    readonly UI.Layout L = new();
    JsonObject G = new();
    // timer
    readonly ProgressRing ring = UI.Ring(170, UI.Lane("gaming"));
    readonly TextBlock timeText = new() { FontSize = 38, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, FontFamily = UI.Mono };
    readonly StackPanel presets = new() { Orientation = Orientation.Horizontal, Spacing = 6, HorizontalAlignment = HorizontalAlignment.Center };
    Button? startBtn;
    // tilt
    readonly StackPanel dots = new() { Orientation = Orientation.Horizontal, Spacing = 6 };
    readonly TextBlock tiltText = UI.M("");
    readonly InfoBar tiltBar = new() { IsOpen = false, IsClosable = true, Severity = InfoBarSeverity.Warning, Title = "Two tilted sessions in a row", Message = "Time for a 10 minute break." };
    // scores / rank
    readonly StackPanel scoreChart = new() { Spacing = 6 };
    readonly TextBox[] scoreBoxes = { new(), new(), new() };
    readonly TextBlock rankNow = UI.T("", 18, true);
    readonly AutoSuggestBox rankBox = new() { Header = "Rank" };
    readonly NumberBox rankPts = new() { SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Hidden };
    readonly StackPanel rankChart = new();
    readonly StackPanel rankHist = new() { Spacing = 0 };
    readonly StackPanel drillBox = new() { Spacing = 8 };
    readonly StackPanel allDrills = new() { Spacing = 14 };
    readonly TextBlock focusLine = UI.M("", 13);

    public GamingPage()
    {
        InitializeComponent();
        Content = L.Root;
        G = GameData.Genre(Store.St);
        L.Top.Children.Add(UI.PageTitle("Gaming", Store.FullDay ? "Full day" : "Busy day", Store.FullDay ? "The longer routine. Practice with a plan, then play." : "The short routine. A little practice still counts."));

        // ---- Checklist + focus goal ----
        var list = new ChecklistCard("gaming");
        list.Extra.Children.Add(focusLine);
        var goal = new TextBox { Header = "Focus goal for today", PlaceholderText = GameData.G(G, "ph"), Text = Store.Obj("fg")[Store.Td()]?.ToString() ?? "" };
        goal.TextChanged += (s, e) => { Store.Obj("fg")[Store.Td()] = goal.Text; Store.Save(); };
        list.Extra.Children.Add(goal);
        L.Main.Children.Add(list);

        // ---- Today's drill ----
        var exp = new Expander { Header = "All drill guides", HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch, Content = allDrills };
        L.Main.Children.Add(UI.Card("", "Today's drill", null, UI.Lane("gaming"), null, drillBox, exp));

        // ---- Score tracker ----
        var labels = GameData.Strings(G["scores"]);
        while (labels.Count < 3) labels.Add("Score " + (labels.Count + 1));
        var today = (Store.St["sc"] as JsonArray)?.OfType<JsonObject>().LastOrDefault(x => x["d"]?.ToString() == Store.Td());
        string[] keys = { "a", "b", "c" };
        for (int i = 0; i < 3; i++) { scoreBoxes[i].Header = labels[i]; scoreBoxes[i].InputScope = new Microsoft.UI.Xaml.Input.InputScope { Names = { new Microsoft.UI.Xaml.Input.InputScopeName(Microsoft.UI.Xaml.Input.InputScopeNameValue.Number) } }; if (today != null) scoreBoxes[i].Text = today[keys[i]]?.ToString() ?? ""; }
        L.Main.Children.Add(UI.Card("", "Score tracker", "Log your numbers and watch your progress.", UI.Lane("gaming"), null,
            UI.Two(scoreBoxes[0], scoreBoxes[1]), scoreBoxes[2], UI.Btn("Save scores", SaveScores), scoreChart));

        // ---- Weekly review ----
        var r1 = new TextBox { Header = "What improved?", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 70, Text = Store.Str("r1") };
        var r2 = new TextBox { Header = "One fix for next week", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 70, Text = Store.Str("r2") };
        r1.TextChanged += (s, e) => { Store.St["r1"] = r1.Text; Store.Save(); };
        r2.TextChanged += (s, e) => { Store.St["r2"] = r2.Text; Store.Save(); };
        L.Main.Children.Add(UI.Card("", "Weekly review", "Look back once a week. That's how you actually improve.", UI.Lane("gaming"), null, r1, r2));

        // ---- Break timer ----
        var ringGrid = new Grid { Width = 170, Height = 170, HorizontalAlignment = HorizontalAlignment.Center };
        ringGrid.Children.Add(ring); ringGrid.Children.Add(timeText);
        ring.Foreground = UI.Lane("gaming");
        foreach (var m in new[] { 5, 10, 15 }) { int mm = m; var b = new ToggleButton { Content = m + " min", CornerRadius = new CornerRadius(99), Padding = new Thickness(12, 4, 12, 5), Tag = m }; b.Click += (s, e) => BreakTimer.SetMinutes(mm); presets.Children.Add(b); }
        startBtn = UI.Btn("Start", BreakTimer.StartPause, true); startBtn.HorizontalAlignment = HorizontalAlignment.Stretch;
        var reset = UI.Btn("Reset", BreakTimer.Reset); reset.HorizontalAlignment = HorizontalAlignment.Stretch;
        L.Side.Children.Add(UI.Card("", "Break timer", "Lost 2 in a row? Step away. No screens.", UI.Lane("gaming"), null, ringGrid, presets, UI.Two(startBtn, reset, 8)));

        // ---- Tilt tracker ----
        var calm = UI.Btn("Stayed calm", () => LogTilt("c")); calm.HorizontalAlignment = HorizontalAlignment.Stretch;
        var tilted = UI.Btn("Tilted", () => LogTilt("t")); tilted.HorizontalAlignment = HorizontalAlignment.Stretch;
        tiltBar.ActionButton = UI.Btn("Start break", () => { tiltBar.IsOpen = false; if (!BreakTimer.Running) BreakTimer.StartPause(); });
        var undo = new HyperlinkButton { Content = "Undo last" }; undo.Click += (s, e) => { var a = Store.Arr("tl"); if (a.Count > 0) { a.RemoveAt(a.Count - 1); Store.Save(); DrawTilt(); } };
        var clear = new HyperlinkButton { Content = "Clear all" };
        clear.Click += async (s, e) =>
        {
            var d = new ContentDialog { Title = "Clear all sessions?", Content = "This removes every calm and tilted session you've logged.", PrimaryButtonText = "Clear", CloseButtonText = "Cancel", XamlRoot = XamlRoot };
            if (await d.ShowAsync() == ContentDialogResult.Primary) { Store.St["tl"] = new JsonArray(); Store.Save(); DrawTilt(); }
        };
        L.Side.Children.Add(UI.Card("", "Tilt tracker", "How did your last session feel?", UI.Lane("gaming"), null, UI.Two(calm, tilted, 8), dots, tiltText, UI.Row(4, undo, clear), tiltBar));

        // ---- Rank tracker ----
        var R = GameData.Ranks(Store.St);
        var opts = R.Tiers.SelectMany(t => R.Div.Count > 0 ? R.Div.Select(d => t + " " + d) : new[] { t }).Concat(R.Top).ToList();
        rankBox.PlaceholderText = opts.Count > 0 ? "Example: " + opts[Math.Min(14, opts.Count - 1)] : "Your rank";
        rankBox.TextChanged += (s, e) => { if (e.Reason == AutoSuggestionBoxTextChangeReason.UserInput) s.ItemsSource = opts.Where(o => o.Contains(s.Text, StringComparison.OrdinalIgnoreCase)).Take(8).ToList(); };
        rankBox.SuggestionChosen += (s, e) => s.Text = e.SelectedItem?.ToString() ?? s.Text;
        rankPts.Header = R.Pts + " (optional)";
        var logRank = UI.Btn("Log rank", LogRank); logRank.HorizontalAlignment = HorizontalAlignment.Stretch;
        L.Side.Children.Add(UI.Card("", "Rank tracker", null, UI.Lane("gaming"), null, rankNow, UI.Two(rankBox, rankPts, 8), logRank, rankChart, rankHist));
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        BreakTimer.Init();
        BreakTimer.Changed += DrawTimer;
        Store.Changed += Refresh;
        Refresh();
        DrawTimer();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        BreakTimer.Changed -= DrawTimer;
        Store.Changed -= Refresh;
    }

    void Refresh()
    {
        G = GameData.Genre(Store.St);
        string foc = Plan.Focus;
        focusLine.Text = GameData.G(G, "focus") + " today: " + foc;
        DrawDrills(foc); DrawTilt(); DrawScores(); DrawRanks();
    }

    // ---------- Drill guide ----------
    void DrawDrills(string foc)
    {
        var today = GameData.DrillFor(G, foc);
        drillBox.Children.Clear(); allDrills.Children.Clear();
        if (today == null) { drillBox.Children.Add(UI.M("No drills for this game type yet.")); return; }
        drillBox.Children.Add(DrillView(today));
        foreach (var d in GameData.Drills(G).Where(d => d.Name != today.Name)) allDrills.Children.Add(DrillView(d));
    }

    internal static UIElement DrillView(Drill d, string lane = "gaming")
    {
        var head = new Grid();
        head.Children.Add(UI.T(d.Name, 17, true));
        var p = UI.Pill(d.Time, UI.Muted); p.HorizontalAlignment = HorizontalAlignment.Right; head.Children.Add(p);
        var s = UI.Stack(8, head);
        for (int i = 0; i < d.Steps.Count; i++)
        {
            var g = new Grid { ColumnSpacing = 10 };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.Children.Add(new Border { Width = 24, Height = 24, CornerRadius = new CornerRadius(12), Background = UI.Tint(UI.Lane(lane), 0.15), VerticalAlignment = VerticalAlignment.Top,
                Child = new TextBlock { Text = (i + 1).ToString(), FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = UI.Lane(lane), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } });
            var t = UI.T(d.Steps[i], 14); Grid.SetColumn(t, 1); g.Children.Add(t);
            s.Children.Add(g);
        }
        if (d.Tip.Length > 0)
            s.Children.Add(new Border { Padding = new Thickness(12, 10, 12, 10), CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(3, 0, 0, 0), BorderBrush = UI.Lane(lane),
                Background = UI.Res("SubtleFillColorSecondaryBrush"), Child = UI.T("Tip: " + d.Tip, 13) });
        return s;
    }

    // ---------- Break timer ----------
    void DrawTimer()
    {
        timeText.Text = BreakTimer.Text;
        ring.Value = 100.0 * BreakTimer.Left / (BreakTimer.Minutes * 60);
        if (startBtn != null) startBtn.Content = BreakTimer.Running ? "Pause" : BreakTimer.Left > 0 && BreakTimer.Left < BreakTimer.Minutes * 60 ? "Resume" : "Start";
        foreach (var b in presets.Children.OfType<ToggleButton>()) b.IsChecked = (int)b.Tag == BreakTimer.Minutes;
    }

    // ---------- Tilt ----------
    void LogTilt(string v)
    {
        Store.Arr("tl").Add(new JsonArray(Store.Td(), v));
        Store.Save(); DrawTilt();
    }

    void DrawTilt()
    {
        var all = Store.Arr("tl").OfType<JsonArray>().Select(x => (d: x[0]?.ToString() ?? "", v: x[1]?.ToString() ?? "")).ToList();
        var week = all.Where(x => string.CompareOrdinal(x.d, Store.Td(6)) >= 0).ToList();
        int c = week.Count(x => x.v == "c");
        tiltText.Text = $"Last 7 days: {c} calm, {week.Count - c} tilted";
        dots.Children.Clear();
        foreach (var x in all.TakeLast(14))
        {
            var e = new Microsoft.UI.Xaml.Shapes.Ellipse { Width = 12, Height = 12, Fill = x.v == "c" ? UI.Res("CodingBrush") : UI.Res("SleepBrush") };
            ToolTipService.SetToolTip(e, x.d + (x.v == "c" ? ": calm" : ": tilted"));
            dots.Children.Add(e);
        }
        if (all.Count == 0) dots.Children.Add(UI.M("No sessions logged yet"));
        var t = all.Where(x => x.d == Store.Td()).ToList();
        tiltBar.IsOpen = t.Count >= 2 && t[^1].v == "t" && t[^2].v == "t";
    }

    // ---------- Scores ----------
    void SaveScores()
    {
        var sc = Store.Arr("sc");
        foreach (var old in sc.OfType<JsonObject>().Where(x => x["d"]?.ToString() == Store.Td()).ToList()) sc.Remove(old);
        double P(int i) => double.TryParse(scoreBoxes[i].Text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.CurrentCulture, out var v) ? v : 0;
        sc.Add(new JsonObject { ["d"] = Store.Td(), ["a"] = P(0), ["b"] = P(1), ["c"] = P(2) });
        Store.Save();
        ShellPage.Current?.ShowToast("Scores saved", "Nice. Keep logging to see your progress.");
    }

    void DrawScores()
    {
        scoreChart.Children.Clear();
        var sc = Store.Arr("sc").OfType<JsonObject>().TakeLast(14).ToList();
        if (sc.Count < 2) { scoreChart.Children.Add(UI.M("Save scores on 2 or more days to see a chart.")); return; }
        var g = new Grid { Height = 80 };
        g.Children.Add(UI.Line(sc.Select(x => Store.Num(x["a"])).ToList(), UI.Brand, 80));
        g.Children.Add(UI.Line(sc.Select(x => Store.Num(x["b"])).ToList(), UI.Res("SleepBrush"), 80));
        scoreChart.Children.Add(g);
        var labels = GameData.Strings(G["scores"]);
        scoreChart.Children.Add(UI.M($"Purple: {(labels.Count > 0 ? labels[0] : "Score 1")}. Orange: {(labels.Count > 1 ? labels[1] : "Score 2")}.", 12));
    }

    // ---------- Rank ----------
    void LogRank()
    {
        var r = rankBox.Text.Trim(); if (r.Length == 0) { rankBox.Focus(FocusState.Programmatic); return; }
        var list = Store.Arr("rkl");
        foreach (var old in list.OfType<JsonObject>().Where(x => x["d"]?.ToString() == Store.Td()).ToList()) list.Remove(old);
        var o = new JsonObject { ["d"] = Store.Td(), ["r"] = r.Length > 40 ? r[..40] : r, ["p"] = double.IsNaN(rankPts.Value) ? null : (JsonNode)Math.Round(rankPts.Value) };
        list.Add(o);
        rankBox.Text = ""; rankPts.Value = double.NaN;
        Store.Save();
        ShellPage.Current?.ShowToast("Rank logged", r);
    }

    void DrawRanks()
    {
        var R = GameData.Ranks(Store.St);
        var L2 = Store.Arr("rkl").OfType<JsonObject>().ToList();
        var last = L2.LastOrDefault();
        rankNow.Text = last == null ? "No rank logged yet" : last["r"] + (last["p"] != null ? " · " + last["p"] + " " + R.Pts : "");
        rankChart.Children.Clear();
        var pts = L2.Where(x => x["p"] != null).TakeLast(14).Select(x => Store.Num(x["p"])).ToList();
        if (pts.Count >= 2) rankChart.Children.Add(UI.Line(pts, UI.Brand, 60));
        rankHist.Children.Clear();
        foreach (var x in L2.TakeLast(6).Reverse())
        {
            var g = new Grid { Padding = new Thickness(0, 8, 0, 8), ColumnSpacing = 10, BorderBrush = UI.Res("DividerStrokeColorDefaultBrush"), BorderThickness = new Thickness(0, 1, 0, 0) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(56) });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.Children.Add(UI.M(DateTime.TryParse(x["d"]?.ToString(), out var dt) ? dt.ToString("MMM d") : ""));
            var name = UI.T(x["r"]?.ToString() ?? "", 14, true); Grid.SetColumn(name, 1); g.Children.Add(name);
            if (x["p"] != null) { var p = UI.M(x["p"] + " " + R.Pts); Grid.SetColumn(p, 2); g.Children.Add(p); }
            rankHist.Children.Add(g);
        }
    }
}
