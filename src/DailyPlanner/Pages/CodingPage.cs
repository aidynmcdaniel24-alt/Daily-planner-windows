using System.Text.Json.Nodes;
using DailyPlanner.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace DailyPlanner.Pages;

// ===== Coding: practice checklist, learning log, today's project idea =====
public sealed class CodingPage : Page
{
    readonly UI.Layout L = new();
    readonly TextBox entry = new() { PlaceholderText = "Example: finished CS50 week 1" };
    readonly StackPanel log = new();
    readonly TextBlock ideaTitle = UI.T("", 19, true);
    readonly TextBlock ideaDesc = UI.M("", 14);
    readonly Border ideaTag = UI.Pill("");
    Button? builtBtn;

    public CodingPage()
    {
        Content = L.Root;
        L.Top.Children.Add(UI.PageTitle("Coding", "Code every day", "Small steps toward a career in tech."));
        L.Main.Children.Add(new ChecklistCard("coding"));

        var add = UI.Btn("Add", AddEntry, true);
        var row = new Grid { ColumnSpacing = 8 };
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.Children.Add(entry); Grid.SetColumn(add, 1); row.Children.Add(add);
        entry.KeyDown += (s, e) => { if (e.Key == Windows.System.VirtualKey.Enter) AddEntry(); };
        L.Main.Children.Add(UI.Card("", "Learning log", "What did you learn or build today?", UI.Lane("coding"), null, row, log));

        builtBtn = UI.Btn("I built this", Built, true);
        L.Side.Children.Add(UI.Card("", "Today's project idea", null, UI.Lane("coding"), ideaTag, ideaTitle, ideaDesc,
            UI.Row(8, builtBtn, UI.Btn("Another idea", Another))));
    }

    protected override void OnNavigatedTo(NavigationEventArgs e) { Store.Changed += Draw; Draw(); }
    protected override void OnNavigatedFrom(NavigationEventArgs e) => Store.Changed -= Draw;

    void AddEntry()
    {
        var v = entry.Text.Trim(); if (v.Length == 0) return;
        Store.Arr("tc").Add(new JsonObject { ["d"] = Store.Td(), ["t"] = v });
        entry.Text = "";
        Store.Save();
    }

    void Draw()
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
        DrawIdea();
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
