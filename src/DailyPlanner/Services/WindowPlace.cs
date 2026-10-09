using System.Text.Json;
using Microsoft.UI.Windowing;
using Windows.Graphics;

namespace DailyPlanner.Services;

// ===== Opens the window at a size that fits the screen, and remembers where you left it =====
public static class WindowPlace
{
    record Place(int X, int Y, int W, int H, bool Max);
    static string File => Path.Combine(Store.Folder, "window.json");

    public static void Restore(AppWindow w)
    {
        var area = DisplayArea.GetFromWindowId(w.Id, DisplayAreaFallback.Primary).WorkArea;
        Place? last = null;
        try { if (System.IO.File.Exists(File)) last = JsonSerializer.Deserialize<Place>(System.IO.File.ReadAllText(File)); } catch { }

        // Use the saved spot only if it still fits on this screen
        if (last != null && last.W >= 420 && last.H >= 500 && last.W <= area.Width + 16 && last.H <= area.Height + 16
            && last.X >= area.X - 16 && last.Y >= area.Y - 16 && last.X + last.W <= area.X + area.Width + 16 && last.Y + last.H <= area.Y + area.Height + 16)
        {
            w.MoveAndResize(new RectInt32(last.X, last.Y, last.W, last.H));
            if (last.Max && w.Presenter is OverlappedPresenter op) op.Maximize();
            return;
        }

        // First time: about 80% of the screen, centered. Small screens (laptops) start maximized.
        if (area.Width < 1280 || area.Height < 760)
        {
            w.MoveAndResize(new RectInt32(area.X, area.Y, area.Width, area.Height));
            if (w.Presenter is OverlappedPresenter op2) op2.Maximize();
            return;
        }
        int width = Math.Min(1400, (int)(area.Width * 0.8)), height = Math.Min(940, (int)(area.Height * 0.85));
        w.MoveAndResize(new RectInt32(area.X + (area.Width - width) / 2, area.Y + (area.Height - height) / 2, width, height));
    }

    public static void Save(AppWindow w)
    {
        try
        {
            bool max = w.Presenter is OverlappedPresenter op && op.State == OverlappedPresenterState.Maximized;
            if (max && System.IO.File.Exists(File))
            {
                // Keep the normal size from before, just remember it was maximized
                var old = JsonSerializer.Deserialize<Place>(System.IO.File.ReadAllText(File));
                if (old != null) { System.IO.File.WriteAllText(File, JsonSerializer.Serialize(old with { Max = true })); return; }
            }
            Directory.CreateDirectory(Store.Folder);
            System.IO.File.WriteAllText(File, JsonSerializer.Serialize(new Place(w.Position.X, w.Position.Y, w.Size.Width, w.Size.Height, max)));
        }
        catch { }
    }
}
