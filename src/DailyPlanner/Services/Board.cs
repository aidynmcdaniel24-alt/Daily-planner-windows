using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace DailyPlanner.Services;

public record BoardRow(string Uid, string Name, string Code, int Streak, int Best);

// ===== Friends leaderboard (same database as the website) =====
public static class Board
{
    const string ABC = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
    public static JsonObject? Lb => Store.St["lb"] as JsonObject;

    public static async Task<BoardRow?> Find(string code) =>
        (await Cloud.WhereIn("board", "code", new[] { code })).Select(Row).FirstOrDefault();

    static BoardRow Row((string Id, JsonObject D) x) => new(x.Id, x.D["name"]?.ToString() ?? "?", x.D["code"]?.ToString() ?? "",
        (int)Store.Num(x.D["streak"]), (int)Store.Num(x.D["best"]));

    public static async Task<List<BoardRow>> Rows()
    {
        if (Lb == null) return new();
        var codes = new List<string> { Lb["code"]?.ToString() ?? "" }.Concat(GameData.Strings(Lb["fr"])).Where(c => c.Length > 0).Distinct().ToList();
        var rows = (await Cloud.WhereIn("board", "code", codes)).Select(Row).ToList();
        return rows.OrderByDescending(r => r.Streak).ThenByDescending(r => r.Best).ToList();
    }

    public static async Task Join()
    {
        var s = Auth.Current ?? throw new ApiException("NOT_SIGNED_IN", 0);
        string code;
        do { code = new string(Enumerable.Range(0, 6).Select(_ => ABC[Random.Shared.Next(ABC.Length)]).ToArray()); }
        while (await Find(code) != null);
        string name = (Store.Name.Length > 0 ? Store.Name : s.Name.Split(' ')[0]);
        if (name.Length == 0 || NameProblem(name) != null) name = "Player";
        if (name.Length > 24) name = name[..24];
        Store.St["lb"] = new JsonObject { ["code"] = code, ["name"] = name, ["fr"] = new JsonArray() };
        Store.Save();
        await Update(force: true);
    }

    // Updates my entry if my streak or name changed
    public static async Task Update(bool force = false)
    {
        var s = Auth.Current; var lb = Lb;
        if (s == null || lb == null || lb["code"] == null) return;
        int streak = Stats.BestStreak;
        var mine = await Cloud.Get("board/" + s.Uid);
        int best = Math.Max(streak, mine == null ? 0 : (int)Store.Num(mine["best"]));
        string name = lb["name"]?.ToString() ?? "Player";
        if (!force && mine != null && (int)Store.Num(mine["streak"]) == streak && (int)Store.Num(mine["best"]) == best && mine["name"]?.ToString() == name) return;
        await Cloud.Commit(Cloud.Update("board/" + s.Uid, new JsonObject { ["name"] = name, ["code"] = lb["code"]!.ToString(), ["streak"] = streak, ["best"] = best }, "updated", replace: true));
    }

    public static async Task Leave()
    {
        var s = Auth.Current;
        if (s != null) { try { await Cloud.Commit(Cloud.Delete("board/" + s.Uid)); } catch { } }
        Store.St.Remove("lb");
        Store.Save();
    }

    public static async Task Nudge(string toUid)
    {
        var s = Auth.Current ?? throw new ApiException("NOT_SIGNED_IN", 0);
        await Cloud.Commit(Cloud.Update("nudges/" + toUid + "/in/" + s.Uid, new JsonObject { ["name"] = Lb?["name"]?.ToString() ?? "A friend" }, "time", replace: true));
        Store.Obj(Lb!, "nd")[toUid] = Store.Td();
        Store.Save();
    }

    public static bool Nudged(string uid) => (Lb?["nd"] as JsonObject)?[uid]?.ToString() == Store.Td();

    // Reads (and clears) the nudges friends sent me
    public static async Task<List<string>> TakeNudges()
    {
        var s = Auth.Current; if (s == null) return new();
        var list = await Cloud.List("nudges/" + s.Uid + "/in");
        if (list.Count > 0) await Cloud.Commit(list.Select(x => Cloud.Delete("nudges/" + s.Uid + "/in/" + x.Id)).ToArray());
        return list.Select(x => x.Data["name"]?.ToString() ?? "A friend").ToList();
    }

    // ---------- Name filter (same as the website) ----------
    static readonly string[] BadAny = { "fuck","shit","bitch","cunt","nigg","fagg","dick","pussy","whore","slut","bastard","retard","rape","porn",
        "kike","spic","chink","tranny","nazi","hitler","cock","twat","wank","jizz","cum","dildo","penis","vagina","boob","titt" };
    static readonly string[] BadWord = { "ass","fag","hoe","tit","sex","kys","gay","homo","dyke","coon","gook","wetback","jap" };
    static readonly string[] Safe = { "scunthorpe","cocktail","cockpit","peacock","hancock","dickens","cumulative","document","circumstance","therapist","grape","drape","sussex" };

    static string Normalize(string s) => new string(s.ToLowerInvariant().Select(c => c switch
    { '0' => 'o', '1' => 'i', '3' => 'e', '4' => 'a', '5' => 's', '7' => 't', '@' => 'a', '$' => 's', '!' => 'i', '|' => 'i', _ => c }).ToArray());

    public static string? NameProblem(string name)
    {
        string n = name.Trim();
        if (n.Length == 0) return "Type a name.";
        if (n.Length > 24) return "Names can be up to 24 characters.";
        if (!Regex.IsMatch(n, @"^[\p{L}\p{N} ._'-]+$")) return "Use letters, numbers, spaces, and . _ ' - only.";
        string flat = Regex.Replace(Normalize(n), "[^a-z]", "");
        foreach (var w in Safe) flat = flat.Replace(w, "");
        var words = Regex.Split(Normalize(n), "[^a-z]+").Where(w => w.Length > 0);
        if (BadAny.Any(flat.Contains) || words.Any(w => BadWord.Contains(w))) return "That name isn't allowed. Please pick another.";
        return null;
    }
}
