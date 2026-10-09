using Microsoft.UI.Xaml;

namespace DailyPlanner.Services;

// ===== The break timer keeps running even if you switch pages =====
public static class BreakTimer
{
    public static int Minutes { get; private set; } = 10;
    public static int Left { get; private set; } = 600;     // seconds
    public static bool Running => tick != null;
    public static event Action? Changed;
    static DispatcherTimer? tick;
    static DateTime end;

    public static void Init()
    {
        Minutes = (int)Store.Num(Store.St["brk"], 10);
        if (Minutes is not (5 or 10 or 15)) Minutes = 10;
        if (!Running) Left = Minutes * 60;
    }

    public static void SetMinutes(int m)
    {
        Stop(); Minutes = m; Left = m * 60;
        Store.St["brk"] = m; Store.Save();
        Changed?.Invoke();
    }

    public static void StartPause()
    {
        if (Running) { Stop(); Changed?.Invoke(); return; }
        if (Left <= 0) Left = Minutes * 60;
        end = DateTime.UtcNow.AddSeconds(Left);
        tick = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        tick.Tick += (s, e) => Step();
        tick.Start();
        Changed?.Invoke();
    }

    public static void Reset() { Stop(); Left = Minutes * 60; Changed?.Invoke(); }

    static void Stop() { tick?.Stop(); tick = null; }

    static void Step()
    {
        Left = Math.Max(0, (int)Math.Round((end - DateTime.UtcNow).TotalSeconds));
        if (Left <= 0)
        {
            Stop();
            Sound.Chime();
            Pages.ShellPage.Current?.ShowToast("Break's over", "Back to it, with a clear head.");
            Notify.Show("Break's over", "Back to it, with a clear head.");
        }
        Changed?.Invoke();
    }

    public static string Text => Left <= 0 ? "Done" : $"{Left / 60}:{Left % 60:00}";
}
