using System.Diagnostics;
using DailyPlanner.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;

namespace DailyPlanner.Pages;

public sealed partial class HomePage : Page
{
    int quoteShift;
    (string lane, int index)? next;
    static readonly Dictionary<string, string> Icons = new() { ["gaming"] = "", ["sleep"] = "", ["coding"] = "" };

    public HomePage()
    {
        InitializeComponent();
        SetupBar.Closed += (s, e) => { SetupBar.Tag = "seen"; SetupBar.Visibility = Visibility.Collapsed; };
        VerifyBar.Closed += (s, e) => VerifyBar.Visibility = Visibility.Collapsed;
        SetupBar.Visibility = Visibility.Collapsed;
        VerifyBar.Visibility = Visibility.Collapsed;
        SizeChanged += (s, e) => Layout(e.NewSize.Width);
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        Store.Changed += Draw;
        Draw();
        _ = CheckVerified();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e) => Store.Changed -= Draw;

    // Two columns on wide windows, one on narrow ones
    void Layout(double width)
    {
        bool narrow = width < 820;
        Grid.SetColumn(TodayCard, narrow ? 0 : 1);
        Grid.SetRow(TodayCard, narrow ? 1 : 0);
        Grid.SetColumnSpan(NextCard, narrow ? 2 : 1);
        Grid.SetColumnSpan(TodayCard, narrow ? 2 : 1);
        Root.Padding = narrow ? new Thickness(18, 20, 18, 28) : new Thickness(36, 28, 36, 36);
        Hello.FontSize = narrow ? 30 : 38;
    }

    void Draw()
    {
        // Greeting
        int h = DateTime.Now.Hour;
        string part = h < 5 ? "Up late" : h < 12 ? "Good morning" : h < 17 ? "Good afternoon" : "Good evening";
        Hello.Text = part + (Store.Name.Length > 0 ? ", " + Store.Name : "");
        DateText.Text = DateTime.Now.ToString("dddd, MMMM d").ToUpperInvariant();
        SubText.Text = h < 5 ? "Get some sleep. Your plan will be here tomorrow." : "Here's your plan for today.";
        Goals.Children.Clear();
        foreach (var g in Store.Goals)
            Goals.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(99), Padding = new Thickness(10, 3, 10, 4),
                Background = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"],
                Child = new TextBlock { Text = g, FontSize = 12, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
            });
        SetupBar.IsOpen = !Store.SetUp && SetupBar.Tag == null;
        SetupBar.Visibility = SetupBar.IsOpen ? Visibility.Visible : Visibility.Collapsed;

        // Rings and lanes
        int all = 0, done = 0;
        Lanes.Children.Clear();
        foreach (var lane in Store.Lanes)
        {
            var (k, n) = Store.Count(lane);
            all += n; done += k;
            Lanes.Children.Add(LaneRow(lane, k, n));
        }
        int pct = all == 0 ? 0 : (int)Math.Round(100.0 * done / all);
        AllRing.Value = pct;
        AllPct.Text = pct + "%";

        // Next unchecked task: gaming, then coding, then sleep
        next = null;
        foreach (var lane in new[] { "gaming", "coding", "sleep" })
        {
            var item = Store.Items(lane).FirstOrDefault(t => !t.Done);
            if (item != null) { next = (lane, item.Index); NextTitle.Text = item.Title; NextNote.Text = Store.LaneNames[lane] + (item.Note.Length > 0 ? " · " + item.Note : ""); break; }
        }
        if (next == null)
        {
            NextLabel.Text = "ALL DONE";
            NextTitle.Text = "Every checklist is finished for today.";
            NextNote.Text = "Nice work. Rest up and come back tomorrow.";
            NextDone.Visibility = Visibility.Collapsed;
        }
        else { NextLabel.Text = "NEXT UP"; NextDone.Visibility = Visibility.Visible; }

        var q = Quotes.ForNow(quoteShift);
        QuoteText.Text = "“" + q.Text + "”";
        QuoteBy.Text = "— " + q.Author;
    }

    UIElement LaneRow(string lane, int k, int n)
    {
        var color = (Brush)Application.Current.Resources[char.ToUpper(lane[0]) + lane[1..] + "Brush"];
        int streak = Store.Streak(lane);
        var ring = new Grid { Width = 38, Height = 38 };
        ring.Children.Add(new ProgressRing { IsIndeterminate = false, Width = 38, Height = 38, Maximum = 100, Value = n == 0 ? 0 : 100.0 * k / n, Foreground = color });
        ring.Children.Add(new FontIcon { Glyph = Icons[lane], FontSize = 15, Foreground = color, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });

        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(new TextBlock { Text = Store.LaneNames[lane], FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        text.Children.Add(new TextBlock
        {
            Text = n > 0 && k == n ? "All done" : $"{k}/{n} done", FontSize = 12,
            Foreground = n > 0 && k == n ? (Brush)Application.Current.Resources["CodingBrush"] : (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
        });

        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(ring);
        Grid.SetColumn(text, 1); grid.Children.Add(text);
        if (streak > 0)
        {
            var sk = new TextBlock { Text = "\U0001F525 " + streak, FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = (Brush)Application.Current.Resources["StreakBrush"], VerticalAlignment = VerticalAlignment.Center };
            ToolTipService.SetToolTip(sk, streak + " day streak");
            Grid.SetColumn(sk, 2); grid.Children.Add(sk);
        }

        var b = new Button
        {
            Content = grid, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent), BorderThickness = new Thickness(0), Padding = new Thickness(6),
        };
        b.Click += (s, e) => ShellPage.Current?.OpenLane(lane);
        return b;
    }

    void NextDone_Click(object sender, RoutedEventArgs e)
    {
        if (next is { } n) Store.SetDone(n.lane, n.index, true);
    }

    void NewQuote_Click(object sender, RoutedEventArgs e) { quoteShift++; Draw(); }

    void Setup_Click(object sender, RoutedEventArgs e)
    {
        SetupBar.Tag = "seen";
        Process.Start(new ProcessStartInfo(Config.Website + "setup/") { UseShellExecute = true });
    }

    async Task CheckVerified()
    {
        if (Auth.Current == null) return;
        await Auth.RefreshProfile();
        VerifyBar.IsOpen = Auth.Current is { UsesPassword: true, Verified: false };
        VerifyBar.Visibility = VerifyBar.IsOpen ? Visibility.Visible : Visibility.Collapsed;
    }

    async void Resend_Click(object sender, RoutedEventArgs e)
    {
        try { await Auth.SendVerifyEmail(); VerifyBar.Message = "Sent! Check your inbox (and spam)."; }
        catch (Exception ex) { VerifyBar.Message = Auth.Friendly(ex); }
    }
}
