using System.Text.Json.Nodes;
using Microsoft.UI.Xaml;

namespace DailyPlanner.Services;

// ===== Coding focus session: counts down, and the minutes go into "cm" (minutes coded per day) =====
public static class FocusTimer
{
    public static int Minutes { get; private set; } = 25;
    public static int Left { get; private set; } = 25 * 60;    // seconds
    public static bool Running => tick != null;
    public static bool Started => Left < Minutes * 60;
    public static event Action? Changed;
    static DispatcherTimer? tick;
    static DateTime end;

    public static void Init()
    {
        if (Running || Started) return;
        Minutes = (int)Store.Num(Store.St["cmin"], 25);
        if (Minutes is not (25 or 45 or 60)) Minutes = 25;
        Left = Minutes * 60;
    }

    public static int MinsOn(string day) => (int)Store.Num((Store.St["cm"] as JsonObject)?[day]);

    static int Log()
    {
        int used = (Minutes * 60 - Math.Max(0, Left)) / 60;
        Left = Minutes * 60;
        if (used < 1) return 0;
        var cm = Store.Obj("cm");
        cm[Store.Td()] = MinsOn(Store.Td()) + used;
        string cut = Store.Td(60);
        foreach (var k in cm.Select(kv => kv.Key).Where(k => string.CompareOrdinal(k, cut) < 0).ToList()) cm.Remove(k);
        Store.Save();
        return used;
    }

    public static void SetMinutes(int m)
    {
        Halt(); if (Started) Log();
        Minutes = m; Left = m * 60;
        Store.St["cmin"] = m; Store.Save();
        Changed?.Invoke();
    }

    public static void StartPause()
    {
        if (Running) { Halt(); Changed?.Invoke(); return; }
        if (Left <= 0) Left = Minutes * 60;
        end = DateTime.UtcNow.AddSeconds(Left);
        tick = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        tick.Tick += (s, e) => Step();
        tick.Start();
        Changed?.Invoke();
    }

    // Stop button: saves the minutes done so far
    public static int Stop() { Halt(); int n = Log(); Changed?.Invoke(); return n; }

    // App closing mid-session still counts the minutes
    public static void Finish() { try { if (Started) { Halt(); Log(); } } catch { } }

    static void Halt() { tick?.Stop(); tick = null; }

    static void Step()
    {
        Left = Math.Max(0, (int)Math.Round((end - DateTime.UtcNow).TotalSeconds));
        if (Left <= 0)
        {
            Halt(); Log();
            Sound.Chime();
            Pages.ShellPage.Current?.ShowToast("Nice session", $"{Minutes} minutes added to your week.");
            Notify.Show("Focus session done", $"{Minutes} minutes of coding. Take a short break.");
        }
        Changed?.Invoke();
    }

    public static string Text => Left <= 0 ? "Done" : $"{Left / 60}:{Left % 60:00}";

    public static string Hm(int m) => m < 60 ? m + " min" : m / 60 + " h" + (m % 60 > 0 ? " " + m % 60 + " m" : "");
}
