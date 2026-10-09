using System.Text.Json.Nodes;
using DailyPlanner.Services;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.ApplicationModel.DataTransfer;

namespace DailyPlanner.Pages;

// ===== Leaderboard: friend code, friends ranked by streak, nudges =====
public sealed partial class LeaderboardPage : Page
{
    readonly StackPanel root = new() { Spacing = 16, MaxWidth = 760, Padding = new Thickness(36, 28, 36, 36) };
    readonly StackPanel body = new() { Spacing = 16 };
    readonly StackPanel list = new();
    readonly TextBox nameBox = new() { Header = "Name shown to friends", MaxLength = 24 };
    readonly TextBox codeBox = new() { PlaceholderText = "ABC123", MaxLength = 6, CharacterCasing = CharacterCasing.Upper, FontFamily = new FontFamily("Cascadia Mono, Consolas") };
    readonly InfoBar msg = new() { IsOpen = false, IsClosable = true };
    readonly Border count = UI.Pill("");
    DispatcherTimer? nameTimer;

    public LeaderboardPage()
    {
        InitializeComponent();
        root.Children.Add(UI.PageTitle("Leaderboard", "Compete with friends", "Compare streaks, send a nudge, keep each other going."));
        root.Children.Add(body);
        Content = new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        nameBox.TextChanged += (s, e) =>
        {
            nameTimer?.Stop();
            nameTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(800) };
            nameTimer.Tick += async (a, b) =>
            {
                nameTimer?.Stop();
                var v = nameBox.Text.Trim(); if (v.Length == 0 || Board.Lb == null) return;
                var bad = Board.NameProblem(v);
                if (bad != null) { Show(bad); return; }
                Board.Lb["name"] = v; Store.Save();
                try { await Board.Update(); await DrawList(); } catch { }
            };
            nameTimer.Start();
        };
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e) => await Draw();

    static UIElement Empty(string title, string text, Button? action)
    {
        var s = UI.Stack(10);
        s.HorizontalAlignment = HorizontalAlignment.Center;
        s.Padding = new Thickness(10, 16, 10, 8);
        s.Children.Add(new Border { Width = 64, Height = 64, CornerRadius = new CornerRadius(20), Background = UI.Tint(UI.Brand, 0.16), HorizontalAlignment = HorizontalAlignment.Center,
            Child = new FontIcon { Glyph = "", FontSize = 28, Foreground = UI.Brand } });
        var t = UI.T(title, 20, true); t.HorizontalAlignment = HorizontalAlignment.Center; s.Children.Add(t);
        var m = UI.M(text, 14); m.TextAlignment = TextAlignment.Center; m.MaxWidth = 360; s.Children.Add(m);
        if (action != null) { action.HorizontalAlignment = HorizontalAlignment.Center; s.Children.Add(action); }
        return new Border { Background = UI.Res("CardBackgroundFillColorDefaultBrush"), BorderBrush = UI.Res("CardStrokeColorDefaultBrush"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(14), Padding = new Thickness(20), Child = s };
    }

    async Task Draw()
    {
        body.Children.Clear();
        if (Auth.Current == null)
        {
            body.Children.Add(Empty("Sign in to compete", "The leaderboard needs an account so friends can find you. It only takes a minute.", UI.Btn("Sign in", () => App.Window?.ShowLogin(), true)));
            return;
        }
        if (Board.Lb == null)
        {
            // Already joined on another device?
            try
            {
                var mine = await Cloud.Get("board/" + Auth.Current.Uid);
                if (mine != null) { Store.St["lb"] = new JsonObject { ["code"] = mine["code"]?.ToString(), ["name"] = mine["name"]?.ToString(), ["fr"] = new JsonArray() }; Store.Save(); }
            }
            catch { }
        }
        if (Board.Lb == null)
        {
            Button join = null!;
            join = UI.Btn("Join the leaderboard", async () =>
            {
                join.IsEnabled = false;
                try { await Board.Join(); await Draw(); }
                catch (Exception ex) { join.IsEnabled = true; Show(Auth.Friendly(ex)); }
            }, true);
            body.Children.Add(Empty("Join the leaderboard", "Friends you add will see your name and your streaks. Nothing else is shared.", join));
            body.Children.Add(msg);
            return;
        }

        // Friend code
        string code = Board.Lb["code"]?.ToString() ?? "";
        var codeText = new TextBlock { Text = code, FontSize = 28, FontWeight = FontWeights.SemiBold, CharacterSpacing = 300, FontFamily = new FontFamily("Cascadia Mono, Consolas"), VerticalAlignment = VerticalAlignment.Center };
        Button copy = null!;
        copy = UI.Btn("Copy", () =>
        {
            var p = new DataPackage(); p.SetText(code); Clipboard.SetContent(p);
            copy.Content = "Copied!";
        });
        var codeRow = new Grid { Padding = new Thickness(16, 12, 12, 12), Background = UI.Res("SubtleFillColorSecondaryBrush"), CornerRadius = new CornerRadius(12) };
        codeRow.Children.Add(codeText); copy.HorizontalAlignment = HorizontalAlignment.Right; copy.VerticalAlignment = VerticalAlignment.Center; codeRow.Children.Add(copy);
        nameBox.Text = Board.Lb["name"]?.ToString() ?? "";
        body.Children.Add(UI.Card("", "Your friend code", "Share it so friends can add you.", null, null, codeRow, nameBox));

        // Friends
        var add = UI.Btn("Add", AddFriend, true);
        var addRow = new Grid { ColumnSpacing = 8 };
        addRow.ColumnDefinitions.Add(new ColumnDefinition()); addRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        addRow.Children.Add(codeBox); Grid.SetColumn(add, 1); addRow.Children.Add(add);
        codeBox.KeyDown += (s, e) => { if (e.Key == Windows.System.VirtualKey.Enter) AddFriend(); };
        body.Children.Add(UI.Card("", "Friends", "Ranked by current streak.", null, count, list, UI.T("Add a friend by code", 13, true), addRow, msg));

        var leave = UI.Btn("Leave the leaderboard", async () =>
        {
            var d = new ContentDialog { Title = "Leave the leaderboard?", Content = "Friends won't see your streaks anymore. You can join again later.", PrimaryButtonText = "Leave", CloseButtonText = "Cancel", XamlRoot = XamlRoot };
            if (await d.ShowAsync() == ContentDialogResult.Primary) { await Board.Leave(); await Draw(); }
        });
        leave.Foreground = UI.Solid(0xE5, 0x48, 0x4D);
        body.Children.Add(leave);
        try { await Board.Update(); } catch { }
        await DrawList();
    }

    async Task DrawList()
    {
        list.Children.Clear();
        list.Children.Add(new ProgressRing { IsActive = true, Width = 24, Height = 24, Margin = new Thickness(0, 10, 0, 10) });
        List<BoardRow> rows;
        try { rows = await Board.Rows(); }
        catch { list.Children.Clear(); list.Children.Add(UI.M("Couldn't load the board. Try again later.")); return; }
        list.Children.Clear();
        string mine = Board.Lb?["code"]?.ToString() ?? "";
        ((TextBlock)count.Child).Text = UI.Plural(Math.Max(0, rows.Count - 1), "friend", "friends");
        Brush[] medal = { UI.Solid(0xE8, 0xA3, 0x17), UI.Solid(0x9A, 0xA3, 0xB5), UI.Solid(0xC2, 0x7C, 0x4A) };
        for (int i = 0; i < rows.Count; i++)
        {
            var r = rows[i]; bool me = r.Code == mine;
            var g = new Grid { ColumnSpacing = 12, Padding = new Thickness(10, 10, 10, 10), CornerRadius = new CornerRadius(10), Background = me ? UI.Tint(UI.Brand, 0.10) : null };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(22) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var rk = UI.T((i + 1).ToString(), 14, true, i < 3 ? medal[i] : UI.Muted); rk.VerticalAlignment = VerticalAlignment.Center; g.Children.Add(rk);
            var av = new PersonPicture { DisplayName = r.Name, Width = 34, Height = 34 }; Grid.SetColumn(av, 1); g.Children.Add(av);
            var nm = UI.Stack(0, UI.T(r.Name + (me ? " (you)" : ""), 14, true), UI.M("Best " + UI.Plural(r.Best, "day", "days"), 12)); nm.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(nm, 2); g.Children.Add(nm);
            var sv = UI.Stack(0, UI.T(r.Streak.ToString(), 20, true), UI.M("day streak", 11)); sv.VerticalAlignment = VerticalAlignment.Center;
            ((TextBlock)sv.Children[0]).HorizontalAlignment = HorizontalAlignment.Right;
            Grid.SetColumn(sv, 3); g.Children.Add(sv);
            if (!me)
            {
                var row = r;
                Button nudge = null!;
                nudge = UI.Btn(Board.Nudged(row.Uid) ? "Nudged" : "Nudge", async () =>
                {
                    nudge.IsEnabled = false; nudge.Content = "Sending…";
                    try { await Board.Nudge(row.Uid); nudge.Content = "Nudged"; }
                    catch { nudge.Content = "Try later"; }
                });
                nudge.IsEnabled = !Board.Nudged(row.Uid);
                var remove = UI.IconBtn("", "Remove " + row.Name, () =>
                {
                    var fr = Board.Lb?["fr"] as JsonArray; var keep = GameData.Strings(fr).Where(c => c != row.Code).ToList();
                    Board.Lb!["fr"] = new JsonArray(keep.Select(c => (JsonNode?)c).ToArray());
                    Store.Save(); _ = DrawList();
                });
                var acts = UI.Row(6, nudge, remove); acts.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetColumn(acts, 4); g.Children.Add(acts);
            }
            list.Children.Add(g);
        }
        if (rows.Count < 2) list.Children.Add(UI.M("No friends yet. Share your code above, or add theirs below."));
    }

    async void AddFriend()
    {
        msg.IsOpen = false;
        var lb = Board.Lb; if (lb == null) return;
        string code = new string(codeBox.Text.ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());
        var fr = GameData.Strings(lb["fr"]);
        if (code.Length != 6) { Show("Friend codes are 6 letters and numbers."); return; }
        if (code == lb["code"]?.ToString()) { Show("That's your own code."); return; }
        if (fr.Contains(code)) { Show("You already added them."); return; }
        if (fr.Count >= 50) { Show("You can add up to 50 friends."); return; }
        var ad = lb["ad"] as JsonObject;
        if (ad == null || ad["d"]?.ToString() != Store.Td()) { ad = new JsonObject { ["d"] = Store.Td(), ["n"] = 0 }; lb["ad"] = ad; }
        if (Store.Num(ad["n"]) >= 10) { Show("You've added 10 friends today. Try again tomorrow."); return; }
        try { if (await Board.Find(code) == null) { Show("No one has that code. Check it and try again."); return; } }
        catch { Show("Couldn't check that code. Try again."); return; }
        fr.Add(code);
        lb["fr"] = new JsonArray(fr.Select(c => (JsonNode?)c).ToArray());
        ad["n"] = Store.Num(ad["n"]) + 1;
        Store.Save();
        codeBox.Text = "";
        await DrawList();
    }

    void Show(string text, InfoBarSeverity sev = InfoBarSeverity.Error) { msg.Message = text; msg.Severity = sev; msg.IsOpen = true; }
}
