using Foxnet.Desktop.ViewModels;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Foxnet.Desktop;

public partial class MainWindow : Window
{
    private bool _closePending;

    public MainWindow() => InitializeComponent();

    private void Minimize(object sender, RoutedEventArgs e) => SystemCommands.MinimizeWindow(this);

    private void ToggleMaximized(object sender, RoutedEventArgs e)
    {
        if (WindowState == WindowState.Maximized)
            SystemCommands.RestoreWindow(this);
        else
            SystemCommands.MaximizeWindow(this);
    }

    private void CloseWindow(object sender, RoutedEventArgs e) => SystemCommands.CloseWindow(this);

    protected override async void OnClosing(CancelEventArgs e)
    {
        if (DataContext is ShellViewModel { Devices: { Busy: true } device })
        {
            e.Cancel = true;
            if (!_closePending)
            {
                _closePending = true;
                var operation = device.ConnectCommand.IsRunning
                    ? device.ConnectCommand.ExecutionTask : device.DisconnectCommand.ExecutionTask;
                if (operation != null)
                    await operation;
                _closePending = false;
                Close();
            }
            return;
        }

        base.OnClosing(e);
    }
}

public sealed class PageMatchConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool match = value?.ToString() == parameter?.ToString();
        return targetType == typeof(Visibility) ? (match ? Visibility.Visible : Visibility.Collapsed) : match;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
