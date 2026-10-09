using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DailyPlanner.Services;

// ===== Signing in (talks to Firebase Authentication) =====
public class Session
{
    public string Uid { get; set; } = "";
    public string Email { get; set; } = "";
    public string Name { get; set; } = "";
    public bool Verified { get; set; }
    public bool UsesPassword { get; set; }
    public string RefreshToken { get; set; } = "";
    public long LoginAt { get; set; }      // when this computer signed in (for "Log out of all devices")
    public long SeenAt { get; set; }       // last time the app was opened (for the 14-day rule)
}

public static class Auth
{
    const string IdBase = "https://identitytoolkit.googleapis.com/v1/accounts:";
    static string Key => "?key=" + Config.ApiKey;

    public static Session? Current { get; private set; }
    static string idToken = "";
    static DateTime idTokenExpires = DateTime.MinValue;

    static string SessionFile => Path.Combine(Store.Folder, "session.dat");

    // ---------- Email and password ----------
    public static async Task SignIn(string email, string password)
    {
        var r = await Http.PostJson(IdBase + "signInWithPassword" + Key,
            new JsonObject { ["email"] = email, ["password"] = password, ["returnSecureToken"] = true });
        await Start(r, true);
    }

    public static async Task SignUp(string email, string password)
    {
        var r = await Http.PostJson(IdBase + "signUp" + Key,
            new JsonObject { ["email"] = email, ["password"] = password, ["returnSecureToken"] = true });
        await Start(r, true);
        try { await SendVerifyEmail(); } catch { }
    }

    public static async Task SendPasswordReset(string email) =>
        await Http.PostJson(IdBase + "sendOobCode" + Key,
            new JsonObject { ["requestType"] = "PASSWORD_RESET", ["email"] = email });

    public static async Task SendVerifyEmail() =>
        await Http.PostJson(IdBase + "sendOobCode" + Key,
            new JsonObject { ["requestType"] = "VERIFY_EMAIL", ["idToken"] = await Token() });

    // ---------- Google (opens the person's normal browser) ----------
    public static bool GoogleReady => Config.GoogleClientId.Length > 0 && Config.GoogleClientSecret.Length > 0;

    public static async Task SignInWithGoogle(CancellationToken cancel)
    {
        if (!GoogleReady) throw new ApiException("GOOGLE_NOT_SET_UP", 0);

        // A one-time secret so only this app can finish the sign-in (PKCE)
        string verifier = B64(RandomNumberGenerator.GetBytes(32));
        string challenge = B64(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        string state = B64(RandomNumberGenerator.GetBytes(16));

        // Listen on a free local port for Google to send the person back
        int port = FreePort();
        string redirect = $"http://127.0.0.1:{port}/";
        using var listener = new HttpListener();
        listener.Prefixes.Add(redirect);
        listener.Start();

        string url = "https://accounts.google.com/o/oauth2/v2/auth"
            + "?client_id=" + Uri.EscapeDataString(Config.GoogleClientId)
            + "&redirect_uri=" + Uri.EscapeDataString(redirect)
            + "&response_type=code&scope=" + Uri.EscapeDataString("openid email profile")
            + "&code_challenge=" + challenge + "&code_challenge_method=S256"
            + "&state=" + state + "&prompt=select_account";
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

        // Wait for the browser to come back (or the person to cancel)
        var getCtx = listener.GetContextAsync();
        var done = await Task.WhenAny(getCtx, Task.Delay(TimeSpan.FromMinutes(5), cancel));
        if (done != getCtx) throw new ApiException("GOOGLE_CANCELLED", 0);
        var ctx = await getCtx;

        string? code = ctx.Request.QueryString["code"];
        bool ok = code != null && ctx.Request.QueryString["state"] == state;
        await Reply(ctx.Response, ok);
        if (!ok) throw new ApiException("GOOGLE_CANCELLED", 0);

        // Trade the code for a Google ID token
        var tok = await Http.PostForm("https://oauth2.googleapis.com/token", new()
        {
            ["client_id"] = Config.GoogleClientId,
            ["client_secret"] = Config.GoogleClientSecret,
            ["code"] = code!,
            ["code_verifier"] = verifier,
            ["redirect_uri"] = redirect,
            ["grant_type"] = "authorization_code",
        });
        string googleIdToken = tok["id_token"]?.ToString() ?? throw new ApiException("GOOGLE_FAILED", 0);

        // Sign in to Firebase with the Google account (same account as the website)
        var r = await Http.PostJson(IdBase + "signInWithIdp" + Key, new JsonObject
        {
            ["postBody"] = "id_token=" + googleIdToken + "&providerId=google.com",
            ["requestUri"] = "http://localhost",
            ["returnSecureToken"] = true,
            ["returnIdpCredential"] = true,
        });
        await Start(r, true);
    }

    static async Task Reply(HttpListenerResponse res, bool ok)
    {
        string msg = ok ? "You're signed in. You can close this tab and go back to Daily Planner."
                        : "Sign-in was cancelled. You can close this tab.";
        string html = "<!doctype html><meta charset=utf-8><title>Daily Planner</title>"
            + "<body style=\"font-family:Segoe UI,sans-serif;background:#0a0c12;color:#e8eaf1;display:grid;place-items:center;height:100vh;margin:0\">"
            + "<div style=\"text-align:center\"><div style=\"font-size:44px\">" + (ok ? "&#10003;" : "&#10005;") + "</div><h2>" + msg + "</h2></div>";
        byte[] bytes = Encoding.UTF8.GetBytes(html);
        res.ContentType = "text/html; charset=utf-8";
        res.ContentLength64 = bytes.Length;
        await res.OutputStream.WriteAsync(bytes);
        res.Close();
    }

    static int FreePort()
    {
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        int p = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return p;
    }

    static string B64(byte[] b) => Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    // ---------- After any sign-in ----------
    static async Task Start(JsonObject r, bool fresh)
    {
        idToken = r["idToken"]?.ToString() ?? "";
        idTokenExpires = DateTime.UtcNow.AddSeconds(int.Parse(r["expiresIn"]?.ToString() ?? "3600") - 120);
        var s = new Session
        {
            Uid = r["localId"]?.ToString() ?? "",
            Email = r["email"]?.ToString() ?? "",
            Name = r["displayName"]?.ToString() ?? r["fullName"]?.ToString() ?? "",
            RefreshToken = r["refreshToken"]?.ToString() ?? "",
            LoginAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            SeenAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        };
        Current = s;
        await RefreshProfile();
        SaveSession();
        if (fresh) { try { await Cloud.RecordLogin(s.Uid); } catch { } }
    }

    // Reads name, email and "verified" from the account
    public static async Task RefreshProfile()
    {
        if (Current == null) return;
        try
        {
            var r = await Http.PostJson(IdBase + "lookup" + Key, new JsonObject { ["idToken"] = await Token() });
            var u = r["users"]?[0];
            if (u == null) return;
            Current.Email = u["email"]?.ToString() ?? Current.Email;
            Current.Name = u["displayName"]?.ToString() ?? Current.Name;
            Current.Verified = u["emailVerified"]?.ToString() == "true";
            Current.UsesPassword = u["passwordHash"] != null
                || (u["providerUserInfo"] as JsonArray)?.Any(p => p?["providerId"]?.ToString() == "password") == true;
            SaveSession();
        }
        catch { }
    }

    // A fresh ID token for talking to the database (renews itself every hour)
    public static async Task<string> Token()
    {
        if (Current == null) throw new ApiException("NOT_SIGNED_IN", 0);
        if (idToken.Length > 0 && DateTime.UtcNow < idTokenExpires) return idToken;
        var r = await Http.PostForm("https://securetoken.googleapis.com/v1/token" + Key, new()
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = Current.RefreshToken,
        });
        idToken = r["id_token"]?.ToString() ?? "";
        idTokenExpires = DateTime.UtcNow.AddSeconds(int.Parse(r["expires_in"]?.ToString() ?? "3600") - 120);
        var newRefresh = r["refresh_token"]?.ToString();
        if (!string.IsNullOrEmpty(newRefresh) && newRefresh != Current.RefreshToken) { Current.RefreshToken = newRefresh; SaveSession(); }
        return idToken;
    }

    // ---------- Remembering the sign-in on this computer ----------
    // Saved encrypted with Windows (only this Windows user can read it)
    public static bool Restore()
    {
        try
        {
            if (!File.Exists(SessionFile)) return false;
            byte[] raw = ProtectedData.Unprotect(File.ReadAllBytes(SessionFile), null, DataProtectionScope.CurrentUser);
            var s = JsonSerializer.Deserialize<Session>(raw);
            if (s == null || s.Uid.Length == 0 || s.RefreshToken.Length == 0) return false;
            long away = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - s.SeenAt;
            if (away > Config.MaxAwayDays * 86400000L) { File.Delete(SessionFile); Expired = true; return false; }
            s.SeenAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            Current = s;
            SaveSession();
            return true;
        }
        catch { return false; }
    }

    // True when we just signed someone out for being away too long
    public static bool Expired { get; set; }

    static void SaveSession()
    {
        if (Current == null) return;
        Directory.CreateDirectory(Store.Folder);
        byte[] raw = JsonSerializer.SerializeToUtf8Bytes(Current);
        File.WriteAllBytes(SessionFile, ProtectedData.Protect(raw, null, DataProtectionScope.CurrentUser));
    }

    public static void SignOut()
    {
        Current = null;
        idToken = "";
        try { File.Delete(SessionFile); } catch { }
    }

    // Friendly messages for Firebase error codes
    public static string Friendly(Exception e)
    {
        if (e is HttpRequestException || e is TaskCanceledException) return "No internet connection.";
        string code = e is ApiException a ? a.Code : "";
        return code switch
        {
            "INVALID_LOGIN_CREDENTIALS" or "INVALID_PASSWORD" or "EMAIL_NOT_FOUND" => "Wrong email or password.",
            "EMAIL_EXISTS" => "That email already has an account. Try Log in.",
            "INVALID_EMAIL" => "That email doesn't look right.",
            "WEAK_PASSWORD" or "PASSWORD_DOES_NOT_MEET_REQUIREMENTS" => "Use at least 8 characters with a letter and a number.",
            "TOO_MANY_ATTEMPTS_TRY_LATER" => "Too many tries. Wait a minute and try again.",
            "USER_DISABLED" => "This account has been turned off.",
            "OPERATION_NOT_ALLOWED" => "This sign-in type is turned off in Firebase.",
            "GOOGLE_NOT_SET_UP" => "Google sign-in isn't set up in the Windows app yet. Use email for now.",
            "GOOGLE_CANCELLED" => "Google sign-in was cancelled.",
            "API_KEY_HTTP_REFERRER_BLOCKED" or "PERMISSION_DENIED" => "The app isn't allowed to reach the server. Check the API key settings.",
            _ => "Something went wrong. Try again.",
        };
    }
}
