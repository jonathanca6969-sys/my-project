using System.Windows;
using System.Windows.Controls;

namespace TurretApp.Views;

public partial class HomePage : Page
{
    private readonly MainWindow _main;

    public HomePage(MainWindow main)
    {
        InitializeComponent();
        _main = main;
    }

    private void GoDieHeight(object s, RoutedEventArgs e) => _main.NavigateTo(new DieHeightPage());
    private void GoClearance(object s, RoutedEventArgs e) => _main.NavigateTo(new ClearancePage());
    private void GoTonnage(object s, RoutedEventArgs e) => _main.NavigateTo(new TonnagePage());
    private void GoSbr(object s, RoutedEventArgs e) => _main.NavigateTo(new SbrPage());
    private void GoToolLibrary(object s, RoutedEventArgs e) => _main.NavigateTo(new ToolLibraryPage());
    private void GoTurretView(object s, RoutedEventArgs e) => _main.NavigateTo(new TurretViewPage());
}
