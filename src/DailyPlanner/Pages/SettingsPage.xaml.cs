using System.Diagnostics;
using System.Text.Json.Nodes;
using DailyPlanner.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Win32;
using Windows.Storage.Pickers;

namespace DailyPlanner.Pages;

// ===== Settings: account, game, look, reminders, data =====
public sealed partial class SettingsPage : Page
{
    readonly StackPanel root = new() { Spacing = 16, MaxWidth = 820, HorizontalAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(36, 28, 36, 36) };
    readonly TextBlock syncInfo = UI.M("", 12);
    readonly StackPanel logins = new();
    readonly TextBlock aiStatus = UI.M("", 12);
    bool loading = true;

    public SettingsPage()
    {
        InitializeComponent();
        Content = UI.Scroller(root);
        root.ChildrenTransitions = new Microsoft.UI.Xaml.Media.Animation.TransitionCollection { new Microsoft.UI.Xaml.Media.Animation.EntranceThemeTransition { IsStaggeringEnabled = true } };
        SizeChanged += (s, e) => root.Padding = e.NewSize.Width < 600 ? new Thickness(14, 16, 14, 24) : e.NewSize.Width < 860 ? new Thickness(22, 22, 22, 30) : new Thickness(36, 28, 36, 36);
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        Build();
        // some controls report their starting value a moment later; ignore that
        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () => loading = false);
    }

    static string Website(string path) => Config.Website + path;
    static void Open(string url) => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

    void Build()
    {
        root.Children.Clear();
        root.Children.Add(UI.PageTitle("Settings", "Make it yours", "Changes save as you go."));
        root.Children.Add(AccountCard());
        root.Children.Add(GameCard());
        root.Children.Add(LookCard());
        root.Children.Add(ReminderCard());
        root.Children.Add(DataCard());
        root.Children.Add(AboutCard());
    }

    // ---------- Account ----------
    UIElement AccountCard()
    {
        var s = Auth.Current;
        string name = s == null ? (Store.Name.Length > 0 ? Store.Name : "Guest") : (s.Name.Length > 0 ? s.Name : Store.Name.Length > 0 ? Store.Name : s.Email.Split('@')[0]);
        var who = new Grid { ColumnSpacing = 14 };
        who.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); who.ColumnDefinitions.Add(new ColumnDefinition());
        who.Children.Add(UI.Avatar(name, 48));
        var info = UI.Stack(1, UI.T(name, 15, true), UI.M(s == null ? "Not signed in. Your data only saves on this computer." : "Signed in as " + s.Email)); info.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(info, 1); who.Children.Add(info);

        if (s == null)
            return UI.Card("", "Account", null, null, null, who, UI.Btn("Sign in to sync", () => App.Window?.ShowLogin(), true));

        var buttons = UI.Row(8,
            UI.Btn("Sync now", SyncNow, false, ""),
            UI.Btn("Log out", LogOut),
            UI.Btn("Log out of all devices", LogOutAll));
        var del = UI.Btn("Delete account", DeleteAccount);
        del.Foreground = UI.Solid(0xE5, 0x48, 0x4D);
        _ = LoadLogins();
        return UI.Card("", "Account", null, null, null, who, buttons, syncInfo,
            UI.T("Recent logins", 13, true), logins, UI.M("Don't recognize one? Use \"Log out of all devices\" and change your password.", 12), del);
    }

    async Task LoadLogins()
    {
        logins.Children.Clear();
        try
        {
            foreach (var (device, time) in await Cloud.RecentLogins(Auth.Current!.Uid))
            {
                var g = new Grid { Padding = new Thickness(0, 6, 0, 6), BorderBrush = UI.Res("DividerStrokeColorDefaultBrush"), BorderThickness = new Thickness(0, 1, 0, 0) };
                g.Children.Add(UI.T(device, 13));
                var t = UI.M(time?.ToString("MMM d, h:mm tt") ?? "", 12); t.HorizontalAlignment = HorizontalAlignment.Right; g.Children.Add(t);
                logins.Children.Add(g);
            }
        }
        catch { logins.Children.Add(UI.M("Couldn't load recent logins.", 12)); }
    }

    async void SyncNow()
    {
        syncInfo.Text = "Syncing…";
        try
        {
            var r = await Store.SyncDown();
            syncInfo.Text = r switch { "down" => "Got the latest from your account.", "up" => "Saved this computer's changes to your account.", _ => "Everything is up to date." };
        }
        catch (Exception ex) { syncInfo.Text = Auth.Friendly(ex); }
    }

    async Task<bool> Confirm(string title, string text, string ok, bool danger = false)
    {
        var d = new ContentDialog { Title = title, Content = text, PrimaryButtonText = ok, CloseButtonText = "Cancel", DefaultButton = danger ? ContentDialogButton.Close : ContentDialogButton.Primary, XamlRoot = XamlRoot };
        return await d.Themed().ShowAsync() == ContentDialogResult.Primary;
    }

    async void LogOut()
    {
        if (!await Confirm("Log out?", "Your data stays saved in your account. Log back in anytime.", "Log out")) return;
        Auth.SignOut(); Store.ClearLocal(); App.Window?.ShowLogin();
    }

    async void LogOutAll()
    {
        if (!await Confirm("Log out everywhere?", "This signs you out on every device, including this one.", "Log out everywhere", true)) return;
        try { await Cloud.KickAll(Auth.Current!.Uid); }
        catch { syncInfo.Text = "Couldn't reach the server. Try again."; return; }
        Auth.SignOut(); Store.ClearLocal(); App.Window?.ShowLogin();
    }

    async void DeleteAccount()
    {
        if (!Auth.RecentLogin)
        {
            await new ContentDialog { Title = "Please log in again first", Content = "For safety, log out and log back in, then delete your account within 5 minutes.", CloseButtonText = "OK", XamlRoot = XamlRoot }.Themed().ShowAsync();
            return;
        }
        var box = new TextBox { PlaceholderText = "Type DELETE" };
        var d = new ContentDialog
        {
            Title = "Delete your account?", PrimaryButtonText = "Delete forever", CloseButtonText = "Cancel", DefaultButton = ContentDialogButton.Close, XamlRoot = XamlRoot, IsPrimaryButtonEnabled = false,
            Content = UI.Stack(12, UI.T("This deletes your account and all your saved data forever. It can't be undone. Type DELETE to confirm."), box),
        };
        box.TextChanged += (s, e) => d.IsPrimaryButtonEnabled = box.Text.Trim() == "DELETE";
        if (await d.Themed().ShowAsync() != ContentDialogResult.Primary) return;
        var uid = Auth.Current!.Uid;
        try
        {
            var writes = new List<JsonObject>();
            foreach (var l in await Cloud.List("users/" + uid + "/logins")) writes.Add(Cloud.Delete("users/" + uid + "/logins/" + l.Id));
            writes.Add(Cloud.Delete("board/" + uid));
            foreach (var n in await Cloud.List("nudges/" + uid + "/in")) writes.Add(Cloud.Delete("nudges/" + uid + "/in/" + n.Id));
            if ((Store.St["lb"] as JsonObject)?["nd"] is JsonObject nd) foreach (var kv in nd) writes.Add(Cloud.Delete("nudges/" + kv.Key + "/in/" + uid));
            writes.Add(Cloud.Delete("users/" + uid));
            foreach (var chunk in writes.Chunk(400)) await Cloud.Commit(chunk);
            await Auth.DeleteAccount();
            Auth.SignOut(); Store.ClearLocal(); App.Window?.ShowLogin();
        }
        catch (Exception ex) { syncInfo.Text = "Couldn't delete your account. " + Auth.Friendly(ex); }
    }

    // ---------- Game and schedule ----------
    UIElement GameCard()
    {
        var st = Store.St;
        var game = new TextBox { Header = "Your game", PlaceholderText = "Example: Apex, chess, GeoGuessr", MaxLength = 40,
            Text = st["gn"]?.ToString() is { Length: > 0 } gn ? gn : (st["gm"]?.ToString() is string gm && gm != "Another game" ? gm : "") };
        var order = GameData.GenreOrder;
        var type = new ComboBox { Header = "Game type", HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var g in order) type.Items.Add(new ComboBoxItem { Content = GameData.GenreName(g), Tag = g });
        void SelectType() { var id = GameData.GenreId(Store.St); type.SelectedItem = type.Items.OfType<ComboBoxItem>().FirstOrDefault(i => (string)i.Tag == id); }
        SelectType();
        void Status() { var G = GameData.Genre(Store.St); aiStatus.Text = G["ai"] != null ? "Using a custom AI plan for " + G["game"] + "." : "Using the built-in plan for this game type. (Custom AI plans are made on the website.)"; }
        Status();
        game.TextChanged += (s, e) =>
        {
            if (loading) return;
            var v = game.Text.Trim(); var d = GameData.Detect(v);
            Store.St["gn"] = v; Store.St["gm"] = d.K.Length > 0 ? d.K : "Another game"; Store.St.Remove("gg");
            Store.Save(false); SelectType(); Status();
        };
        type.SelectionChanged += (s, e) =>
        {
            if (loading || type.SelectedItem is not ComboBoxItem it) return;
            if ((string)it.Tag == GameData.GenreId(Store.St)) return;
            Store.St["gg"] = (string)it.Tag; Store.Save(); Status();
        };

        // Full days
        string[] names = { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };
        var fd = (Store.St["fd"] as JsonArray)?.Select(x => (int)Store.Num(x)).ToList() ?? new List<int> { 0, 1, 2 };
        var days = new Grid { ColumnSpacing = 6 };
        for (int i = 0; i < 7; i++)
        {
            int idx = i;
            days.ColumnDefinitions.Add(new ColumnDefinition());
            var b = UI.Chip(names[i], fd.Contains(i));
            b.Click += (s, e) =>
            {
                var cur = (Store.St["fd"] as JsonArray)?.Select(x => (int)Store.Num(x)).ToList() ?? new List<int> { 0, 1, 2 };
                if (b.IsChecked == true) { if (!cur.Contains(idx)) cur.Add(idx); } else cur.Remove(idx);
                Store.St["fd"] = new JsonArray(cur.OrderBy(x => x).Select(x => (JsonNode?)x).ToArray());
                Store.Save();
            };
            Grid.SetColumn(b, i); days.Children.Add(b);
        }
        // Short day letters when the window is narrow
        bool? wasTiny = null;
        days.SizeChanged += (s, e) =>
        {
            bool tiny = e.NewSize.Width < 420;
            if (tiny == wasTiny) return; wasTiny = tiny;
            for (int i = 0; i < 7; i++) if (days.Children[i] is ToggleButton tb) tb.Content = tiny ? names[i][..1] : names[i];
        };
        return UI.Card("", "Game and schedule", "What you play and which days you have more time.", UI.Lane("gaming"), null,
            game, type, UI.M("This picks your drills and score labels. Change it if we guessed wrong.", 12), aiStatus,
            UI.T("Full days (longer routine)", 13, true), days);
    }

    // ---------- Look ----------
    UIElement LookCard()
    {
        var theme = new RadioButtons { Header = "Look", MaxColumns = 3 };
        foreach (var t in new[] { "Match Windows", "Light", "Dark" }) theme.Items.Add(t);
        theme.SelectedIndex = Theme.Mode switch { "light" => 1, "dark" => 2, _ => 0 };
        theme.SelectionChanged += (s, e) =>
        {
            if (loading || theme.SelectedIndex < 0) return;
            var m = theme.SelectedIndex switch { 1 => "light", 2 => "dark", _ => "auto" };
            if (m != Theme.Mode) Theme.Set(m);
        };
        var sound = new ToggleSwitch { Header = "Sound when you finish a checklist", IsOn = Store.St["snd"]?.ToString() != "false" };
        sound.Toggled += (s, e) => { if (loading) return; Store.St["snd"] = sound.IsOn; Store.Save(); if (sound.IsOn) Sound.Chime(); };
        var startup = new ToggleSwitch { Header = "Open Daily Planner when Windows starts", IsOn = Startup.On };
        startup.Toggled += (s, e) => { if (!loading) Startup.On = startup.IsOn; };
        return UI.Card("", "Appearance and app", "Light or dark, sound, and startup.", null, null, theme, sound, startup);
    }

    // ---------- Reminders ----------
    UIElement ReminderCard()
    {
        var rm = Store.Obj("rm");
        TimePicker Make(string header, string key)
        {
            var tp = new TimePicker { Header = header, MinuteIncrement = 5, HorizontalAlignment = HorizontalAlignment.Stretch };
            if (TimeSpan.TryParse(rm[key]?.ToString(), out var t)) tp.SelectedTime = t;
            tp.SelectedTimeChanged += (s, e) =>
            {
                if (loading) return;
                var r = Store.Obj("rm");
                if (tp.SelectedTime is TimeSpan v) r[key] = $"{v.Hours:00}:{v.Minutes:00}"; else r.Remove(key);
                Store.Save();
            };
            return tp;
        }
        var clear = new HyperlinkButton { Content = "Turn reminders off" };
        clear.Click += (s, e) => { Store.Obj("rm").Remove("b"); Store.Obj("rm").Remove("s"); Store.Save(); Build(); };
        return UI.Card("", "Reminders", "Windows pop-ups while the app is open (even minimized).", null, null,
            UI.Two(Make("Bedtime reminder", "b"), Make("Study reminder", "s")), clear);
    }

    // ---------- Data ----------
    UIElement DataCard()
    {
        var reset = UI.Btn("Reset today's checklists", async () =>
        {
            if (!await Confirm("Reset today's checklists?", "This unchecks everything on today's checklists.", "Reset")) return;
            var dn = Store.Obj("dn"); var ok = Store.Obj("ok");
            foreach (var c in Store.Lanes) { dn.Remove(Store.Td() + c); ok.Remove(c + Store.Td()); }
            Store.Save();
        });
        return UI.Card("", "Your data", "Back up, restore, or start fresh.", null, null,
            UI.Row(8, UI.Btn("Export my data", Export, false, ""), UI.Btn("Import data", Import, false, "")),
            UI.Row(8, UI.Btn("Redo setup", () => Open(Website("setup/"))), reset),
            UI.Btn("Open data folder", () => { Directory.CreateDirectory(Store.Folder); Open(Store.Folder); }));
    }

    static void Hook(object picker) => WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(App.Window));

    async void Export()
    {
        var p = new FileSavePicker { SuggestedFileName = "planner-data" };
        p.FileTypeChoices.Add("JSON file", new List<string> { ".json" });
        Hook(p);
        var f = await p.PickSaveFileAsync();
        if (f == null) return;
        await Windows.Storage.FileIO.WriteTextAsync(f, Store.St.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        ShellPage.Current?.ShowToast("Exported", "Saved to " + f.Name);
    }

    async void Import()
    {
        var p = new FileOpenPicker(); p.FileTypeFilter.Add(".json"); Hook(p);
        var f = await p.PickSingleFileAsync();
        if (f == null) return;
        JsonObject? d;
        try { d = JsonNode.Parse(await Windows.Storage.FileIO.ReadTextAsync(f)) as JsonObject; } catch { d = null; }
        if (d == null) { ShellPage.Current?.ShowToast("Not a valid file", "Pick a file made by Export my data."); return; }
        if (!await Confirm("Replace your data?", "Your current planner data will be replaced with this file.", "Replace", true)) return;
        d["acct"] = Store.Acct;
        Store.Replace(d); Store.Save();
        ShellPage.Current?.ShowToast("Imported", "Your planner was restored.");
    }

    // ---------- About ----------
    UIElement AboutCard()
    {
        var v = typeof(App).Assembly.GetName().Version;
        var links = UI.Row(4,
            Link("Open the website", Website("home/")), Link("Privacy Policy", Website("privacy/")), Link("Terms of Use", Website("terms/")));
        var fb = UI.Btn("Send feedback", Feedback, false, "");
        return UI.Card("", "About", $"Daily Planner for Windows {v?.Major}.{v?.Minor}.{v?.Build}", null, null, fb, links);
    }

    static HyperlinkButton Link(string text, string url) { var h = new HyperlinkButton { Content = text }; h.Click += (s, e) => Open(url); return h; }

    async void Feedback()
    {
        if (Auth.Current == null) { ShellPage.Current?.ShowToast("Sign in first", "Feedback needs an account so we can follow up."); return; }
        var type = new RadioButtons { MaxColumns = 3 }; foreach (var t in new[] { "Bug", "Idea", "Other" }) type.Items.Add(t); type.SelectedIndex = 0;
        var box = new TextBox { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 110, MaxLength = 2000, PlaceholderText = "What happened, or what would make the app better?" };
        var d = new ContentDialog { Title = "Send feedback", PrimaryButtonText = "Send", CloseButtonText = "Cancel", DefaultButton = ContentDialogButton.Primary, XamlRoot = XamlRoot, Content = UI.Stack(12, type, box) };
        if (await d.Themed().ShowAsync() != ContentDialogResult.Primary || box.Text.Trim().Length == 0) return;
        try
        {
            await Cloud.Commit(Cloud.Update("feedback/" + Guid.NewGuid().ToString("N")[..20], new JsonObject
            {
                ["uid"] = Auth.Current.Uid, ["type"] = new[] { "bug", "idea", "other" }[Math.Max(0, type.SelectedIndex)], ["text"] = box.Text.Trim(), ["page"] = "windows-app",
            }, "time", replace: true));
            ShellPage.Current?.ShowToast("Thanks!", "Your feedback was sent.");
        }
        catch (Exception ex) { ShellPage.Current?.ShowToast("Couldn't send", Auth.Friendly(ex)); }
    }
}

// ===== "Open when Windows starts" (a normal per-user startup entry) =====
public static class Startup
{
    const string Key = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public static bool On
    {
        get { try { using var k = Registry.CurrentUser.OpenSubKey(Key); return k?.GetValue("DailyPlanner") != null; } catch { return false; } }
        set
        {
            try
            {
                using var k = Registry.CurrentUser.OpenSubKey(Key, true) ?? Registry.CurrentUser.CreateSubKey(Key);
                if (value) k.SetValue("DailyPlanner", "\"" + Environment.ProcessPath + "\"");
                else k.DeleteValue("DailyPlanner", false);
            }
            catch { }
        }
    }
}
