using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;

namespace DailyPlanner.Services;

// One shared web client for every request
public static class Http
{
    public static readonly HttpClient Client = Make();

    static HttpClient Make()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        c.DefaultRequestHeaders.Referrer = new Uri(Config.Website);
        c.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("DailyPlannerWindows", "1.0"));
        return c;
    }

    public static async Task<JsonObject> PostJson(string url, JsonObject body, string? bearer = null)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json")
        };
        if (bearer != null) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        return await Send(req);
    }

    // Like PostJson, but returns the raw text (for answers that are a list instead of an object)
    public static async Task<string> PostRaw(string url, JsonObject body, string bearer)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json")
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        using var res = await Client.SendAsync(req);
        var text = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode) throw new ApiException(ErrorCode(text), (int)res.StatusCode);
        return text;
    }

    public static async Task<JsonObject> PostForm(string url, Dictionary<string, string> form)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = new FormUrlEncodedContent(form) };
        return await Send(req);
    }

    public static async Task<JsonObject?> Get(string url, string bearer)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        using var res = await Client.SendAsync(req);
        if (res.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        var text = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode) throw new ApiException(ErrorCode(text), (int)res.StatusCode);
        return JsonNode.Parse(text) as JsonObject ?? new JsonObject();
    }

    static async Task<JsonObject> Send(HttpRequestMessage req)
    {
        using var res = await Client.SendAsync(req);
        var text = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode) throw new ApiException(ErrorCode(text), (int)res.StatusCode);
        return JsonNode.Parse(text) as JsonObject ?? new JsonObject();
    }

    // Pulls the error code out of a Google error response, like "EMAIL_EXISTS"
    static string ErrorCode(string text)
    {
        try
        {
            var o = JsonNode.Parse(text);
            var err = o?["error"];
            if (err is JsonValue v) return v.ToString();
            var msg = err?["message"]?.ToString() ?? err?["status"]?.ToString();
            if (!string.IsNullOrEmpty(msg)) return msg.Split(' ')[0];
        }
        catch { }
        return "UNKNOWN";
    }
}

public class ApiException : Exception
{
    public string Code { get; }
    public int Status { get; }
    public ApiException(string code, int status) : base(code) { Code = code; Status = status; }
}
