using System.Windows;
using System.Windows.Controls;
using Dms.Desktop.FleetConsole.ViewModels;

namespace Dms.Desktop.FleetConsole;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    // PasswordBox.Password can't be data-bound directly (it's not a
    // DependencyProperty, by design, so it never lands in the visual tree /
    // memory dumps). This is the standard MVVM workaround: push it into the
    // view model manually on change.
    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel && sender is PasswordBox passwordBox)
        {
            viewModel.Password = passwordBox.Password;
        }
    }
}
