using Microsoft.UI;
using Microsoft.UI.Xaml;

namespace DailyPlanner.Services;

// Light, dark, or match Windows (saved in the planner as "md", like the website)
public static class Theme
{
    public static string Mode => Store.St["md"]?.ToString() ?? "auto";
    public static event Action? Changed;

    // "Dark" or "Light": what the app is showing right now
    public static string Current => Mode switch
    {
        "light" => "Light",
        "dark" => "Dark",
        _ => Application.Current.RequestedTheme == ApplicationTheme.Light ? "Light" : "Dark",
    };
    public static bool IsDark => Current == "Dark";

    public static void Apply(FrameworkElement root, Microsoft.UI.Windowing.AppWindow? window = null)
    {
        root.RequestedTheme = IsDark ? ElementTheme.Dark : ElementTheme.Light;
        try
        {
            // Window buttons (minimize, maximize, close) that match the theme
            var tb = (window ?? App.Window?.AppWindow)?.TitleBar;
            if (tb != null)
            {
                var fg = IsDark ? Windows.UI.Color.FromArgb(255, 232, 234, 241) : Windows.UI.Color.FromArgb(255, 17, 19, 26);
                var hover = IsDark ? Windows.UI.Color.FromArgb(40, 255, 255, 255) : Windows.UI.Color.FromArgb(20, 0, 0, 0);
                tb.ButtonBackgroundColor = Colors.Transparent;
                tb.ButtonInactiveBackgroundColor = Colors.Transparent;
                tb.ButtonForegroundColor = fg;
                tb.ButtonHoverForegroundColor = fg;
                tb.ButtonHoverBackgroundColor = hover;
                tb.ButtonPressedBackgroundColor = hover;
                tb.ButtonPressedForegroundColor = fg;
                tb.ButtonInactiveForegroundColor = IsDark ? Windows.UI.Color.FromArgb(255, 107, 113, 134) : Windows.UI.Color.FromArgb(255, 138, 144, 164);
            }
        }
        catch { }
    }

    public static void Set(string mode)
    {
        Store.St["md"] = mode;
        Store.Save();
        if (App.Window != null) Apply(App.Window.Root);
        Changed?.Invoke();
    }
}
