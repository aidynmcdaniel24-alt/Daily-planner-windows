using System.Diagnostics;
using DailyPlanner.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace DailyPlanner.Pages;

public sealed partial class SettingsPage : Page
{
    bool loading;

    public SettingsPage() { InitializeComponent(); }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        loading = true;
        ThemePick.SelectedIndex = Theme.Mode switch { "light" => 1, "dark" => 2, _ => 0 };
        loading = false;
        var v = typeof(App).Assembly.GetName().Version;
        VersionText.Text = $"Daily Planner for Windows {v?.Major}.{v?.Minor}.{v?.Build}";
        DrawAccount();
    }

    void DrawAccount()
    {
        var s = Auth.Current;
        if (s != null)
        {
            AcctName.Text = s.Name.Length > 0 ? s.Name : (Store.Name.Length > 0 ? Store.Name : s.Email.Split('@')[0]);
            AcctInfo.Text = "Signed in as " + s.Email;
            Avatar.DisplayName = AcctName.Text;
            SignInOut.Content = "Log out";
            SyncNow.Visibility = Visibility.Visible;
            SyncInfo.Text = "Your plan syncs with the website automatically.";
        }
        else
        {
            AcctName.Text = Store.Name.Length > 0 ? Store.Name : "Guest";
            AcctInfo.Text = "Not signed in. Your data only saves on this computer.";
            Avatar.DisplayName = AcctName.Text;
            SignInOut.Content = "Sign in to sync";
            SyncNow.Visibility = Visibility.Collapsed;
            SyncInfo.Text = "";
        }
    }

    async void SyncNow_Click(object sender, RoutedEventArgs e)
    {
        SyncNow.IsEnabled = false; SyncInfo.Text = "Syncing…";
        try
        {
            var r = await Store.SyncDown();
            SyncInfo.Text = r switch { "down" => "Got the latest from your account.", "up" => "Saved this computer's changes to your account.", _ => "Everything is up to date." };
        }
        catch (Exception ex) { SyncInfo.Text = Auth.Friendly(ex); }
        SyncNow.IsEnabled = true;
    }

    async void SignInOut_Click(object sender, RoutedEventArgs e)
    {
        if (Auth.Current == null) { App.Window?.ShowLogin(); return; }
        var d = new ContentDialog
        {
            Title = "Log out?",
            Content = "Your data stays saved in your account. Log back in anytime.",
            PrimaryButtonText = "Log out",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };
        if (await d.ShowAsync() != ContentDialogResult.Primary) return;
        Auth.SignOut();
        Store.ClearLocal();
        App.Window?.ShowLogin();
    }

    void ThemePick_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (loading) return;
        Theme.Set(ThemePick.SelectedIndex switch { 1 => "light", 2 => "dark", _ => "auto" });
    }

    void WebSettings_Click(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo(Config.Website + "settings/") { UseShellExecute = true });

    void Folder_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(Store.Folder);
        Process.Start(new ProcessStartInfo(Store.Folder) { UseShellExecute = true });
    }
}
