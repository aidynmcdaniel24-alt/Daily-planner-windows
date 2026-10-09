using System.Text.Json.Nodes;

namespace DailyPlanner.Services;

// ===== Numbers for the Summary tab and challenges (same rules as the website) =====
public static class Stats
{
    public static double? Hours(string? b, string? w)
    {
        if (string.IsNullOrEmpty(b) || string.IsNullOrEmpty(w)) return null;
        var x = b.Split(':'); var y = w.Split(':');
        if (x.Length < 2 || y.Length < 2) return null;
        int m = (int.Parse(y[0]) * 60 + int.Parse(y[1])) - (int.Parse(x[0]) * 60 + int.Parse(x[1]));
        if (m <= 0) m += 1440;
        return m / 60.0;
    }

    public static double? SleepOn(string day)
    {
        var e = (Store.St["sl"] as JsonObject)?[day] as JsonObject;
        return e == null ? null : Hours(e["b"]?.ToString(), e["w"]?.ToString());
    }

    public static bool Finished(string lane, string day) => (Store.St["ok"] as JsonObject)?.ContainsKey(lane + day) == true;
    public static int DaysFinished(string lane) => Enumerable.Range(0, 7).Count(i => Finished(lane, Store.Td(i)));
    public static int BestStreak => Store.Lanes.Max(Store.Streak);
    public static int PerfectDays => Enumerable.Range(0, 7).Count(i => Store.Lanes.All(l => Finished(l, Store.Td(i))));

    public static List<(string d, string v)> Sessions => Store.Arr("tl").OfType<JsonArray>().Select(x => (x[0]?.ToString() ?? "", x[1]?.ToString() ?? "")).ToList();

    // ---------- This week (Monday to today) ----------
    public static List<string> WeekDays => Enumerable.Range(0, Plan.Weekday + 1).Select(i => Store.Td(i)).ToList();
    static bool InWeek(string? d) => d != null && WeekDays.Contains(d);
    public static string WeekKey => Store.Td(Plan.Weekday);

    public static int ChallengeCount(string id)
    {
        var D = WeekDays;
        return id switch
        {
            "calm" => Sessions.Count(x => InWeek(x.d) && x.v == "c"),
            "sleep" => D.Count(d => SleepOn(d) >= 7),
            "logs" => Store.Arr("tc").OfType<JsonObject>().Count(x => InWeek(x["d"]?.ToString())),
            "gday" => D.Count(d => Finished("gaming", d)),
            "cday" => D.Count(d => Finished("coding", d)),
            "score" => Store.Arr("sc").OfType<JsonObject>().Count(x => InWeek(x["d"]?.ToString())),
            "notes" => D.Count(d => ((Store.St["nt"] as JsonObject)?[d]?.ToString() ?? "").Trim().Length > 0),
            "all" => D.Count(d => Store.Lanes.All(l => Finished(l, d))),
            "rank" => Store.Arr("rkl").OfType<JsonObject>().Count(x => InWeek(x["d"]?.ToString())),
            _ => 0,
        };
    }

    // Three challenges a week, picked the same way as the website
    public static List<Challenge> ThisWeek()
    {
        var L = Content.Challenges; int n = L.Count; var outp = new List<Challenge>();
        if (n == 0) return outp;
        long seed = (long)Math.Floor((DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 86400000.0 + 3) / 7);
        for (int k = 0; outp.Count < Math.Min(3, n) && k < n * 4; k++)
        {
            var c = L[(int)((seed * 5 + k * 4) % n)];
            if (!outp.Contains(c)) outp.Add(c);
        }
        return outp;
    }

    // Percent of all tasks done on a day (for the 7-day chart)
    public static double DonePercent(string day)
    {
        int tot = 0, dn = 0;
        foreach (var c in Store.Lanes)
        {
            int n = Store.Tasks(c).Count; tot += n;
            var a = (Store.St["dn"] as JsonObject)?[day + c] as JsonArray;
            dn += Math.Min(a?.Count ?? 0, n);
        }
        return tot == 0 ? 0 : Math.Round(100.0 * dn / tot);
    }
}
