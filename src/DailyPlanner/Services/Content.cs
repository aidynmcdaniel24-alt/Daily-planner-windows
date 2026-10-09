using System.Text.Json.Nodes;

namespace DailyPlanner.Services;

public record Challenge(string Id, string Text, int Goal);

// ===== Quotes, Bible verses, moods, weekly challenges and "What's new" =====
// Starts with the website's built-in lists, then uses anything changed on the admin page.
public static class Content
{
    public static List<(string Q, string A)> QuotesList = new();
    public static List<string> Verses = new();
    public static Dictionary<string, (List<(string Q, string A)> Q, List<string> V)> Moods = new();
    public static List<Challenge> Challenges = new();
    public static string NewsVersion = "";
    public static List<string> NewsItems = new();
    static readonly Dictionary<string, string> verseText = new();
    static string CacheFile => Path.Combine(Store.Folder, "verses.json");

    static Content()
    {
        Apply(GameData.Data["DEFAULT_CONTENT"] as JsonObject ?? new JsonObject(), true);
        if (GameData.Data["VERSE_TEXT"] is JsonObject vt) foreach (var kv in vt) verseText[kv.Key] = kv.Value?.ToString() ?? "";
        try
        {
            if (File.Exists(CacheFile) && JsonNode.Parse(File.ReadAllText(CacheFile)) is JsonObject c)
                foreach (var kv in c) verseText[kv.Key] = kv.Value?.ToString() ?? "";
        }
        catch { }
    }

    static List<(string, string)> Pairs(JsonNode? n)
    {
        var list = new List<(string, string)>();
        foreach (var x in (n as JsonArray) ?? new JsonArray())
        {
            if (x is JsonArray a && a.Count >= 2) list.Add((a[0]?.ToString() ?? "", a[1]?.ToString() ?? ""));
            else if (x is JsonObject o && o["q"] != null) list.Add((o["q"]!.ToString(), o["a"]?.ToString() ?? ""));
        }
        return list.Where(p => p.Item1.Length > 0).ToList();
    }

    static void Apply(JsonObject d, bool defaults)
    {
        var q = Pairs(d["quotes"]); if (q.Count > 0) QuotesList = q;
        var v = GameData.Strings(d["verses"]).Where(x => x.Length > 0).ToList(); if (v.Count > 0) Verses = v;
        if (d["moods"] is JsonObject m)
            foreach (var key in new[] { "focus", "loss", "tired" })
            {
                if (m[key] is not JsonObject mo) continue;
                var mq = Pairs(mo["q"]); var mv = GameData.Strings(mo["v"]);
                var cur = Moods.TryGetValue(key, out var c) ? c : (new List<(string, string)>(), new List<string>());
                Moods[key] = (mq.Count > 0 ? mq : cur.Item1, mv.Count > 0 ? mv : cur.Item2);
            }
        if (d["challenges"] is JsonArray ch)
        {
            if (defaults)
                Challenges = ch.OfType<JsonObject>().Where(x => x["on"]?.ToString() != "false")
                    .Select(x => new Challenge(x["id"]?.ToString() ?? "", x["text"]?.ToString() ?? "", (int)Store.Num(x["goal"], 1))).ToList();
            else
            {
                var all = GameData.Data["DEFAULT_CONTENT"]?["challenges"] as JsonArray ?? new JsonArray();
                Challenges = all.OfType<JsonObject>().Select(def =>
                {
                    var id = def["id"]?.ToString() ?? "";
                    var o = ch.OfType<JsonObject>().FirstOrDefault(y => y["id"]?.ToString() == id);
                    if (o == null) return (on: def["on"]?.ToString() != "false", c: new Challenge(id, def["text"]?.ToString() ?? "", (int)Store.Num(def["goal"], 1)));
                    string text = o["text"]?.ToString() is { Length: > 0 } t ? t : def["text"]?.ToString() ?? "";
                    return (on: o["on"]?.ToString() != "false", c: new Challenge(id, text, Math.Clamp((int)Store.Num(o["goal"], 1), 1, 50)));
                }).Where(x => x.on).Select(x => x.c).ToList();
            }
        }
        if (d["news"] is JsonObject n && n["v"]?.ToString() is string nv && (defaults || Newer(nv, NewsVersion)))
        {
            NewsVersion = nv;
            NewsItems = GameData.Strings(n["items"]);
        }
    }

    // true if version a is higher than b ("1.10" > "1.9")
    public static bool Newer(string a, string b)
    {
        var x = a.Split('.'); var y = b.Split('.');
        for (int i = 0; i < Math.Max(x.Length, y.Length); i++)
        {
            int p = i < x.Length && int.TryParse(x[i], out var pp) ? pp : 0, q = i < y.Length && int.TryParse(y[i], out var qq) ? qq : 0;
            if (p != q) return p > q;
        }
        return false;
    }

    // Gets the admin page's edits from the database (anyone can read them)
    public static async Task Refresh()
    {
        try
        {
            var doc = await Cloud.GetPublic("content/app");
            if (doc != null) Apply(doc, false);
        }
        catch { }
    }

    // ---------- The quote for this hour ----------
    public static (string Text, string By, string? VerseRef) Pick(string type, string mood, int shift)
    {
        var quotes = QuotesList; var verses = Verses;
        if (mood != "any" && Moods.TryGetValue(mood, out var m)) { quotes = m.Q; verses = m.V; }
        var items = new List<(string, string, string?)>();
        var qs = quotes.Select(q => (q.Q, q.A, (string?)null)).ToList();
        var vs = verses.Select(v => ("", v, (string?)v)).ToList();
        if (type == "moti") items = qs;
        else if (type == "faith") items = vs;
        else for (int i = 0; i < Math.Max(qs.Count, vs.Count); i++) { if (i < qs.Count) items.Add(qs[i]); if (i < vs.Count) items.Add(vs[i]); }
        if (items.Count == 0) items = qs.Count > 0 ? qs : vs;
        if (items.Count == 0) return ("Keep going.", "", null);
        long hour = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 3600;
        var it = items[(int)((hour + shift) % items.Count + items.Count) % items.Count];
        return it.Item3 == null ? (it.Item1, it.Item2, null) : (VerseText(it.Item3), it.Item3 + " (KJV)", it.Item3);
    }

    public static string VerseText(string reference) => verseText.TryGetValue(reference, out var t) ? t : "";

    // Loads a verse's exact text from bible-api.com and saves it
    public static async Task<bool> LoadVerse(string reference)
    {
        if (VerseText(reference).Length > 0) return true;
        try
        {
            string url = "https://bible-api.com/" + Uri.EscapeDataString(reference).Replace("%20", "+") + "?translation=kjv";
            var r = JsonNode.Parse(await new HttpClient { Timeout = TimeSpan.FromSeconds(10) }.GetStringAsync(url));
            string text = System.Text.RegularExpressions.Regex.Replace(r?["text"]?.ToString() ?? "", @"\s+", " ").Trim();
            if (text.Length == 0) return false;
            verseText[reference] = text;
            var o = new JsonObject(); foreach (var kv in verseText) o[kv.Key] = kv.Value;
            Directory.CreateDirectory(Store.Folder);
            File.WriteAllText(CacheFile, o.ToJsonString());
            return true;
        }
        catch { return false; }
    }
}
