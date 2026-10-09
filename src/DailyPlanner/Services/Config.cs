namespace DailyPlanner.Services;

// ===== Settings for Firebase and Google sign-in =====
// These match the website so both use the same accounts and data.
public static partial class Config
{
    // Firebase web API key (same project as the website)
    public const string ApiKey = "AIzaSyDCrg9rC3A_AFBqrYNDYLy3PwH5Y2MxvpA";
    public const string ProjectId = "daily-planner-8801e";

    // The website address. Sent with requests so the key's website restriction still allows the app.
    public const string Website = "https://aidynmcdaniel24-alt.github.io/Daily-planner/";

    // Google sign-in for desktop apps (Google Cloud > APIs & Services > Credentials > "Desktop app" client).
    // Leave empty until it's set up; the Google button will say it's not ready yet.
    public const string GoogleClientId = "204598190100-aijder1pj518p4tc6s5oip00ho8aqjml.apps.googleusercontent.com";
    // The client secret lives in Secrets.cs, which GitHub fills in at build time
    // from the GOOGLE_CLIENT_SECRET repository secret (so it isn't stored in the code).

    // Sign out if the app hasn't been opened for this many days (same as the website)
    public const int MaxAwayDays = 14;
}
