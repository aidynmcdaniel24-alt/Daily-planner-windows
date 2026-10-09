using System.Globalization;
using System.Text.Json.Nodes;
using Microsoft.UI.Dispatching;

namespace DailyPlanner.Services;

public record TaskItem(int Index, string Title, string Note, bool Done);

// ===== The planner's data (same format as the website, so they stay in sync) =====
public static class Store
{
    public static readonly string[] Lanes = { "gaming", "sleep", "coding" };
    public static readonly Dictionary<string, string> LaneNames = new() { ["gaming"] = "Gaming", ["sleep"] = "Sleep", ["coding"] = "Coding" };

    public static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DailyPlanner");
    static string StateFile => Path.Combine(Folder, "planner.json");

    public static JsonObject St { get; private set; } = new();
    public static event Action? Changed;          // anything in the planner changed (redraw)
    public static event Action<string>? Finished; // a whole checklist was just finished
    public static event Action? Kicked;           // "Log out of all devices" was used somewhere else
    public static event Action? Saved;            // the person changed something (after it's written)
    public static DispatcherQueue? Ui { get; set; }

    // Reads a number from the data, whatever form it was saved in
    public static double Num(JsonNode? n, double fallback = 0) =>
        n != null && double.TryParse(n.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : fallback;

    // ---------- Dates ----------
    public static string Td(int daysAgo = 0) => DateTime.Now.AddDays(-daysAgo).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    static int Weekday => ((int)DateTime.Now.DayOfWeek + 6) % 7;   // Monday = 0, like the website

    // ---------- Load and save ----------
    public static void Load()
    {
        try { St = File.Exists(StateFile) ? JsonNode.Parse(File.ReadAllText(StateFile)) as JsonObject ?? new() : new(); }
        catch { St = new(); }
    }

    static void WriteFile()
    {
        Directory.CreateDirectory(Folder);
        string tmp = StateFile + ".tmp";
        File.WriteAllText(tmp, St.ToJsonString());
        File.Move(tmp, StateFile, true);
    }

    public static void Save()
    {
        St["ts"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        WriteFile();
        QueuePush();
        Raise();
        Saved?.Invoke();
    }

    public static void Replace(JsonObject next)
    {
        St = next;
        WriteFile();
        Raise();
    }

    static void Raise()
    {
        if (Ui != null && !Ui.HasThreadAccess) Ui.TryEnqueue(() => Changed?.Invoke());
        else Changed?.Invoke();
    }

    public static string Acct => St["acct"]?.ToString() ?? "";
    public static bool IsGuest => Acct == "guest";
    public static string Name => St["nm"]?.ToString() ?? "";
    public static bool SetUp => St["done"] != null || St["nm"] != null || St["sk"] != null;

    public static IEnumerable<string> Goals =>
        (St["gl"]?.ToString() ?? "").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    // ---------- Today's checklists ----------
    // The person's own edited list (st.ct) if they have one, otherwise the built-in plan
    public static List<(string Title, string Note)> Tasks(string lane)
    {
        var custom = Parse((St["ct"] as JsonObject)?[lane]);
        return custom.Count > 0 ? custom : Plan.Defaults(lane);
    }

    public static bool FullDay => Plan.FullDay;

    static List<(string, string)> Parse(JsonNode? n)
    {
        var list = new List<(string, string)>();
        if (n is not JsonArray arr) return list;
        foreach (var item in arr)
            if (item is JsonArray pair && pair.Count > 0)
                list.Add((pair[0]?.ToString() ?? "", pair.Count > 1 ? pair[1]?.ToString() ?? "" : ""));
        return list;
    }

    // Saves an edited list (same place the website keeps it)
    public static void SetTasks(string lane, List<(string Title, string Note)> list, List<int>? doneToday = null)
    {
        var ct = Obj("ct");
        ct[lane] = new JsonArray(list.Select(t => (JsonNode?)new JsonArray(t.Title, t.Note)).ToArray());
        if (doneToday != null) Obj("dn")[Td() + lane] = new JsonArray(doneToday.Select(x => (JsonNode?)x).ToArray());
        UpdateFinished(lane);
        Save();
    }

    public static void ResetTasks(string lane)
    {
        Obj("ct").Remove(lane);
        Obj("dn").Remove(Td() + lane);
        UpdateFinished(lane);
        Save();
    }

    // ---------- Small helpers for reading and writing the data ----------
    public static JsonObject Obj(string key) => Obj(St, key);
    public static JsonObject Obj(JsonObject parent, string key)
    {
        if (parent[key] is JsonObject o) return o;
        var n = new JsonObject(); parent[key] = n; return n;
    }
    public static JsonArray Arr(string key)
    {
        if (St[key] is JsonArray a) return a;
        var n = new JsonArray(); St[key] = n; return n;
    }
    public static string Str(string key) => St[key]?.ToString() ?? "";

    public static List<int> Done(string lane)
    {
        var a = (St["dn"] as JsonObject)?[Td() + lane] as JsonArray;
        return a?.Select(x => (int)Num(x, -1)).Where(x => x >= 0).ToList() ?? new();
    }

    public static List<TaskItem> Items(string lane)
    {
        var done = Done(lane);
        return Tasks(lane).Select((t, i) => new TaskItem(i, t.Title, t.Note, done.Contains(i))).ToList();
    }

    public static (int done, int total) Count(string lane)
    {
        int n = Tasks(lane).Count;
        return (Math.Min(Done(lane).Count(i => i < n), n), n);
    }

    public static void SetDone(string lane, int index, bool done)
    {
        var dn = St["dn"] as JsonObject ?? new JsonObject();
        St["dn"] = dn;
        var list = Done(lane);
        bool wasFinished = IsFinished(lane);
        if (done && !list.Contains(index)) list.Add(index);
        if (!done) list.Remove(index);
        dn[Td() + lane] = new JsonArray(list.Select(x => (JsonNode?)x).ToArray());

        UpdateFinished(lane);
        Save();
        if (!wasFinished && IsFinished(lane)) Finished?.Invoke(lane);
    }

    // Marks the day finished (this is what streaks count), same as the website
    static void UpdateFinished(string lane)
    {
        var ok = Obj("ok");
        var (k, n) = Count(lane);
        if (n > 0 && k == n) ok[lane + Td()] = 1; else ok.Remove(lane + Td());
    }

    public static bool IsFinished(string lane) => (St["ok"] as JsonObject)?.ContainsKey(lane + Td()) == true;

    // Days in a row with the whole checklist done
    public static int Streak(string lane)
    {
        var ok = St["ok"] as JsonObject;
        if (ok == null) return 0;
        int s = 0, o = ok.ContainsKey(lane + Td()) ? 0 : 1;
        while (ok.ContainsKey(lane + Td(o))) { s++; o++; }
        return s;
    }

    // ---------- Syncing with the account ----------
    static CancellationTokenSource? pushWait;
    public static bool Syncing { get; private set; }
    public static event Action<bool>? SyncState;

    static void QueuePush()
    {
        var s = Auth.Current;
        if (s == null || Acct != s.Uid) return;
        pushWait?.Cancel();
        pushWait = new CancellationTokenSource();
        var token = pushWait.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(1500, token);
                SetSyncing(true);
                await Cloud.Push(s.Uid, St);
            }
            catch { }
            finally { if (!token.IsCancellationRequested) SetSyncing(false); }
        });
    }

    static void SetSyncing(bool on)
    {
        Syncing = on;
        if (Ui != null && !Ui.HasThreadAccess) Ui.TryEnqueue(() => SyncState?.Invoke(on));
        else SyncState?.Invoke(on);
    }

    // Matches the website's rule: the newer copy wins. Returns "kicked", "down", "same" or "up".
    public static async Task<string> SyncDown()
    {
        var s = Auth.Current;
        if (s == null) return "same";
        var got = await Cloud.Pull(s.Uid);
        if (got.Epoch > 0 && got.Epoch > s.LoginAt)
        {
            if (Ui != null) Ui.TryEnqueue(() => Kicked?.Invoke()); else Kicked?.Invoke();
            return "kicked";
        }
        var cloud = got.Data;
        long localTs = (long)Num(St["ts"]);
        long cloudTs = (long)Num(cloud?["ts"]);
        if (cloud != null && (Acct != s.Uid || cloudTs > localTs))
        {
            cloud["acct"] = s.Uid;
            if (Ui != null && !Ui.HasThreadAccess) Ui.TryEnqueue(() => Replace(cloud)); else Replace(cloud);
            return "down";
        }
        if (cloud != null && cloudTs == localTs) return "same";
        St["acct"] = s.Uid;
        if (localTs == 0) St["ts"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        WriteFile();
        await Cloud.Push(s.Uid, St);
        return "up";
    }

    public static void UseAsGuest()
    {
        St["acct"] = "guest";
        Save();
    }

    // Signing out clears the planner on this computer (it's still saved in the account)
    public static void ClearLocal()
    {
        St = new JsonObject();
        try { File.Delete(StateFile); } catch { }
        Raise();
    }
}
