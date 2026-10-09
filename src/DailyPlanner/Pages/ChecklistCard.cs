using DailyPlanner.Services;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;

namespace DailyPlanner.Pages;

// ===== A day's checklist with progress, streak, and "Edit tasks" (used on Gaming, Sleep and Coding) =====
public sealed class ChecklistCard : UserControl
{
    readonly string lane;
    readonly StackPanel rows = new();
    readonly StackPanel editor = new() { Spacing = 8, Visibility = Visibility.Collapsed };
    readonly UI.Meter bar;
    readonly TextBlock count = UI.T("", 12, true, UI.Muted);
    readonly Border streak = UI.Pill("");
    readonly Button editBtn;
    string sig = "";
    bool drawing;

    public readonly StackPanel Extra = new() { Spacing = 10 };   // pages can put things under the title (focus goal, etc.)

    static readonly Dictionary<string, (string glyph, string title)> Info = new()
    {
        ["gaming"] = ("", "Today's gaming"), ["sleep"] = ("", "Sleep habits"), ["coding"] = ("", "Coding practice"),
    };

    public ChecklistCard(string lane, string? sub = null)
    {
        this.lane = lane;
        bar = new UI.Meter(UI.Lane(lane));
        rows.ChildrenTransitions = new TransitionCollection { new EntranceThemeTransition { IsStaggeringEnabled = true } };
        editBtn = UI.Btn("Edit tasks", ToggleEdit, false, "");
        editBtn.HorizontalAlignment = HorizontalAlignment.Stretch;

        var prog = new Grid { ColumnSpacing = 12 };
        prog.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        prog.ColumnDefinitions.Add(new ColumnDefinition());
        prog.Children.Add(count);
        bar.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(bar, 1); prog.Children.Add(bar);

        Content = UI.Card(Info[lane].glyph, Info[lane].title, sub, UI.Lane(lane), streak, Extra, prog, rows, editBtn, editor);
        Loaded += (s, e) => { Store.Changed += Draw; Draw(); };
        Unloaded += (s, e) => Store.Changed -= Draw;
    }

    public void Draw()
    {
        drawing = true;
        var items = Store.Items(lane);
        var (k, n) = Store.Count(lane);
        count.Text = $"{k}/{n} done";
        bar.Value = n == 0 ? 0 : 100.0 * k / n;
        int s = Store.Streak(lane);
        ((TextBlock)streak.Child).Text = s > 0 ? $"\U0001F525 {UI.Plural(s, "day", "days")}" : "No streak yet";
        ((TextBlock)streak.Child).Foreground = s > 0 ? UI.Res("StreakBrush") : UI.Muted;

        string newSig = string.Join("|", items.Select(i => i.Title + "\u0001" + i.Note));
        if (newSig != sig)
        {
            sig = newSig;
            rows.Children.Clear();
            foreach (var t in items) rows.Children.Add(Row(t, t.Index == items.Count - 1));
            if (editor.Visibility == Visibility.Visible) DrawEditor();
        }
        else
            foreach (var t in items)
                if (rows.Children[t.Index] is Grid g && g.Children[0] is CheckBox cb) { cb.IsChecked = t.Done; Strike(g, t.Done); }
        drawing = false;
    }

    Grid Row(TaskItem t, bool last)
    {
        var g = new Grid { Padding = new Thickness(4, 10, 4, 10), ColumnSpacing = 2, Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.ColumnDefinitions.Add(new ColumnDefinition());
        var cb = new CheckBox { IsChecked = t.Done, VerticalAlignment = VerticalAlignment.Top, MinWidth = 0 };
        LaneColors(cb, lane);
        var text = new StackPanel { Margin = new Thickness(0, 5, 0, 0), Spacing = 1 };

        // "Aim training, 15 min" → title + a small time pill
        var m = System.Text.RegularExpressions.Regex.Match(t.Title, @"^(.*?),\s*((?:about\s+)?\d[\d\s\-–]*(?:min|mins|minutes|h|hr|hrs|hours?))$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var titleRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        titleRow.Children.Add(new TextBlock { Text = m.Success ? m.Groups[1].Value : t.Title, FontSize = 15, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
        if (m.Success) titleRow.Children.Add(UI.Pill(m.Groups[2].Value, UI.Muted));
        text.Children.Add(titleRow);
        if (t.Note.Length > 0) text.Children.Add(UI.M(t.Note));
        g.Children.Add(cb);
        Grid.SetColumn(text, 1); g.Children.Add(text);
        if (!last)
        {
            var line = new Border { Height = 1, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(36, 0, 0, -10), Background = UI.Res("DividerStrokeColorDefaultBrush") };
            Grid.SetColumnSpan(line, 2); g.Children.Add(line);
        }
        Strike(g, t.Done);
        cb.Checked += (s, e) => { if (!drawing) Store.SetDone(lane, t.Index, true); };
        cb.Unchecked += (s, e) => { if (!drawing) Store.SetDone(lane, t.Index, false); };
        g.Tapped += (s, e) =>
        {
            if (e.OriginalSource is DependencyObject d && Inside(d, cb)) return;
            cb.IsChecked = !(cb.IsChecked ?? false);
        };
        return g;
    }

    // Checkboxes in the lane's color (blue, orange or green) instead of the default
    internal static void LaneColors(CheckBox cb, string lane)
    {
        var c = UI.Lane(lane); var r = cb.Resources;
        foreach (var st in new[] { "Checked", "CheckedPointerOver", "CheckedPressed" })
        {
            r["CheckBoxCheckBackgroundFill" + st] = c;
            r["CheckBoxCheckBackgroundStroke" + st] = c;
            r["CheckBoxCheckGlyphForeground" + st] = new SolidColorBrush(Microsoft.UI.Colors.White);
        }
        r["CheckBoxCheckBackgroundStrokeUnchecked"] = UI.Res("Line2Brush");
        r["CheckBoxCheckBackgroundStrokeUncheckedPointerOver"] = c;
        r["CheckBoxCheckBackgroundFillUncheckedPointerOver"] = UI.Tint(c, 0.12);
    }

    static bool Inside(DependencyObject? el, DependencyObject parent)
    {
        while (el != null) { if (el == parent) return true; el = VisualTreeHelper.GetParent(el); }
        return false;
    }

    static void Strike(Grid g, bool done)
    {
        if (g.Children[1] is StackPanel sp && sp.Children[0] is StackPanel tr && tr.Children[0] is TextBlock title)
        {
            title.TextDecorations = done ? Windows.UI.Text.TextDecorations.Strikethrough : Windows.UI.Text.TextDecorations.None;
            sp.Opacity = done ? 0.55 : 1;
        }
    }

    // ---------- Edit tasks ----------
    void ToggleEdit()
    {
        bool open = editor.Visibility != Visibility.Visible;
        editor.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
        editBtn.Content = UI.Row(8, new FontIcon { Glyph = open ? "" : "", FontSize = 14 }, new TextBlock { Text = open ? "Done editing" : "Edit tasks" });
        if (open) DrawEditor();
    }

    void DrawEditor()
    {
        editor.Children.Clear();
        var list = Store.Tasks(lane);
        editor.Children.Add(UI.M("Move tasks up or down, delete them, or add your own."));
        for (int i = 0; i < list.Count; i++)
        {
            int idx = i;
            var g = new Grid { ColumnSpacing = 6 };
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var name = UI.T(list[i].Title, 14); name.VerticalAlignment = VerticalAlignment.Center;
            g.Children.Add(name);
            var up = UI.IconBtn("", "Move up", () => Move(idx, idx - 1)); up.IsEnabled = idx > 0;
            var down = UI.IconBtn("", "Move down", () => Move(idx, idx + 1)); down.IsEnabled = idx < list.Count - 1;
            var del = UI.IconBtn("", "Delete", () => Delete(idx));
            Grid.SetColumn(up, 1); Grid.SetColumn(down, 2); Grid.SetColumn(del, 3);
            g.Children.Add(up); g.Children.Add(down); g.Children.Add(del);
            editor.Children.Add(g);
        }
        var title = new TextBox { PlaceholderText = "New task" };
        var note = new TextBox { PlaceholderText = "Short note (optional)" };
        void Add()
        {
            var v = title.Text.Trim(); if (v.Length == 0) { title.Focus(FocusState.Programmatic); return; }
            var cur = Store.Tasks(lane); cur.Add((v, note.Text.Trim()));
            Store.SetTasks(lane, cur);
            DrawEditor();
        }
        title.KeyDown += (s, e) => { if (e.Key == Windows.System.VirtualKey.Enter) Add(); };
        editor.Children.Add(title);
        editor.Children.Add(note);
        editor.Children.Add(UI.Row(8, UI.Btn("Add task", Add, true), UI.Btn("Restore defaults", () => { Store.ResetTasks(lane); DrawEditor(); })));
    }

    void Move(int from, int to)
    {
        var cur = Store.Tasks(lane);
        if (to < 0 || to >= cur.Count) return;
        var done = cur.Select((t, i) => Store.Done(lane).Contains(i)).ToList();
        var item = cur[from]; cur.RemoveAt(from); cur.Insert(to, item);
        var d = done[from]; done.RemoveAt(from); done.Insert(to, d);
        Store.SetTasks(lane, cur, done.Select((x, i) => x ? i : -1).Where(i => i >= 0).ToList());
        DrawEditor();
    }

    void Delete(int index)
    {
        var before = Store.Tasks(lane); var beforeDone = Store.Done(lane);
        var cur = before.ToList(); var gone = cur[index]; cur.RemoveAt(index);
        var done = beforeDone.Where(x => x != index).Select(x => x > index ? x - 1 : x).ToList();
        Store.SetTasks(lane, cur, done);
        DrawEditor();
        ShellPage.Current?.ShowUndo($"Deleted “{gone.Title}”", () => { Store.SetTasks(lane, before, beforeDone); if (editor.Visibility == Visibility.Visible) DrawEditor(); });
    }
}
