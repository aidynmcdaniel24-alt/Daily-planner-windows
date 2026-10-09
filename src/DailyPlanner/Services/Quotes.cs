namespace DailyPlanner.Services;

// Motivational quotes (same list as the website). One shows each hour.
public static class Quotes
{
    public static readonly (string Text, string Author)[] All =
    {
        ("It does not matter how slowly you go as long as you do not stop.", "Confucius"),
        ("The journey of a thousand miles begins with a single step.", "Lao Tzu"),
        ("Well done is better than well said.", "Benjamin Franklin"),
        ("Genius is one percent inspiration and ninety-nine percent perspiration.", "Thomas Edison"),
        ("The best way to predict the future is to invent it.", "Alan Kay"),
        ("Talk is cheap. Show me the code.", "Linus Torvalds"),
        ("The only way to do great work is to love what you do.", "Steve Jobs"),
        ("You miss 100% of the shots you don't take.", "Wayne Gretzky"),
        ("Success is the sum of small efforts, repeated day in and day out.", "Robert Collier"),
        ("Programs must be written for people to read, and only incidentally for machines to execute.", "Harold Abelson"),
        ("Simplicity is prerequisite for reliability.", "Edsger W. Dijkstra"),
        ("Discipline is the bridge between goals and accomplishment.", "Jim Rohn")
    };

    public static (string Text, string Author) ForNow(int shift = 0)
    {
        long hour = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 3600;
        return All[(int)((hour + shift) % All.Length)];
    }
}
