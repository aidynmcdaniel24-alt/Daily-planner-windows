using System.Text.Json.Nodes;

namespace DailyPlanner.Services;

// ===== Saving to and loading from the online database (Cloud Firestore) =====
// Uses the same place as the website: users/{uid} with the planner saved as text in "data".
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
        long epoch = 0;
        long.TryParse(f?["epoch"]?["integerValue"]?.ToString(), out epoch);
        return new Pulled(data, epoch);
    }

    public static async Task Push(string uid, JsonObject st)
    {
        var write = new JsonObject
        {
            ["update"] = new JsonObject
            {
                ["name"] = Root + "/users/" + uid,
                ["fields"] = new JsonObject { ["data"] = new JsonObject { ["stringValue"] = st.ToJsonString() } },
            },
            ["updateMask"] = new JsonObject { ["fieldPaths"] = new JsonArray("data") },
            ["updateTransforms"] = new JsonArray(new JsonObject { ["fieldPath"] = "updated", ["setToServerValue"] = "REQUEST_TIME" }),
        };
        await Commit(write);
    }

    // Shows up in "Recent logins" on the website's Settings page
    public static async Task RecordLogin(string uid)
    {
        string id = Guid.NewGuid().ToString("N")[..20];
        var write = new JsonObject
        {
            ["update"] = new JsonObject
            {
                ["name"] = Root + "/users/" + uid + "/logins/" + id,
                ["fields"] = new JsonObject { ["device"] = new JsonObject { ["stringValue"] = "Daily Planner app on Windows" } },
            },
            ["currentDocument"] = new JsonObject { ["exists"] = false },
            ["updateTransforms"] = new JsonArray(new JsonObject { ["fieldPath"] = "time", ["setToServerValue"] = "REQUEST_TIME" }),
        };
        await Commit(write);
    }

    static async Task Commit(JsonObject write)
    {
        var body = new JsonObject { ["writes"] = new JsonArray(write) };
        await Http.PostJson(Api + Root + ":commit", body, await Auth.Token());
    }
}
