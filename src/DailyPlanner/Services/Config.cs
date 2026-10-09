namespace DailyPlanner.Services;

// ===== Settings for Firebase and Google sign-in =====
// These match the website so both use the same accounts and data.
public static class Config
{
    // Firebase web API key (same project as the website)
    public const string ApiKey = "AIzaSyDCrg9rC3A_AFBqrYNDYLy3PwH5Y2MxvpA";
    public const string ProjectId = "daily-planner-8801e";

    // The website address. Sent with requests so the key's website restriction still allows the app.
    public const string Website = "https://aidynmcdaniel24-alt.github.io/Daily-planner/";

    // Google sign-in for desktop apps (Google Cloud > APIs & Services > Credentials > "Desktop app" client).
    // Leave empty until it's set up; the Google button will say it's not ready yet.
    public const string GoogleClientId = "";
    public const string GoogleClientSecret = "";  // Google says desktop app secrets are not truly secret, so this is OK here

    // Sign out if the app hasn't been opened for this many days (same as the website)
    public const int MaxAwayDays = 14;
}
