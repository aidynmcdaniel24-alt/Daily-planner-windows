using System.Text.Json.Nodes;
using DailyPlanner.Services;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace DailyPlanner.Pages;

// ===== Summary: stats, charts, calendar, notes, challenges, badges =====
public sealed class SummaryPage : Page
{
    readonly UI.Layout L = new();
    readonly Grid tiles = new() { ColumnSpacing = 12, RowSpacing = 12 };
    readonly StackPanel charts = new() { Spacing = 14 };
    readonly StackPanel cal = new() { Spacing = 8 };
    readonly TextBlock monthTitle = UI.T("", 16, true);
    readonly TextBox note = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 90, PlaceholderText = "Example: Held my angles better. Still panic when third-partied." };
    readonly StackPanel past = new();
    readonly StackPanel challenges = new() { Spacing = 12 };
    readonly TextBlock chSub = UI.M("");
    readonly StackPanel badges = new() { Spacing = 6 };
    readonly StackPanel week = new() { Spacing = 0 };
    int monthOffset;
    DispatcherTimer? noteTimer;

    public SummaryPage()
    {
        Content = L.Root;
        L.Top.Children.Add(UI.PageTitle("Summary", "Your week", "How you're doing across gaming, sleep and coding."));
        L.Top.Children.Add(tiles);

        L.Main.Children.Add(UI.Card("", "Last 7 days", null, null, null, charts));
        var prev = UI.IconBtn("", "Previous month", () => { monthOffset--; DrawCalendar(); });
        var next = UI.IconBtn("", "Next month", () => { if (monthOffset < 0) { monthOffset++; DrawCalendar(); } });
        L.Main.Children.Add(UI.Card("", "Calendar", "A dot for each checklist you finished.", null, UI.Row(4, prev, next), monthTitle, cal,
            UI.Row(14, Legend("gaming", "Gaming"), Legend("sleep", "Sleep"), Legend("coding", "Coding"))));
        note.TextChanged += (s, e) =>
        {
            noteTimer?.Stop();
            noteTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
            noteTimer.Tick += (a, b) =>
            {
                noteTimer?.Stop();
                var nt = Store.Obj("nt");
                if (note.Text.Trim().Length == 0) nt.Remove(Store.Td()); else nt[Store.Td()] = note.Text;
                Store.Save();
            };
            noteTimer.Start();
        };
        L.Main.Children.Add(UI.Card("", "Today's note", "What went well? What will you change?", null, null, note, past));

        L.Side.Children.Add(UI.Card("", "Weekly challenges", null, null, null, chSub, challenges));
        L.Side.Children.Add(UI.Card("", "Badges", null, null, null, badges));
        L.Side.Children.Add(UI.Card("", "This week", null, null, null, week));
    }

    static UIElement Legend(string lane, string name) =>
        UI.Row(6, new Microsoft.UI.Xaml.Shapes.Ellipse { Width = 8, Height = 8, Fill = UI.Lane(lane), VerticalAlignment = VerticalAlignment.Center }, UI.M(name, 12));

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        note.Text = (Store.St["nt"] as JsonObject)?[Store.Td()]?.ToString() ?? "";
        Draw();
        Store.Changed += Draw;
    }
    protected override void OnNavigatedFrom(NavigationEventArgs e) => Store.Changed -= Draw;

    void Draw()
    {
        DrawTiles(); DrawCharts(); DrawCalendar(); DrawNotes(); DrawChallenges(); DrawBadges(); DrawWeek();
    }

    void DrawTiles()
    {
        var hs = Enumerable.Range(0, 7).Select(i => Stats.SleepOn(Store.Td(i))).Where(h => h != null).Select(h => h!.Value).ToList();
        var s = Stats.Sessions.Where(x => string.CompareOrdinal(x.d, Store.Td(6)) >= 0).ToList();
        int calm = s.Count(x => x.v == "c"), best = Stats.BestStreak;
        var T = new List<(string, string, string)>
        {
            ("PERFECT DAYS", Stats.PerfectDays + "/7", "All three checklists done"),
            ("BEST STREAK", UI.Plural(best, "day", "days"), "Your longest current run"),
            ("AVG SLEEP", hs.Count > 0 ? hs.Average().ToString("0.0") + " h" : "–", "Over the last 7 nights"),
            ("CALM SESSIONS", s.Count > 0 ? Math.Round(100.0 * calm / s.Count) + "%" : "–", s.Count > 0 ? $"{calm} of {s.Count} this week" : "Log sessions to see this"),
            ("CODING LOGS", Store.Arr("tc").Count.ToString(), "Everything you've logged"),
        };
        tiles.Children.Clear(); tiles.ColumnDefinitions.Clear();
        for (int i = 0; i < T.Count; i++)
        {
            tiles.ColumnDefinitions.Add(new ColumnDefinition());
            var b = new Border
            {
                Background = UI.Res("CardBackgroundFillColorDefaultBrush"), BorderBrush = UI.Res("CardStrokeColorDefaultBrush"), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14), Padding = new Thickness(16),
                Child = UI.Stack(2, UI.T(T[i].Item1, 11, true, UI.Muted), UI.T(T[i].Item2, 26, true), UI.M(T[i].Item3, 12)),
            };
            Grid.SetColumn(b, i); tiles.Children.Add(b);
        }
    }

    void DrawCharts()
    {
        var labels = new List<string>(); var done = new List<double?>(); var sleep = new List<double?>();
        for (int i = 6; i >= 0; i--) { labels.Add(DateTime.Now.AddDays(-i).ToString("dddd")); done.Add(Stats.DonePercent(Store.Td(i))); sleep.Add(Stats.SleepOn(Store.Td(i))); }
        charts.Children.Clear();
        charts.Children.Add(UI.T("Tasks done each day", 13, true, UI.Muted));
        charts.Children.Add(UI.Bars(done, labels, 100, UI.Brand, v => v + "%"));
        charts.Children.Add(UI.T("Hours of sleep", 13, true, UI.Muted));
        charts.Children.Add(UI.Bars(sleep, labels, 10, UI.Lane("sleep"), v => v.ToString("0.0") + " h", 7, "7 h goal"));
    }

    void DrawCalendar()
    {
        var first = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(monthOffset);
        monthTitle.Text = first.ToString("MMMM yyyy");
        var grid = new Grid { ColumnSpacing = 4, RowSpacing = 4 };
        for (int c = 0; c < 7; c++) grid.ColumnDefinitions.Add(new ColumnDefinition());
        string[] dows = { "M", "T", "W", "T", "F", "S", "S" };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (int c = 0; c < 7; c++) { var t = UI.M(dows[c], 12); t.HorizontalAlignment = HorizontalAlignment.Center; Grid.SetColumn(t, c); grid.Children.Add(t); }
        int lead = ((int)first.DayOfWeek + 6) % 7, days = DateTime.DaysInMonth(first.Year, first.Month);
        string today = Store.Td();
        for (int d = 1; d <= days; d++)
        {
            int pos = lead + d - 1, row = pos / 7 + 1, col = pos % 7;
            while (grid.RowDefinitions.Count <= row) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            string key = new DateTime(first.Year, first.Month, d).ToString("yyyy-MM-dd");
            var dotsRow = UI.Row(3);
            dotsRow.HorizontalAlignment = HorizontalAlignment.Center;
            var got = new List<string>();
            foreach (var lane in Store.Lanes)
            {
                bool on = Stats.Finished(lane, key); if (on) got.Add(lane);
                dotsRow.Children.Add(new Microsoft.UI.Xaml.Shapes.Ellipse { Width = 6, Height = 6, Fill = on ? UI.Lane(lane) : null, Stroke = on ? null : UI.Res("ControlStrokeColorDefaultBrush"), StrokeThickness = 1 });
            }
            var cell = new Border
            {
                CornerRadius = new CornerRadius(8), Padding = new Thickness(0, 6, 0, 6), Background = UI.Res("SubtleFillColorSecondaryBrush"),
                BorderBrush = key == today ? UI.Brand : null, BorderThickness = new Thickness(key == today ? 1.5 : 0),
                Opacity = string.CompareOrdinal(key, today) > 0 ? 0.45 : 1,
                Child = UI.Stack(4, new TextBlock { Text = d.ToString(), FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center }, dotsRow),
            };
            ToolTipService.SetToolTip(cell, new DateTime(first.Year, first.Month, d).ToString("MMMM d") + ": " + (got.Count > 0 ? string.Join(", ", got) + " done" : "nothing finished"));
            Grid.SetRow(cell, row); Grid.SetColumn(cell, col); grid.Children.Add(cell);
        }
        cal.Children.Clear(); cal.Children.Add(grid);
    }

    void DrawNotes()
    {
        past.Children.Clear();
        var nt = Store.St["nt"] as JsonObject;
        var days = nt?.Where(kv => string.CompareOrdinal(kv.Key, Store.Td()) < 0 && (kv.Value?.ToString() ?? "").Trim().Length > 0)
            .OrderByDescending(kv => kv.Key).Take(5).ToList() ?? new();
        if (days.Count == 0) { past.Children.Add(UI.M("Your past notes will show here.")); return; }
        foreach (var kv in days)
            past.Children.Add(new Border
            {
                Padding = new Thickness(0, 8, 0, 8), BorderBrush = UI.Res("DividerStrokeColorDefaultBrush"), BorderThickness = new Thickness(0, 1, 0, 0),
                Child = UI.Stack(2, UI.T(DateTime.TryParse(kv.Key, out var dt) ? dt.ToString("ddd, MMM d") : kv.Key, 12, true, UI.Muted), UI.T(kv.Value?.ToString() ?? "", 14)),
            });
    }

    void DrawChallenges()
    {
        var L2 = Stats.ThisWeek(); int done = 0;
        challenges.Children.Clear();
        foreach (var c in L2)
        {
            int v = Math.Min(Stats.ChallengeCount(c.Id), c.Goal); bool fin = v >= c.Goal; if (fin) done++;
            var head = new Grid();
            head.Children.Add(UI.T((fin ? "✓ " : "") + c.Text, 14, fin, fin ? UI.Res("CodingBrush") : null));
            var cnt = UI.T($"{v}/{c.Goal}", 13, true, UI.Muted); cnt.HorizontalAlignment = HorizontalAlignment.Right; head.Children.Add(cnt);
            challenges.Children.Add(UI.Stack(6, head, new ProgressBar { Maximum = c.Goal, Value = v, Height = 6, CornerRadius = new CornerRadius(3) }));
        }
        chSub.Text = L2.Count > 0 ? $"{done} of {L2.Count} done. New challenges every Monday." : "No challenges this week.";
    }

    void DrawBadges()
    {
        int best = Stats.BestStreak, cl = Store.Arr("tc").Count;
        int calm = Stats.Sessions.Count(x => string.CompareOrdinal(x.d, Store.Td(6)) >= 0 && x.v == "c");
        int nights = Enumerable.Range(0, 7).Count(i => Stats.SleepOn(Store.Td(i)) >= 7);
        var B = new (string, bool)[] { ("3-day streak", best >= 3), ("7-day streak", best >= 7), ("5 calm sessions", calm >= 5), ("3 nights of 7+ hours", nights >= 3), ("First coding log", cl >= 1), ("10 coding logs", cl >= 10) };
        badges.Children.Clear();
        var wrap = new VariableSizedWrapGrid { Orientation = Orientation.Horizontal, ItemHeight = 34, ItemWidth = 160 };
        foreach (var (name, on) in B)
        {
            var p = UI.Pill((on ? "✓ " : "") + name, on ? UI.Res("CodingBrush") : UI.Muted, on ? UI.Tint(UI.Res("CodingBrush"), 0.14) : null);
            p.Margin = new Thickness(0, 0, 6, 6);
            wrap.Children.Add(p);
        }
        badges.Children.Add(wrap);
    }

    void DrawWeek()
    {
        var s = Stats.Sessions.Where(x => string.CompareOrdinal(x.d, Store.Td(6)) >= 0).ToList();
        int calm = s.Count(x => x.v == "c");
        var hs = Enumerable.Range(0, 7).Select(i => Stats.SleepOn(Store.Td(i))).Where(h => h != null).Select(h => h!.Value).ToList();
        var sc = Store.Arr("sc").OfType<JsonObject>().LastOrDefault();
        var labels = GameData.Strings(GameData.Genre(Store.St)["scores"]);
        string L(int i) => i < labels.Count ? labels[i] : "Score " + (i + 1);
        var rows = new (string, string)[]
        {
            ("Checklists finished (7 days)", $"Gaming {Stats.DaysFinished("gaming")}, Sleep {Stats.DaysFinished("sleep")}, Coding {Stats.DaysFinished("coding")}"),
            ("Current streaks", $"Gaming {Store.Streak("gaming")}, Sleep {Store.Streak("sleep")}, Coding {Store.Streak("coding")}"),
            ("Sessions (7 days)", $"Calm {calm}, Tilted {s.Count - calm}"),
            ("Average sleep", hs.Count > 0 ? hs.Average().ToString("0.0") + " h" : "no data yet"),
            ("Latest scores", sc == null ? "none yet" : $"{L(0)} {sc["a"]}, {L(1)} {sc["b"]}, {L(2)} {sc["c"]}"),
        };
        week.Children.Clear();
        foreach (var (k, v) in rows)
            week.Children.Add(new Border
            {
                Padding = new Thickness(0, 8, 0, 8), BorderBrush = UI.Res("DividerStrokeColorDefaultBrush"), BorderThickness = new Thickness(0, week.Children.Count == 0 ? 0 : 1, 0, 0),
                Child = UI.Stack(1, UI.T(k, 12, true, UI.Muted), UI.T(v, 14)),
            });
    }
}
