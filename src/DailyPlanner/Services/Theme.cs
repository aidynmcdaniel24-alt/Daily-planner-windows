using Microsoft.UI.Xaml;

namespace DailyPlanner.Services;

// Light, dark, or match Windows (saved in the planner as "md", like the website)
public static class Theme
{
    public static string Mode => Store.St["md"]?.ToString() ?? "auto";

    public static void Apply(FrameworkElement root)
    {
        root.RequestedTheme = Mode switch
        {
            "light" => ElementTheme.Light,
            "dark" => ElementTheme.Dark,
            _ => ElementTheme.Default,
        };
    }

    public static void Set(string mode)
    {
        Store.St["md"] = mode;
        Store.Save();
        if (App.Window != null) Apply(App.Window.Root);
    }
}
