using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.UI;

namespace DailyPlanner.Services;

// ===== Small building blocks so every page looks the same =====
public static class UI
{
    // Looks up a color for the theme the app is showing (light or dark)
    public static Brush Res(string key)
    {
        var all = Application.Current.Resources;
        if (all.ThemeDictionaries.TryGetValue(Theme.Current, out var d) && d is ResourceDictionary rd && rd.TryGetValue(key, out var v) && v is Brush b) return b;
        return (Brush)all[key];
    }
    public static Brush Lane(string lane) => Res(char.ToUpper(lane[0]) + lane[1..] + "Brush");
    public static Brush Muted => Res("TextFillColorSecondaryBrush");
    public static Brush Brand => Res("BrandBrush");
    public static SolidColorBrush Solid(byte r, byte g, byte b, byte a = 255) => new(Color.FromArgb(a, r, g, b));
    public static Brush Tint(Brush b, double opacity) => new SolidColorBrush(((SolidColorBrush)b).Color) { Opacity = opacity };

    public static TextBlock T(string text, double size = 14, bool bold = false, Brush? color = null, bool wrap = true)
    {
        var t = new TextBlock { Text = text, FontSize = size, TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap };
        if (bold) t.FontWeight = FontWeights.SemiBold;
        if (color != null) t.Foreground = color;
        return t;
    }
    public static TextBlock M(string text, double size = 13) => T(text, size, false, Muted);

    public static FontFamily Mono => (FontFamily)Application.Current.Resources["GeistMono"];

    public static TextBlock Eyebrow(string text) => new()
    {
        Text = text.ToUpperInvariant(), FontSize = 12, FontWeight = FontWeights.SemiBold, CharacterSpacing = 100, Foreground = Res("AccentTextBrush"), FontFamily = Mono,
    };

    public static StackPanel Stack(double spacing = 12, params UIElement[] kids)
    {
        var s = new StackPanel { Spacing = spacing };
        foreach (var k in kids) s.Children.Add(k);
        return s;
    }

    public static StackPanel Row(double spacing = 8, params UIElement[] kids)
    {
        var s = new StackPanel { Orientation = Orientation.Horizontal, Spacing = spacing };
        foreach (var k in kids) s.Children.Add(k);
        return s;
    }

    // A rounded card with an icon, a title, an optional subtitle and something on the right
    public static Border Card(string glyph, string title, string? sub, Brush? color, FrameworkElement? right, params UIElement[] body)
    {
        var head = new Grid { ColumnSpacing = 12, Margin = new Thickness(0, 0, 0, 4) };
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var c = color ?? Brand;
        var icon = new Border { Width = 36, Height = 36, CornerRadius = new CornerRadius(11), Background = Tint(c, 0.14), BorderBrush = Tint(c, 0.22), BorderThickness = new Thickness(1), VerticalAlignment = VerticalAlignment.Top,
            Child = new FontIcon { Glyph = glyph, FontSize = 16, Foreground = c } };
        head.Children.Add(icon);
        var titles = Stack(1, T(title, 16, true));
        if (!string.IsNullOrEmpty(sub)) titles.Children.Add(M(sub));
        titles.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(titles, 1); head.Children.Add(titles);
        if (right != null) { right.VerticalAlignment = VerticalAlignment.Top; Grid.SetColumn(right, 2); head.Children.Add(right); }

        var inner = Stack(14, head);
        foreach (var b in body) inner.Children.Add(b);
        return Surface(inner);
    }

    // The card box itself: rounded, thin border that brightens a little on hover
    public static Border Surface(UIElement child, double padding = 22)
    {
        var card = new Border
        {
            Background = Res("CardBackgroundFillColorDefaultBrush"), BorderBrush = Res("CardStrokeColorDefaultBrush"),
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(18), Padding = new Thickness(padding), Child = child,
        };
        card.PointerEntered += (s, e) => card.BorderBrush = Res("Line2Brush");
        card.PointerExited += (s, e) => card.BorderBrush = Res("CardStrokeColorDefaultBrush");
        return card;
    }

    // A thin rounded progress bar (Value is 0 to 100)
    public sealed class Meter : Grid
    {
        readonly Border fill;
        double v;
        public Meter(Brush color, double height = 6)
        {
            Height = height; CornerRadius = new CornerRadius(height / 2); Background = Res("Line2Brush");
            fill = new Border { HorizontalAlignment = HorizontalAlignment.Left, CornerRadius = new CornerRadius(height / 2), Background = color, Width = 0 };
            Children.Add(fill);
            SizeChanged += (s, e) => Draw();
        }
        public double Value { get => v; set { v = Math.Clamp(value, 0, 100); Draw(); } }
        public Brush Color { set => fill.Background = value; }
        void Draw() => fill.Width = Math.Max(0, ActualWidth * v / 100);
    }

    // Progress ring with a faint track behind it
    public static ProgressRing Ring(double size, Brush color) =>
        new() { IsIndeterminate = false, Width = size, Height = size, Maximum = 100, Foreground = color, Background = Res("Line2Brush") };

    public static Border Pill(string text, Brush? fg = null, Brush? bg = null) => new()
    {
        CornerRadius = new CornerRadius(99), Padding = new Thickness(10, 3, 10, 4),
        Background = bg ?? Res("SubtleFillColorSecondaryBrush"),
        Child = new TextBlock { Text = text, FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = fg ?? Res("TextFillColorPrimaryBrush") },
    };

    public static Button Btn(string text, Action click, bool accent = false, string? glyph = null)
    {
        object content = text;
        if (glyph != null) content = Row(8, new FontIcon { Glyph = glyph, FontSize = 14 }, new TextBlock { Text = text });
        var b = new Button { Content = content, Padding = new Thickness(16, 8, 16, 9), CornerRadius = new CornerRadius(10) };
        if (accent) b.Style = (Style)Application.Current.Resources["AccentButtonStyle"];
        b.Click += (s, e) => click();
        return b;
    }

    public static Button IconBtn(string glyph, string tip, Action click)
    {
        var b = new Button { Content = new FontIcon { Glyph = glyph, FontSize = 13 }, Padding = new Thickness(8, 6, 8, 6) };
        ToolTipService.SetToolTip(b, tip);
        b.Click += (s, e) => click();
        return b;
    }

    // Two equal columns that stack on top of each other when there isn't room
    public static Grid Two(FrameworkElement a, FrameworkElement b, double gap = 12)
    {
        var g = new Grid { ColumnSpacing = gap, RowSpacing = gap };
        g.ColumnDefinitions.Add(new ColumnDefinition());
        g.ColumnDefinitions.Add(new ColumnDefinition());
        g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        g.Children.Add(a); g.Children.Add(b);
        void Fit(double w)
        {
            bool stack = w > 0 && w < 380;
            Grid.SetColumn(b, stack ? 0 : 1); Grid.SetRow(b, stack ? 1 : 0);
            Grid.SetColumnSpan(a, stack ? 2 : 1); Grid.SetColumnSpan(b, stack ? 2 : 1);
        }
        Fit(1000);
        g.SizeChanged += (s, e) => Fit(e.NewSize.Width);
        return g;
    }

    // Lays out boxes in as many columns as fit (like the website's tiles)
    public static Grid Wrap(IList<FrameworkElement> items, double minWidth, double gap = 12)
    {
        var g = new Grid { ColumnSpacing = gap, RowSpacing = gap };
        foreach (var it in items) g.Children.Add(it);
        int lastCols = -1;
        void Fit(double w)
        {
            int cols = Math.Max(1, Math.Min(items.Count, (int)((w + gap) / (minWidth + gap))));
            if (cols == lastCols) return; lastCols = cols;
            g.ColumnDefinitions.Clear(); g.RowDefinitions.Clear();
            for (int c = 0; c < cols; c++) g.ColumnDefinitions.Add(new ColumnDefinition());
            for (int r = 0; r < (items.Count + cols - 1) / cols; r++) g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            for (int i = 0; i < items.Count; i++) { Grid.SetColumn(items[i], i % cols); Grid.SetRow(items[i], i / cols); }
        }
        Fit(900);
        g.SizeChanged += (s, e) => Fit(e.NewSize.Width);
        return g;
    }

    // Choice chips (like the day picker): rounded, outlined, and tinted with a check when picked
    public static ToggleButton Chip(string text, bool on)
    {
        var b = new ToggleButton { Content = text, IsChecked = on, HorizontalAlignment = HorizontalAlignment.Stretch, CornerRadius = new CornerRadius(8), Padding = new Thickness(6, 7, 6, 8), MinWidth = 0 };
        // Soft look: outlined when off, light purple tint + purple text when on (not a solid fill)
        var tint = new SolidColorBrush(Color.FromArgb(0x2E, 0x6D, 0x5C, 0xFF));
        var tintHover = new SolidColorBrush(Color.FromArgb(0x40, 0x6D, 0x5C, 0xFF));
        var accent = new SolidColorBrush(Color.FromArgb(0xFF, 0x8B, 0x7D, 0xFF));
        var r = b.Resources;
        r["ToggleButtonBackgroundChecked"] = tint;
        r["ToggleButtonBackgroundCheckedPointerOver"] = tintHover;
        r["ToggleButtonBackgroundCheckedPressed"] = tint;
        r["ToggleButtonForegroundChecked"] = accent;
        r["ToggleButtonForegroundCheckedPointerOver"] = accent;
        r["ToggleButtonForegroundCheckedPressed"] = accent;
        r["ToggleButtonBorderBrushChecked"] = accent;
        r["ToggleButtonBorderBrushCheckedPointerOver"] = accent;
        r["ToggleButtonBorderBrushCheckedPressed"] = accent;
        r["ToggleButtonBackground"] = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        b.BorderThickness = new Thickness(1);
        return b;
    }

    // A scrolling page with a two-column layout on wide windows
    public sealed class Layout
    {
        public readonly ScrollViewer Root;
        public readonly StackPanel Top = new() { Spacing = 16 };
        public readonly StackPanel Main = new() { Spacing = 16 };
        public readonly StackPanel Side = new() { Spacing = 16 };
        readonly Grid cols = new() { ColumnSpacing = 18, RowSpacing = 16 };
        readonly StackPanel all = new() { Spacing = 16, MaxWidth = 1180 };

        public Layout()
        {
            cols.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.5, GridUnitType.Star) });
            cols.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            cols.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            cols.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            cols.Children.Add(Main); cols.Children.Add(Side);
            all.Children.Add(Top); all.Children.Add(cols);
            Main.ChildrenTransitions = new TransitionCollection { new EntranceThemeTransition { IsStaggeringEnabled = true } };
            Side.ChildrenTransitions = new TransitionCollection { new EntranceThemeTransition { IsStaggeringEnabled = true } };
            Root = new ScrollViewer { Content = all, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            Root.SizeChanged += (s, e) => Fit(e.NewSize.Width);
            Fit(1200);
        }

        void Fit(double w)
        {
            bool narrow = w < 860;
            all.Padding = w < 600 ? new Thickness(14, 16, 14, 24) : narrow ? new Thickness(22, 22, 22, 30) : new Thickness(36, 28, 36, 36);
            Grid.SetColumn(Side, narrow ? 0 : 1); Grid.SetRow(Side, narrow ? 1 : 0);
            Grid.SetColumnSpan(Main, narrow ? 2 : 1); Grid.SetColumnSpan(Side, narrow ? 2 : 1);
        }
    }

    // A page title like the website's: small colored label, big title, gray line
    public static StackPanel PageTitle(string eyebrow, string title, string? sub = null)
    {
        var t = T(title, 34, true); t.FontWeight = FontWeights.Bold; t.CharacterSpacing = -25;
        var s = Stack(6, Eyebrow(eyebrow), t);
        if (sub != null) s.Children.Add(M(sub, 15));
        return s;
    }

    // ---------- Simple charts ----------
    // Bars for 7 days; null values show as an empty dash
    public static Grid Bars(List<double?> values, List<string> labels, double max, Brush color, Func<double, string> fmt, double? goal = null, string? goalLabel = null)
    {
        var g = new Grid { Height = 150, ColumnSpacing = 6 };
        g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (int i = 0; i < values.Count; i++)
        {
            g.ColumnDefinitions.Add(new ColumnDefinition());
            var v = values[i];
            double h = v == null ? 3 : Math.Max(v > 0 ? 6 : 3, 120 * Math.Min(v.Value, max) / max);
            var bar = new Border
            {
                Height = h, VerticalAlignment = VerticalAlignment.Bottom, CornerRadius = new CornerRadius(6, 6, 2, 2),
                Background = v == null || v == 0 ? Res("SubtleFillColorSecondaryBrush") : color, MaxWidth = 34,
            };
            ToolTipService.SetToolTip(bar, labels[i] + ": " + (v == null ? "no data" : fmt(v.Value)));
            Grid.SetColumn(bar, i); g.Children.Add(bar);
            var lab = new TextBlock { Text = labels[i][..1], FontSize = 11, Foreground = Muted, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 4, 0, 0) };
            Grid.SetColumn(lab, i); Grid.SetRow(lab, 1); g.Children.Add(lab);
        }
        if (goal != null)
        {
            var line = new Border { Height = 1, BorderThickness = new Thickness(0, 1, 0, 0), BorderBrush = Muted, Opacity = 0.6,
                VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 120 * goal.Value / max) };
            Grid.SetColumnSpan(line, values.Count); g.Children.Add(line);
            if (goalLabel != null)
            {
                var gl = new TextBlock { Text = goalLabel, FontSize = 11, Foreground = Muted, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 120 * goal.Value / max + 2) };
                Grid.SetColumnSpan(gl, values.Count); g.Children.Add(gl);
            }
        }
        return g;
    }

    // A line chart that stretches to fit
    public static UIElement Line(List<double> values, Brush color, double height = 70)
    {
        if (values.Count < 2) return new Grid();
        double mn = values.Min(), mx = values.Max(), r = mx - mn; if (r == 0) r = 1;
        var pl = new Microsoft.UI.Xaml.Shapes.Polyline { Stroke = color, StrokeThickness = 3, Stretch = Stretch.Fill, StrokeLineJoin = PenLineJoin.Round, Height = height };
        for (int i = 0; i < values.Count; i++) pl.Points.Add(new Windows.Foundation.Point(i * 10, 50 - (values[i] - mn) / r * 50));
        return pl;
    }

    // A stat tile like the website's (label, big number, small note)
    public static Border Tile(string label, string value, string sub)
    {
        var l = T(label.ToUpperInvariant(), 11, true, Muted); l.FontFamily = Mono; l.CharacterSpacing = 80;
        var v = T(value, 28, true); v.FontWeight = FontWeights.Bold; v.CharacterSpacing = -20;
        return Surface(Stack(3, l, v, M(sub, 12)), 18);
    }

    public static string Plural(int n, string one, string many) => n + " " + (n == 1 ? one : many);
}
