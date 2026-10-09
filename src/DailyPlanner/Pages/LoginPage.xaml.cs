using System.Text.RegularExpressions;
using DailyPlanner.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;

namespace DailyPlanner.Pages;

public sealed partial class LoginPage : Page
{
    bool signUp, busy;
    CancellationTokenSource? googleWait;

    public LoginPage()
    {
        InitializeComponent();
        if (Auth.Expired) Show("You were away for a while, so we signed you out to keep your account safe. Log in again.", InfoBarSeverity.Informational);
    }

    // ---------- Log in / Sign up switch (with a slide animation) ----------
    void Tabs_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        signUp = sender.SelectedItem == SignupTab;
        Title.Text = signUp ? "Create your account" : "Welcome back";
        Sub.Text = signUp ? "Save your plan and pick up on any device." : "Log in to sync your plan across devices.";
        GoText.Text = signUp ? "Create account" : "Log in";
        Password.PlaceholderText = signUp ? "Make a password" : "Your password";
        Forgot.Visibility = signUp ? Visibility.Collapsed : Visibility.Visible;
        Rules.Visibility = signUp ? Visibility.Visible : Visibility.Collapsed;
        GoogleText.Text = signUp ? "Sign up with Google" : "Continue with Google";
        Message.IsOpen = false;
        Slide(signUp ? 30 : -30);
    }

    void Slide(double from)
    {
        var t = new Microsoft.UI.Xaml.Media.TranslateTransform { X = from };
        Body.RenderTransform = t;
        Body.Opacity = 0;
        var sb = new Storyboard();
        var move = new DoubleAnimation { To = 0, Duration = TimeSpan.FromMilliseconds(260), EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        Storyboard.SetTarget(move, t); Storyboard.SetTargetProperty(move, "X");
        var fade = new DoubleAnimation { To = 1, Duration = TimeSpan.FromMilliseconds(220) };
        Storyboard.SetTarget(fade, Body); Storyboard.SetTargetProperty(fade, "Opacity");
        sb.Children.Add(move); sb.Children.Add(fade);
        sb.Begin();
    }

    void Password_Changed(object sender, RoutedEventArgs e)
    {
        if (!signUp) return;
        string p = Password.Password;
        bool ok = p.Length >= 8 && p.Any(char.IsLetter) && p.Any(char.IsDigit);
        Rules.Text = ok ? "✓ Strong enough" : "8+ characters, with a letter and a number";
    }

    void Field_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter) { e.Handled = true; Go_Click(sender, new RoutedEventArgs()); }
    }

    // ---------- Email ----------
    static string? EmailProblem(string em)
    {
        if (em.Length == 0) return "Type your email.";
        if (!Regex.IsMatch(em, @"^[A-Za-z0-9._%+-]+@[A-Za-z0-9-]+(\.[A-Za-z0-9-]+)*\.[A-Za-z]{2,}$")) return "That email doesn't look right. It should look like you@example.com.";
        return null;
    }

    async void Go_Click(object sender, RoutedEventArgs e)
    {
        if (busy) return;
        string em = Email.Text.Trim(), pw = Password.Password;
        var problem = EmailProblem(em);
        if (problem != null) { Show(problem); Email.Focus(FocusState.Programmatic); return; }
        if (pw.Length == 0) { Show("Type your password."); return; }
        if (signUp && (pw.Length < 8 || !pw.Any(char.IsLetter) || !pw.Any(char.IsDigit)))
        { Show("Use at least 8 characters with a letter and a number."); return; }

        await Run(async () =>
        {
            if (signUp) await Auth.SignUp(em, pw); else await Auth.SignIn(em, pw);
        });
    }

    async void Forgot_Click(object sender, RoutedEventArgs e)
    {
        var problem = EmailProblem(Email.Text.Trim());
        if (problem != null) { Show("Type your email above first, then click Forgot password."); return; }
        try { await Auth.SendPasswordReset(Email.Text.Trim()); Show("Reset email sent. Check your inbox.", InfoBarSeverity.Success); }
        catch (Exception ex) { Show(Auth.Friendly(ex)); }
    }

    // ---------- Google (opens your browser) ----------
    async void Google_Click(object sender, RoutedEventArgs e)
    {
        if (busy) { googleWait?.Cancel(); return; }
        googleWait = new CancellationTokenSource();
        Show("Finish signing in with Google in your browser, then come back here.", InfoBarSeverity.Informational);
        GoogleText.Text = "Cancel";
        await Run(() => Auth.SignInWithGoogle(googleWait.Token), keepMessage: true);
        GoogleText.Text = signUp ? "Sign up with Google" : "Continue with Google";
    }

    // ---------- Guest ----------
    void Guest_Click(object sender, RoutedEventArgs e)
    {
        Store.UseAsGuest();
        App.Window?.ShowPlanner();
    }

    async Task Run(Func<Task> work, bool keepMessage = false)
    {
        busy = true;
        GoText.Opacity = 0; GoBusy.IsActive = true; Go.IsEnabled = false;
        if (!keepMessage) Message.IsOpen = false;
        try
        {
            await work();
            Show("Loading your planner…", InfoBarSeverity.Success);
            try { await Store.SyncDown(); } catch { }
            App.Window?.ShowPlanner();
        }
        catch (Exception ex)
        {
            Show(Auth.Friendly(ex));
        }
        finally
        {
            busy = false;
            GoText.Opacity = 1; GoBusy.IsActive = false; Go.IsEnabled = true;
        }
    }

    void Show(string text, InfoBarSeverity sev = InfoBarSeverity.Error)
    {
        Message.Message = text;
        Message.Severity = sev;
        Message.IsOpen = true;
    }
}
