using Foxnet.Desktop.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Foxnet.Desktop.Views;

public partial class DevicesView : UserControl
{
    public DevicesView() => InitializeComponent();

    private void Authorize(object sender, RoutedEventArgs e) => SendCredentials();

    private void PasswordKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;
        e.Handled = true;
        SendCredentials();
    }

    private void SendCredentials()
    {
        if (DataContext is DevicesViewModel model && model.ConnectCommand.CanExecute(PasswordInput.Password))
            model.ConnectCommand.Execute(PasswordInput.Password);
        PasswordInput.Clear();
    }
}
