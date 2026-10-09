using System.Text.Json.Nodes;

namespace DailyPlanner.Services;

public record Drill(string Name, string Time, List<string> Steps, string Tip, List<string> Keys);
public record RankInfo(string Pts, List<string> Tiers, List<string> Div, List<string> Top);

// ===== Game types, drills, ranks, quotes and projects (copied from the website into data.json) =====
public static class GameData
{
    public static readonly JsonObject Data = Load();

    static JsonObject Load()
    {
        try { return JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Assets", "data.json"))) as JsonObject ?? new(); }
        catch (Exception e) { App.Log("data.json failed: " + e.Message); return new(); }
    }

    static JsonObject Genres => Data["GENRES"] as JsonObject ?? new();
    public static List<string> GenreOrder => Strings(Data["GENRE_ORDER"]);
    public static string GenreName(string id) => Genres[id]?["name"]?.ToString() ?? id;

    public static List<string> Strings(JsonNode? n) =>
        (n as JsonArray)?.Select(x => x?.ToString() ?? "").ToList() ?? new();

    public static string Norm(string? t) => new string((t ?? "").ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

    // Guesses the game type and the known game name from what the person typed
    public static (string G, string K) Detect(string? text)
    {
        string raw = (text ?? "").ToLowerInvariant(), flat = Norm(raw);
        var words = System.Text.RegularExpressions.Regex.Split(raw, "[^a-z0-9]+").Where(w => w.Length > 0).ToList();
        if (flat.Length == 0) return ("general", "");
        foreach (var g in (Data["GAMES"] as JsonArray) ?? new JsonArray())
        {
            if (g is not JsonArray a || a.Count < 3) continue;
            bool hit = Strings(a[2]).Any(w => w.StartsWith("=") ? words.Contains(w[1..]) : flat.Contains(w));
            if (hit) return (a[0]!.ToString(), a[1]!.ToString());
        }
        return ("general", "");
    }

    static string GameText(JsonObject s) => s["gn"]?.ToString() is { Length: > 0 } gn ? gn : s["gm"]?.ToString() ?? "";

    // The AI-made plan for this person's game (saved by the website in "gp")
    public static JsonObject? AiPlan(JsonObject s)
    {
        if (s["gp"] is not JsonObject gp || gp["plan"] is not JsonObject) return null;
        string g = gp["g"]?.ToString() ?? "";
        if (Genres[g] == null || gp["for"]?.ToString() != Norm(GameText(s))) return null;
        if (s["gg"]?.ToString() is { Length: > 0 } gg && gg != g) return null;
        return gp;
    }

    public static string GenreId(JsonObject s)
    {
        if (s["gg"]?.ToString() is { Length: > 0 } gg && Genres[gg] != null) return gg;
        if (AiPlan(s) is JsonObject gp) return gp["g"]!.ToString();
        return Detect(GameText(s)).G;
    }

    // Everything about this person's game type, with the AI plan on top if there is one
    public static JsonObject Genre(JsonObject s)
    {
        var g = (Genres[GenreId(s)] ?? Genres["general"] ?? new JsonObject()).DeepClone().AsObject();
        if (AiPlan(s) is JsonObject gp && gp["plan"] is JsonObject plan)
        {
            foreach (var kv in plan) if (kv.Key != "ranks" && kv.Key != "drills") g[kv.Key] = kv.Value?.DeepClone();
            if (plan["drills"] is JsonArray d) g["drills"] = d.DeepClone();
            g["ai"] = true;
            g["game"] = gp["name"]?.ToString();
        }
        return g;
    }

    public static string G(JsonObject genre, string key) => genre[key]?.ToString() ?? "";

    public static List<Drill> Drills(JsonObject genre) =>
        ((genre["drills"] as JsonArray) ?? new JsonArray()).OfType<JsonObject>().Select(d => new Drill(
            d["name"]?.ToString() ?? "", d["time"]?.ToString() ?? "", Strings(d["steps"]), d["tip"]?.ToString() ?? "", Strings(d["keys"]))).ToList();

    // Finds the drill guide that matches today's focus text
    public static Drill? DrillFor(JsonObject genre, string text)
    {
        var list = Drills(genre);
        if (list.Count == 0) return null;
        string t = (text ?? "").ToLowerInvariant();
        return list.FirstOrDefault(d => d.Name.ToLowerInvariant() == t)
            ?? list.FirstOrDefault(d => d.Keys.Any(k => t.Contains(k)))
            ?? list.FirstOrDefault(d => t.Contains(d.Name.ToLowerInvariant()))
            ?? list[0];
    }

    public static RankInfo Ranks(JsonObject s)
    {
        var g = Genre(s);
        if (AiPlan(s) is JsonObject gp && gp["plan"]?["ranks"] is JsonObject r)
        {
            var tiers = Strings(r["tiers"]); var top = Strings(r["top"]);
            if (tiers.Count > 0 || top.Count > 0)
                return new RankInfo(gp["plan"]?["pts"]?.ToString() ?? G(g, "pts"), tiers, Strings(r["div"]), top);
        }
        string k = Detect(GameText(s)).K; if (k.Length == 0) k = s["gm"]?.ToString() ?? "";
        if (Data["RANKS"]?[k] is JsonObject known)
            return new RankInfo(known["pts"]?.ToString() ?? "Points", Strings(known["tiers"]), Strings(known["div"]), Strings(known["top"]));
        return new RankInfo(G(g, "pts").Length > 0 ? G(g, "pts") : "Points", new(), new(), new());
    }

    public static List<string> GameFocus(string game) => Strings(Data["GAME_FOCUS"]?[game]);

    // ---------- Coding drills, tips and helpful sites ----------
    public record CodeDrill(Drill Drill, List<string> Paths);
    public record Site(string Name, string Url, string Desc, string Group, List<string> Paths);

    public static List<CodeDrill> CodeDrills() =>
        ((Data["CODE_DRILLS"] as JsonArray) ?? new JsonArray()).OfType<JsonObject>().Select(d => new CodeDrill(new Drill(
            d["name"]?.ToString() ?? "", d["time"]?.ToString() ?? "", Strings(d["steps"]), d["tip"]?.ToString() ?? "", new()), Strings(d["paths"]))).ToList();

    public static List<string> CodeTips() => Strings(Data["CODE_TIPS"]);

    public static List<Site> CodeSites() =>
        ((Data["CODE_SITES"] as JsonArray) ?? new JsonArray()).OfType<JsonArray>().Where(a => a.Count >= 5)
            .Select(a => new Site(a[0]!.ToString(), a[1]!.ToString(), a[2]!.ToString(), a[3]!.ToString(), Strings(a[4]))).ToList();

    // [type, title, description] — type "p" is Python, "w" is web
    public static List<(string Type, string Title, string Desc)> Projects() =>
        ((Data["PROJECTS"] as JsonArray) ?? new JsonArray()).OfType<JsonArray>()
            .Select(p => (p[0]?.ToString() ?? "", p[1]?.ToString() ?? "", p.Count > 2 ? p[2]?.ToString() ?? "" : "")).ToList();
}
