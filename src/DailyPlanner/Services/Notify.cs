using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;

namespace DailyPlanner.Services;

// ===== Windows notifications (bottom-right pop-ups) =====
public static class Notify
{
    static bool ready;

    public static void Start()
    {
        try { AppNotificationManager.Default.Register(); ready = true; }
        catch (Exception e) { App.Log("Notifications off: " + e.Message); }
    }

    public static void Stop()
    {
        try { if (ready) AppNotificationManager.Default.Unregister(); } catch { }
    }

    public static void Show(string title, string text)
    {
        if (!ready) return;
        try
        {
            var n = new AppNotificationBuilder().AddText(title).AddText(text).BuildNotification();
            AppNotificationManager.Default.Show(n);
        }
        catch { }
    }
}
