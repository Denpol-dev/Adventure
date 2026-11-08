using Foxnet.Desktop.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Foxnet.Desktop.Views;

public partial class SignInView : UserControl
{
    public SignInView() => InitializeComponent();

    private void UpdateFocus(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible)
            Dispatcher.InvokeAsync(FocusEntry, DispatcherPriority.Input);
    }

    private async void Submit(object sender, RoutedEventArgs e)
    {
        if (DataContext is not SignInViewModel model)
            return;

        var input = new PasswordInput(PasswordEntry.Password, ConfirmationEntry.Password);
        if (!model.SubmitCommand.CanExecute(input))
            return;

        PasswordEntry.Clear();
        ConfirmationEntry.Clear();
        await model.SubmitCommand.ExecuteAsync(input);
        FocusEntry();
    }

    private void FocusEntry()
    {
        if (!IsVisible || DataContext is not SignInViewModel model)
            return;

        if (model.ShowLogin && string.IsNullOrEmpty(model.Login))
            LoginEntry.Focus();
        else
            PasswordEntry.Focus();
    }
}
