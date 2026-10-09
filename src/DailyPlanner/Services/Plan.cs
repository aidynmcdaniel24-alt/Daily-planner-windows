using System.Text.Json.Nodes;

namespace DailyPlanner.Services;

// ===== Builds today's checklists the same way the website does =====
public static class Plan
{
    public static int Weekday => ((int)DateTime.Now.DayOfWeek + 6) % 7;   // Monday = 0

    public static bool FullDay
    {
        get
        {
            var fd = Store.St["fd"] as JsonArray;
            var days = fd?.Select(x => (int)Store.Num(x, -1)).ToList() ?? new List<int> { 0, 1, 2 };
            return days.Contains(Weekday);
        }
    }

    // Today's practice focus (what the drill and "Aim focus" line show)
    static (int, string) focusKey = (-1, ""); static string focusCache = "";
    public static string Focus
    {
        get
        {
            var key = (Store.Version, Store.Td());
            if (key != focusKey) { focusCache = BuildFocus(); focusKey = key; }
            return focusCache;
        }
    }

    static string BuildFocus()
    {
        {
            var st = Store.St; var G = GameData.Genre(st); string gid = GameData.GenreId(st);
            bool full = FullDay; int W = Weekday;
            var gf = GameData.GameFocus(st["gm"]?.ToString() ?? "");
            var drills = GameData.Drills(G);
            var rot = gf.Count >= 4 && gid == "fps" ? gf : drills.Take(3).Select(d => d.Name).Concat(new[] { GameData.G(G, "warm") }).ToList();
            while (rot.Count < 4) rot.Add(GameData.G(G, "warm"));
            var P = st["plan"] as JsonObject;
            bool samePlan = P != null && (P["gg"]?.ToString() ?? "fps") == gid;
            var pd = GameData.Strings(P?["drills"]);
            if (samePlan && pd.Count > 0) return full ? pd[W % pd.Count] : pd[0];
            return full ? rot[W % 3] : rot[3];
        }
    }

    static string Words(string t, JsonObject G) => t
        .Replace("@skill", GameData.G(G, "skill")).Replace("@play", GameData.G(G, "play"))
        .Replace("Aim training", GameData.G(G, "skill")).Replace("Aim warm-up", GameData.G(G, "skill") + " warm-up")
        .Replace("Ranked block", GameData.G(G, "play"));

    // The built-in list for a lane (before the person's own edits)
    public static List<(string Title, string Note)> Defaults(string lane)
    {
        var st = Store.St;
        if (lane == "sleep") return new() { ("Pick tonight's bedtime", "Same time as work allows"), ("No ranked in the last hour", "Wind down instead"), ("Screens off 30-60 min before bed", "Dim lights, stretch"), ("Skip late caffeine", "Water instead"), ("Wake at your set time", "No snooze spiral") };
        if (lane == "coding") return new() { ("Study 20-45 min", "CS50, freeCodeCamp, or The Odin Project"), ("Build or fix one small thing", "Even a tiny script counts"), ("Add a line to your learning log", "Below on this tab"), ("Push your work to GitHub", "If you made something today") };

        var G = GameData.Genre(st); bool full = FullDay; string foc = Focus;
        string skill = GameData.G(G, "skill"), play = GameData.G(G, "play"), review = GameData.G(G, "review");
        if (st["plan"] is JsonObject P && P["full"] is JsonArray)
        {
            var src = (full ? P["full"] : P["short"]) as JsonArray ?? new JsonArray();
            return src.OfType<JsonArray>().Select(t =>
            {
                string a = Words(t[0]?.ToString() ?? "", G), b = t.Count > 1 ? t[1]?.ToString() ?? "" : "";
                return (a, b == "@foc" ? foc : b == "@rev" ? review : b);
            }).ToList();
        }
        return full
            ? new() { ("Wake up at your set time", "Water and food first"), ("Tech learning, 45 min", "Check the Coding tab"), (skill + ", 15 min", foc), (play + " 1, 60-90 min", "Bring your focus goal into every game"), ("Break, 15 min", "Walk, water, no screen"), (play + " 2, 60-90 min", "Stop early on the 2-loss rule"), ("Review one game, 15 min", review) }
            : new() { (skill + " warm-up, 10 min", foc), ("Tech learning, 20-30 min", "Even a little keeps the streak"), (play + ", 60 min", "Only if you have time"), ("Write one fix", "Quick note, then done") };
    }
}
