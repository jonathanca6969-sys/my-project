using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace TurretApp.Views;

public partial class SettingsPage : Page
{
    public SettingsPage()
    {
        InitializeComponent();
        UpdateButtonText();
    }

    private void OnThemeToggle(object sender, RoutedEventArgs e)
    {
        bool isCurrentlyGreen = App.State.CurrentTheme != "Fluromac";
        string newTheme = isCurrentlyGreen ? "Fluromac" : "Green";

        App.State.CurrentTheme = newTheme;
        App.SaveState();
        App.ApplyTheme(newTheme);

        // Re-navigate to this page so it picks up the new theme resources
        var main = (MainWindow)Application.Current.MainWindow;
        main.NavigateTo(new SettingsPage());
    }

    private void OnResetAllStations(object sender, RoutedEventArgs e)
    {
        var r = MessageBox.Show("Clear ALL mounted tools from ALL stations?",
            "Reset Turret", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (r != MessageBoxResult.Yes) return;
        foreach (var s in App.State.TurretStations)
        { s.MountedPunchId = null; s.MountedDieId = null; }
        App.SaveState();
    }

    private void UpdateButtonText()
    {
        bool isGreen = App.State.CurrentTheme != "Fluromac";
        BtnThemeToggle.Content = isGreen ? "theme green" : "theme Fluromac";
    }
}
