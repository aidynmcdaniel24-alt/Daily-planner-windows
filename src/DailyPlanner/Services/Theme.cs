using Microsoft.UI;
using Microsoft.UI.Xaml;

namespace DailyPlanner.Services;

// Light, dark, or match Windows (saved in the planner as "md", like the website)
public static class Theme
{
    public static string Mode => Store.St["md"]?.ToString() ?? "auto";
    public static event Action? Changed;
    public static ApplicationTheme System { get; set; } = ApplicationTheme.Dark;   // what Windows is set to
    public static ResourceDictionary? Colors;                                         // our colors, for pop-ups

    // Dialogs open outside the window's layout, so give them our colors and theme too
    public static Microsoft.UI.Xaml.Controls.ContentDialog Themed(this Microsoft.UI.Xaml.Controls.ContentDialog d) => Dress(d);
    public static T Dress<T>(T el) where T : FrameworkElement
    {
        el.RequestedTheme = IsDark ? ElementTheme.Dark : ElementTheme.Light;
        if (Colors != null && !el.Resources.MergedDictionaries.Contains(Colors)) el.Resources.MergedDictionaries.Add(Colors);
        return el;
    }

    // "Dark" or "Light": what the app is showing right now
    public static string Current => Mode switch
    {
        "light" => "Light",
        "dark" => "Dark",
        _ => System == ApplicationTheme.Light ? "Light" : "Dark",
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
        if (mode == Mode) return;   // nothing changed (stops the page from reloading over and over)
        Store.St["md"] = mode;
        Store.Save();
        if (App.Window != null) Apply(App.Window.Root);
        Changed?.Invoke();
    }
}

