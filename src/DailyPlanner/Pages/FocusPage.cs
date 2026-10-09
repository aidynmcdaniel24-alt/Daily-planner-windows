using DailyPlanner.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DailyPlanner.Pages;

// ===== Focus mode: only today's three checklists =====
public sealed class FocusPage : Page
{
    public FocusPage()
    {
        var s = new StackPanel { Spacing = 16, MaxWidth = 680, Padding = new Thickness(24, 28, 24, 100) };
        s.Children.Add(UI.PageTitle("Focus mode", "Just today's checklists", "Press Esc or the button below to leave."));
        foreach (var lane in Store.Lanes) s.Children.Add(new ChecklistCard(lane));
        Content = new ScrollViewer { Content = s, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }
}
