using DailyPlanner.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;

namespace DailyPlanner.Pages;

public sealed partial class ChecklistPage : Page
{
    string lane = "gaming";
    bool drawing;
    static readonly Dictionary<string, (string icon, string sub)> Info = new()
    {
        ["gaming"] = ("", "Practice with a plan, then play."),
        ["sleep"] = ("", "Wind down and protect your sleep."),
        ["coding"] = ("", "A small step toward a career in tech."),
    };

    public ChecklistPage() { InitializeComponent(); }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        lane = e.Parameter as string ?? "gaming";
        var color = (Brush)Application.Current.Resources[char.ToUpper(lane[0]) + lane[1..] + "Brush"];
        Heading.Text = Store.LaneNames[lane];
        SubHeading.Text = lane == "gaming" ? (Store.FullDay ? "Full day: the longer routine." : "Busy day: the short routine.") : Info[lane].sub;
        Icon.Glyph = Info[lane].icon;
        Icon.Foreground = color;
        IconBox.Background = new SolidColorBrush(((SolidColorBrush)color).Color) { Opacity = 0.16 };
        Bar.Foreground = color;
        Store.Changed += Draw;
        Draw();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e) => Store.Changed -= Draw;

    void Draw()
    {
        drawing = true;
        var items = Store.Items(lane);
        var (k, n) = Store.Count(lane);
        CountText.Text = $"{k} of {n} done";
        int pct = n == 0 ? 0 : (int)Math.Round(100.0 * k / n);
        PctText.Text = pct + "%";
        Bar.Value = pct;
        int s = Store.Streak(lane);
        StreakText.Text = s > 0 ? $"\U0001F525 {s} day streak" : "No streak yet";

        // Only rebuild the rows when the tasks themselves changed (keeps animations smooth)
        string sig = string.Join("|", items.Select(i => i.Title + "\u0001" + i.Note));
        if (TaskList.Tag as string != sig)
        {
            TaskList.Tag = sig;
            TaskList.Children.Clear();
            foreach (var t in items) TaskList.Children.Add(Row(t, t.Index == items.Count - 1));
        }
        else
        {
            foreach (var t in items)
                if (TaskList.Children[t.Index] is Grid g && g.Children[0] is CheckBox cb) { cb.IsChecked = t.Done; Strike(g, t.Done); }
        }
        drawing = false;
    }

    Grid Row(TaskItem t, bool last)
    {
        var g = new Grid { Padding = new Thickness(12, 12, 12, 12), ColumnSpacing = 4, Tag = t.Index };
        var cb = new CheckBox { IsChecked = t.Done, VerticalAlignment = VerticalAlignment.Top, MinWidth = 0 };
        var text = new StackPanel { Margin = new Thickness(0, 5, 0, 0) };
        text.Children.Add(new TextBlock { Text = t.Title, FontSize = 15, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
        if (t.Note.Length > 0) text.Children.Add(new TextBlock { Text = t.Note, FontSize = 13, TextWrapping = TextWrapping.Wrap, Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"] });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.Children.Add(cb);
        Grid.SetColumn(text, 1); g.Children.Add(text);
        if (!last) { var line = new Border { Height = 1, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(4, 0, 4, -12), Background = (Brush)Application.Current.Resources["DividerStrokeColorDefaultBrush"] }; Grid.SetColumnSpan(line, 2); g.Children.Add(line); }
        Strike(g, t.Done);

        cb.Checked += (s, e) => { if (!drawing) Store.SetDone(lane, t.Index, true); };
        cb.Unchecked += (s, e) => { if (!drawing) Store.SetDone(lane, t.Index, false); };
        // Clicking anywhere on the row ticks it
        g.Tapped += (s, e) => { if (e.OriginalSource is FrameworkElement fe && IsInside(fe, cb)) return; cb.IsChecked = !(cb.IsChecked ?? false); };
        return g;
    }

    static bool IsInside(DependencyObject? el, DependencyObject parent)
    {
        while (el != null) { if (el == parent) return true; el = VisualTreeHelper.GetParent(el); }
        return false;
    }

    static void Strike(Grid g, bool done)
    {
        if (g.Children[1] is StackPanel sp && sp.Children[0] is TextBlock title)
        {
            title.TextDecorations = done ? Windows.UI.Text.TextDecorations.Strikethrough : Windows.UI.Text.TextDecorations.None;
            title.Opacity = done ? 0.55 : 1;
        }
    }
}
