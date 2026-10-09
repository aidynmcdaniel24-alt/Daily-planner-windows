using System.Globalization;
using System.Text.Json.Nodes;

namespace DailyPlanner.Services;

// ===== Talking to the online database (Cloud Firestore) =====
// Uses the same places as the website: users/{uid} (planner saved as text in "data"), board, nudges, logins.
public static class Cloud
{
    static string Root => $"projects/{Config.ProjectId}/databases/(default)/documents";
    static string Api => "https://firestore.googleapis.com/v1/";

    public record Pulled(JsonObject? Data, long Epoch);

    public static async Task<Pulled> Pull(string uid)
    {
        var doc = await Http.Get(Api + Root + "/users/" + uid, await Auth.Token());
        if (doc == null) return new Pulled(null, 0);
        var f = doc["fields"] as JsonObject;
        JsonObject? data = null;
        var text = f?["data"]?["stringValue"]?.ToString();
        if (!string.IsNullOrEmpty(text)) { try { data = JsonNode.Parse(text) as JsonObject; } catch { } }
        long.TryParse(f?["epoch"]?["integerValue"]?.ToString(), out long epoch);
        return new Pulled(data, epoch);
    }

    public static async Task Push(string uid, string json) =>
        await Commit(Update("users/" + uid, new JsonObject { ["data"] = json }, "updated"));

    // "Log out of all devices": every device that signed in before now gets signed out
    public static async Task KickAll(string uid) =>
        await Commit(Update("users/" + uid, new JsonObject { ["epoch"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }, "updated"));

    // Shows up in "Recent logins"
    public static async Task RecordLogin(string uid)
    {
        var w = Update("users/" + uid + "/logins/" + Guid.NewGuid().ToString("N")[..20],
            new JsonObject { ["device"] = "Daily Planner app on Windows" }, "time");
        w["currentDocument"] = new JsonObject { ["exists"] = false };
        await Commit(w);
    }

    public static async Task<List<(string Device, DateTime? Time)>> RecentLogins(string uid)
    {
        var list = (await List("users/" + uid + "/logins"))
            .Select(d => (Device: d.Data["device"]?.ToString() ?? "Unknown device", Time: d.Data["time"] is JsonValue t && DateTime.TryParse(t.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt) ? dt.ToLocalTime() : (DateTime?)null))
            .OrderByDescending(x => x.Time ?? DateTime.MinValue).Take(8).ToList();
        return list;
    }

    // ---------- General helpers ----------
    public static async Task<JsonObject?> Get(string path)
    {
        var doc = await Http.Get(Api + Root + "/" + path, await Auth.Token());
        return doc == null ? null : Plain(doc["fields"] as JsonObject);
    }

    // For things anyone can read (like the admin page's content)
    public static async Task<JsonObject?> GetPublic(string path)
    {
        using var res = await Http.Client.GetAsync(Api + Root + "/" + path + "?key=" + Config.ApiKey);
        if (!res.IsSuccessStatusCode) return null;
        var doc = JsonNode.Parse(await res.Content.ReadAsStringAsync()) as JsonObject;
        return Plain(doc?["fields"] as JsonObject);
    }

    public static async Task<List<(string Id, JsonObject Data)>> List(string collectionPath)
    {
        var r = await Http.Get(Api + Root + "/" + collectionPath + "?pageSize=100", await Auth.Token());
        return ((r?["documents"] as JsonArray) ?? new JsonArray()).OfType<JsonObject>()
            .Select(d => (Id: d["name"]!.ToString().Split('/').Last(), Data: Plain(d["fields"] as JsonObject))).ToList();
    }

    // Finds documents in a collection where a field is one of the given values
    public static async Task<List<(string Id, JsonObject Data)>> WhereIn(string collection, string field, IEnumerable<string> values)
    {
        var all = new List<(string, JsonObject)>();
        foreach (var chunk in values.Chunk(30))
        {
            var q = new JsonObject
            {
                ["structuredQuery"] = new JsonObject
                {
                    ["from"] = new JsonArray(new JsonObject { ["collectionId"] = collection }),
                    ["where"] = new JsonObject
                    {
                        ["fieldFilter"] = new JsonObject
                        {
                            ["field"] = new JsonObject { ["fieldPath"] = field },
                            ["op"] = chunk.Length == 1 ? "EQUAL" : "IN",
                            ["value"] = chunk.Length == 1 ? Typed(chunk[0]) : new JsonObject { ["arrayValue"] = new JsonObject { ["values"] = new JsonArray(chunk.Select(v => (JsonNode?)Typed(v)).ToArray()) } },
                        }
                    },
                }
            };
            var raw = await Http.PostRaw(Api + Root + ":runQuery", q, await Auth.Token());
            foreach (var row in (JsonNode.Parse(raw) as JsonArray) ?? new JsonArray())
                if (row?["document"] is JsonObject d) all.Add((d["name"]!.ToString().Split('/').Last(), Plain(d["fields"] as JsonObject)));
        }
        return all;
    }

    // Writes fields (plus a server timestamp field) to a document, keeping other fields
    public static JsonObject Update(string path, JsonObject fields, string? timeField, bool replace = false)
    {
        var typed = new JsonObject();
        foreach (var kv in fields) typed[kv.Key] = Typed(kv.Value);
        var w = new JsonObject { ["update"] = new JsonObject { ["name"] = Root + "/" + path, ["fields"] = typed } };
        if (!replace) w["updateMask"] = new JsonObject { ["fieldPaths"] = new JsonArray(fields.Select(kv => (JsonNode?)kv.Key).ToArray()) };
        if (timeField != null) w["updateTransforms"] = new JsonArray(new JsonObject { ["fieldPath"] = timeField, ["setToServerValue"] = "REQUEST_TIME" });
        return w;
    }

    public static JsonObject Delete(string path) => new() { ["delete"] = Root + "/" + path };

    public static async Task Commit(params JsonObject[] writes)
    {
        var body = new JsonObject { ["writes"] = new JsonArray(writes.Select(w => (JsonNode?)w).ToArray()) };
        await Http.PostJson(Api + Root + ":commit", body, await Auth.Token());
    }

    // ---------- Firestore's typed values <-> plain JSON ----------
    static JsonObject Typed(JsonNode? v)
    {
        switch (v)
        {
            case null: return new JsonObject { ["nullValue"] = null };
            case JsonObject o:
                var f = new JsonObject(); foreach (var kv in o) f[kv.Key] = Typed(kv.Value);
                return new JsonObject { ["mapValue"] = new JsonObject { ["fields"] = f } };
            case JsonArray a:
                return new JsonObject { ["arrayValue"] = new JsonObject { ["values"] = new JsonArray(a.Select(x => (JsonNode?)Typed(x)).ToArray()) } };
            default:
                var val = v.AsValue();
                if (val.TryGetValue<string>(out var s)) return new JsonObject { ["stringValue"] = s };
                if (val.TryGetValue<bool>(out var b)) return new JsonObject { ["booleanValue"] = b };
                if (val.TryGetValue<long>(out var l)) return new JsonObject { ["integerValue"] = l.ToString(CultureInfo.InvariantCulture) };
                if (val.TryGetValue<int>(out var i)) return new JsonObject { ["integerValue"] = i.ToString(CultureInfo.InvariantCulture) };
                double dbl = Store.Num(v);
                if (dbl == Math.Floor(dbl) && Math.Abs(dbl) < 9e15) return new JsonObject { ["integerValue"] = ((long)dbl).ToString(CultureInfo.InvariantCulture) };
                return new JsonObject { ["doubleValue"] = dbl };
        }
    }

    static JsonObject Typed(string s) => new() { ["stringValue"] = s };

    public static JsonObject Plain(JsonObject? fields)
    {
        var o = new JsonObject();
        if (fields == null) return o;
        foreach (var kv in fields) o[kv.Key] = Value(kv.Value as JsonObject);
        return o;
    }

    static JsonNode? Value(JsonObject? t)
    {
        if (t == null) return null;
        if (t["stringValue"] is JsonNode s) return s.ToString();
        if (t["integerValue"] is JsonNode i) return long.TryParse(i.ToString(), out var l) ? l : 0;
        if (t["doubleValue"] is JsonNode d) return Store.Num(d);
        if (t["booleanValue"] is JsonNode b) return b.ToString() == "true";
        if (t["timestampValue"] is JsonNode ts) return ts.ToString();
        if (t["mapValue"] is JsonObject m) return Plain(m["fields"] as JsonObject);
        if (t["arrayValue"] is JsonObject a) return new JsonArray(((a["values"] as JsonArray) ?? new JsonArray()).Select(x => Value(x as JsonObject)).ToArray());
        return null;
    }
}
