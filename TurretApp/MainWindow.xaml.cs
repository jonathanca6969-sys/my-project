using System.Windows;
using System.Windows.Controls;
using TurretApp.Views;

namespace TurretApp;

public partial class MainWindow : Window
{
    private readonly Button[] _navButtons;

    public MainWindow()
    {
        InitializeComponent();
        _navButtons = [BtnDieHeight, BtnClearance, BtnTonnage, BtnSbr, BtnToolLib, BtnTurretView];
        NavigateTo(new HomePage(this));
    }

    public void NavigateTo(Page page)
    {
        bool isHome = page is HomePage;
        NavBar.Visibility = isHome ? Visibility.Collapsed : Visibility.Visible;
        Footer.Visibility = isHome ? Visibility.Collapsed : Visibility.Visible;
        ContentFrame.Navigate(page);

        // Highlight active nav button
        string tag = page switch
        {
            DieHeightPage => "DIE HEIGHT",
            ClearancePage => "CLEARANCE",
            TonnagePage => "TONNAGE",
            SbrPage => "SBR",
            ToolLibraryPage => "TOOL LIBRARY",
            TurretViewPage => "TURRET VIEW",
            _ => ""
        };

        foreach (var btn in _navButtons)
        {
            btn.Style = (string)btn.Content == tag
                ? (Style)Application.Current.FindResource("NavButtonActive")
                : (Style)Application.Current.FindResource("NavButton");
        }
    }

    private void NavHome(object s, RoutedEventArgs e) => NavigateTo(new HomePage(this));
    private void NavDieHeight(object s, RoutedEventArgs e) => NavigateTo(new DieHeightPage());
    private void NavClearance(object s, RoutedEventArgs e) => NavigateTo(new ClearancePage());
    private void NavTonnage(object s, RoutedEventArgs e) => NavigateTo(new TonnagePage());
    private void NavSbr(object s, RoutedEventArgs e) => NavigateTo(new SbrPage());
    private void NavToolLibrary(object s, RoutedEventArgs e) => NavigateTo(new ToolLibraryPage());
    private void NavTurretView(object s, RoutedEventArgs e) => NavigateTo(new TurretViewPage());
    private void NavSettings(object s, RoutedEventArgs e) => NavigateTo(new SettingsPage());
}
