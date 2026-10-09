using System.Text.Json.Nodes;
using DailyPlanner.Services;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace DailyPlanner.Pages;

// ===== Sleep: habits checklist, last night's sleep, 7-night chart =====
public sealed partial class SleepPage : Page
{
    readonly UI.Layout L = new();
    readonly TimePicker bed = new() { Header = "Bedtime", MinuteIncrement = 5, HorizontalAlignment = HorizontalAlignment.Stretch };
    readonly TimePicker wake = new() { Header = "Wake time", MinuteIncrement = 5, HorizontalAlignment = HorizontalAlignment.Stretch };
    readonly TextBlock result = new() { FontSize = 24, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center };
    readonly TextBlock week = UI.M("");
    readonly StackPanel chart = new();
    readonly InfoBar bedtimeBar = new() { IsOpen = false, IsClosable = false, Severity = InfoBarSeverity.Informational };
    bool loading;

    public SleepPage()
    {
        InitializeComponent();
        Content = L.Root;
        L.Top.Children.Add(UI.PageTitle("Sleep", "Sleep better", "Wind down, protect your sleep, and wake up ready."));
        L.Top.Children.Add(bedtimeBar);
        L.Main.Children.Add(new ChecklistCard("sleep"));
        week.HorizontalAlignment = HorizontalAlignment.Center;
        L.Side.Children.Add(UI.Card("", "Sleep log", "Last night. Aim for 7 hours or more.", UI.Lane("sleep"), null, bed, wake, result, week));
        L.Side.Children.Add(UI.Card("", "Last 7 nights", null, UI.Lane("sleep"), null, chart));
        bed.SelectedTimeChanged += (s, e) => SaveTimes();
        wake.SelectedTimeChanged += (s, e) => SaveTimes();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        loading = true;
        var d = (Store.St["sl"] as JsonObject)?[Store.Td()] as JsonObject;
        bed.SelectedTime = Parse(d?["b"]?.ToString());
        wake.SelectedTime = Parse(d?["w"]?.ToString());
        loading = false;
        Draw();
        Store.Changed += Draw;
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e) => Store.Changed -= Draw;

    static TimeSpan? Parse(string? hhmm) => TimeSpan.TryParse(hhmm, out var t) ? t : null;
    static string Fmt(TimeSpan t) => $"{t.Hours:00}:{t.Minutes:00}";

    void SaveTimes()
    {
        if (loading) return;
        var sl = Store.Obj("sl");
        sl[Store.Td()] = new JsonObject { ["b"] = bed.SelectedTime is TimeSpan b ? Fmt(b) : "", ["w"] = wake.SelectedTime is TimeSpan w ? Fmt(w) : "" };
        Store.Save();
    }

    void Draw()
    {
        var h = Stats.SleepOn(Store.Td());
        result.Text = h == null ? "" : $"{h:0.0} hours" + (h >= 7 ? " ✓" : "");
        result.Foreground = h >= 7 ? UI.Res("CodingBrush") : UI.Res("SleepBrush");
        int n = Enumerable.Range(0, 7).Count(i => Stats.SleepOn(Store.Td(i)) >= 7);
        week.Text = $"Nights with 7+ hours this week: {n}";

        var vals = new List<double?>(); var labels = new List<string>();
        for (int i = 6; i >= 0; i--) { vals.Add(Stats.SleepOn(Store.Td(i))); labels.Add(DateTime.Now.AddDays(-i).ToString("dddd")); }
        chart.Children.Clear();
        chart.Children.Add(UI.Bars(vals, labels, 10, UI.Lane("sleep"), v => v.ToString("0.0") + " h", 7, "7 h goal"));

        // Bedtime countdown
        var bt = (Store.St["ob"] as JsonObject)?["bt"]?.ToString() ?? (Store.St["rm"] as JsonObject)?["b"]?.ToString();
        bedtimeBar.IsOpen = false;
        if (TimeSpan.TryParse(bt, out var t))
        {
            var target = DateTime.Today.Add(t); int diff = (int)Math.Round((target - DateTime.Now).TotalMinutes);
            if (diff < -360) diff += 1440;
            if (diff is > 0 and <= 240) { bedtimeBar.Title = diff > 60 ? $"Bedtime in {diff / 60}h {diff % 60}m" : $"Start winding down. Bedtime in {diff} min"; bedtimeBar.IsOpen = true; }
            else if (diff is <= 0 and > -180) { bedtimeBar.Title = "It's past your bedtime"; bedtimeBar.Severity = InfoBarSeverity.Warning; bedtimeBar.IsOpen = true; }
        }
        bedtimeBar.Visibility = bedtimeBar.IsOpen ? Visibility.Visible : Visibility.Collapsed;
    }
}
